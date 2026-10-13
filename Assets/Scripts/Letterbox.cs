using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 우클릭 연출 (사용자 10-13): 우클릭 능력(느려지기)이 실제로 켜져 있는 동안 위아래에서 남색 띠(레터박스)가 들어오고,
    /// UI는 탄만 남고 전부 사라진다 (탄은 AmmoHud 묶음이 부모 투명도를 무시). 능력이 끝나면 돌아간다.
    /// 평소 화면엔 띠가 없다 (사용자: "기본이 이러니 어색하다, 레터박스는 우클릭 연출로").
    /// 띠는 배경(-2) 위, 탄피(1)·깡통(2) 아래 → 깡통은 띠 위로도 보인다 (서부 영화 느낌).
    /// 띠 두께는 도트 단위(최대 45칸 = 화면 1/6), 움직임은 용수철.
    /// </summary>
    public class Letterbox : MonoBehaviour
    {
        const float MaxDots = 45f, Ppu = 27f;
        const float UiDim = 0f;             // 띠가 다 들어왔을 때 UI 투명도 (탄 빼고 전부 사라짐)
        SpriteRenderer top, bottom;
        CanvasGroup ui;
        float k, kv;                        // 0 = 없음 … 1 = 다 들어옴 (용수철)
        /// <summary>연출이 띠를 강제로 올린다 (현상금 막타 슬로우모션)</summary>
        public static bool Force;

        public static Letterbox Make(Transform hudCanvas)
        {
            Force = false;
            var lb = new GameObject("Letterbox").AddComponent<Letterbox>();
            lb.top = Look.Quad("BarTop", Vector2.zero, Vector2.one, Look.Bar, 0, lb.transform);
            lb.bottom = Look.Quad("BarBottom", Vector2.zero, Vector2.one, Look.Bar, 0, lb.transform);
            lb.ui = hudCanvas.GetComponent<CanvasGroup>();
            if (lb.ui == null) lb.ui = hudCanvas.gameObject.AddComponent<CanvasGroup>();
            lb.Apply();
            return lb;
        }

        void LateUpdate()
        {
            var g = Game.I;
            bool on = Force || (g != null && g.State == GameState.Play && !g.MenuOpen && g.SlowActive);   // 꽉 찬 게이지로 능력을 켰을 때, 또는 연출(현상금 막타)
            // 들어올 땐 빠르게 살짝 넘쳤다 자리, 나갈 땐 조금 느긋하게
            Ease.Spring(ref k, ref kv, on ? 1f : 0f, on ? 260f : 160f, on ? 16f : 18f, Time.unscaledDeltaTime);
            // 끝나기 1초 전부터 깜박임 (사용자 10-13): 남은 시간이 줄수록 빨라진다 (0.25초 주기 → 0.07초)
            float left = on ? g.SlowLeft : 99f;
            if (left < 1f)
            {
                float period = Mathf.Lerp(0.07f, 0.25f, left);
                blinkPhase += Time.unscaledDeltaTime / period;
                blinkDim = (blinkPhase % 1f) >= 0.5f;
            }
            else { blinkPhase = 0f; blinkDim = false; }
            var col = Look.Bar; if (blinkDim) col.a = 0.3f;
            top.color = bottom.color = col;
            Apply();
        }
        float blinkPhase; bool blinkDim;

        void Apply()
        {
            var cam = Camera.main; if (cam == null) return;
            float halfH = cam.orthographicSize, w = halfH * 2f * cam.aspect + 2f;
            float dots = Mathf.Round(Mathf.Clamp(k, 0f, 1.15f) * MaxDots);   // 두께는 도트 단위 (넘칠 땐 최대 52칸)
            float h = dots / Ppu;
            var c = cam.transform.position;
            top.transform.position = new Vector3(c.x, c.y + halfH - h * 0.5f, 0f);
            bottom.transform.position = new Vector3(c.x, c.y - halfH + h * 0.5f, 0f);
            top.transform.localScale = bottom.transform.localScale = new Vector3(w, Mathf.Max(0.0001f, h), 1f);
            top.enabled = bottom.enabled = dots > 0f;
            if (ui != null) ui.alpha = Mathf.Lerp(1f, UiDim, Mathf.Clamp01(k));
        }
    }
}
