using UnityEngine;
using UnityEngine.InputSystem;

namespace TinCan
{
    /// <summary>
    /// 디버그 모드 (사용자 10-14): F9로 켜고 끈다. 깡통마다 머리 위에 속도·중력 배율·무게·질량·체력·상태,
    /// 왼쪽 위에 전체 값(공중 수, 저글링 낙하 배수, 시간 배수, 튐 보너스). 개발용 IMGUI (게임 UI와 별개)
    /// </summary>
    public class CanDebug : MonoBehaviour
    {
        bool on;
        GUIStyle style, head;
        Texture2D bg;

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame) on = !on;
        }

        void OnGUI()
        {
            if (!on || Game.I == null) return;
            if (style == null)
            {
                bg = new Texture2D(1, 1); bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f)); bg.Apply();
                style = new GUIStyle(GUI.skin.box) { font = Ui.Font, fontSize = 14, alignment = TextAnchor.UpperLeft, wordWrap = false, richText = false };
                style.normal.background = bg; style.normal.textColor = Color.white; style.padding = new RectOffset(6, 6, 4, 4);
                head = new GUIStyle(style) { fontSize = 16 };
                head.normal.textColor = new Color(1f, 0.9f, 0.5f);
            }
            var cam = Camera.main; if (cam == null) return;
            float k = Screen.height / 1080f;
            // 전체
            var g = Game.I;
            int air = g.Airborne;
            string top = $"[디버그 F9]  전체 속도 ×{Game.DebugTime:0.#} (F10)  공중 {air}  저글링 낙하 ×{Can.JuggleFall[Mathf.Clamp(air, 1, Can.JuggleFall.Length) - 1]:0.##}  " +
                         $"시간 ×{Can.TimeScale:0.00}  튐 보너스 +{Can.BounceBonus:0.##}  기본 중력 {-Physics2D.gravity.y:0.##}×2.2";
            var hs = head.CalcSize(new GUIContent(top));
            GUI.Box(new Rect(10 * k, 120 * k, hs.x, hs.y), top, head);
            // 깡통마다
            foreach (var c in g.Cans)
            {
                if (c == null) continue;
                string t = c.DebugText();
                var sz = style.CalcSize(new GUIContent(t));
                var sp = cam.WorldToScreenPoint(c.transform.position + Vector3.up * (c.Kind.Height * 0.5f + 0.35f));
                float x = sp.x - sz.x * 0.5f, y = Screen.height - sp.y - sz.y;
                x = Mathf.Clamp(x, 0f, Screen.width - sz.x); y = Mathf.Clamp(y, 0f, Screen.height - sz.y);
                GUI.Box(new Rect(x, y, sz.x, sz.y), t, style);
            }
        }
    }
}
