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

        // 집중(우클릭 능력) 카메라 줌 (사용자 10-13): 켜면 0.9배 거리로 부드럽게 다가가고,
        // 마지막 1초 동안 남은 시간만큼 천천히 풀리고 띠도 조금씩 내려가다가, 끝나면 띠는 확 빠진다. (깜박임은 뺐다)
        const float ZoomIn = 0.9f, FadeSeconds = 1f, BarAtEnd = 0.5f;
        float zoom = 1f, zoomV, uiK, uiV, baseSize = -1f;

        void LateUpdate()
        {
            var g = Game.I;
            bool play = g != null && g.State == GameState.Play;
            bool slow = play && !g.MenuOpen && g.SlowActive;
            bool on = Force || slow;   // 꽉 찬 게이지로 능력을 켰을 때, 또는 연출(현상금 막타)
            float dt = Time.unscaledDeltaTime;
            // 남은 시간 비율: 마지막 1초 동안 1 → 0 (그 전엔 1)
            float tail = slow ? Mathf.Clamp01(g.SlowLeft / FadeSeconds) : 1f;
            // 띠: 들어올 땐 빠르게 살짝 넘쳤다 자리. 마지막 1초엔 남은 만큼 내려가고(최소 50%), 끝나면 아주 빠르게(확) 빠진다
            float target = on ? Mathf.Lerp(BarAtEnd, 1f, tail) : 0f;
            if (on) Ease.Spring(ref k, ref kv, target, tail < 1f ? 120f : 260f, tail < 1f ? 20f : 16f, dt);
            else Ease.Spring(ref k, ref kv, 0f, 900f, 40f, dt);
            // UI: 켜져 있는 동안은 계속 숨김 (띠가 조금 내려가도 UI는 안 돌아온다). 끝나면 띠와 같이 돌아온다
            Ease.Spring(ref uiK, ref uiV, on ? 1f : 0f, on ? 260f : 900f, on ? 16f : 40f, dt);
            // 줌: 집중 중에만. 부드럽게(덜 출렁) 다가가고, 마지막 1초엔 남은 만큼 천천히 풀린다
            float zTarget = slow ? Mathf.Lerp(1f, ZoomIn, tail) : 1f;
            Ease.Spring(ref zoom, ref zoomV, zTarget, 40f, 12f, dt);
            if (play && !Force)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    if (baseSize < 0f) baseSize = cam.orthographicSize;
                    cam.orthographicSize = baseSize * zoom;
                }
            }
            top.color = bottom.color = Look.Bar;
            Apply();
        }

        void Apply()
        {
            var cam = Camera.main; if (cam == null) return;
            float halfH = cam.orthographicSize, w = halfH * 2f * cam.aspect + 2f;
            float dots = Mathf.Round(Mathf.Clamp(k, 0f, 1.15f) * MaxDots);   // 두께는 화면 도트 단위 (넘칠 땐 최대 52칸)
            float screenDot = halfH * 2f / 270f;                              // 줌해도 띠 두께는 화면 기준 그대로
            float h = dots * screenDot;
            var c = cam.transform.position;
            top.transform.position = new Vector3(c.x, c.y + halfH - h * 0.5f, 0f);
            bottom.transform.position = new Vector3(c.x, c.y - halfH + h * 0.5f, 0f);
            top.transform.localScale = bottom.transform.localScale = new Vector3(w, Mathf.Max(0.0001f, h), 1f);
            top.enabled = bottom.enabled = dots > 0f;
            if (ui != null) ui.alpha = Mathf.Lerp(1f, UiDim, Mathf.Clamp01(uiK));
        }
    }
}
