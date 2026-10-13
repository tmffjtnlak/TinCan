using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// UI 공통 부품: 테두리 있는 판자, 그림자 있는 글자. 한 그림체로 통일한다 (어두운 나무판 + 밝은 모서리 + 노란 돈).
    /// 도트 규칙 (사용자 10-12): 그림 1픽셀 = 화면 3픽셀(1080p). UI 두께·간격·그림자도 전부 3의 배수 = "같은 도트 크기".
    /// </summary>
    public static class Ui
    {
        /// <summary>마스터 픽셀: 1080p 기준 화면 픽셀 수. 화면비 480×270 (사용자 10-13 최종) → 4. 그림(PPU 27)과 UI가 이 한 칸을 같이 쓴다</summary>
        public const float Px = 4f;
        static Font font;
        /// <summary>도트 폰트 (사용자 10-12: x10y12pxDenkiChipHangul, 12px 격자). 없으면 기본 벡터 폰트</summary>
        public static Font Font => font != null ? font : (font = Resources.Load<Font>("Fonts/DenkiChip") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        public static bool PixelFont => Font != null && Font.name != "LegacyRuntime";
        /// <summary>도트 폰트는 12의 배수 크기에서만 또렷하다: 가장 가까운 배수로 (최소 12)</summary>
        public static int Snap(int size) => PixelFont ? Mathf.Max(12, Mathf.RoundToInt(size / 12f) * 12) : size;

        public static readonly Color Board = new Color(0.2f, 0.13f, 0.09f, 0.96f);
        public static readonly Color BoardEdge = new Color(0.55f, 0.38f, 0.22f, 1f);
        public static readonly Color BoardDark = new Color(0.1f, 0.06f, 0.04f, 1f);
        public static readonly Color Gold = new Color(1f, 0.85f, 0.35f);
        public static readonly Color Ink = new Color(0.95f, 0.92f, 0.85f);
        public static readonly Color Dim = new Color(0.95f, 0.92f, 0.85f, 0.55f);
        public static readonly Color Good = new Color(0.55f, 1f, 0.6f);
        public static readonly Color Bad = new Color(1f, 0.4f, 0.35f);
        public static readonly Color Curse = new Color(0.8f, 0.55f, 1f);

        public static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static Image Image(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var rt = Rect(name, parent, pos, size);
            var img = rt.gameObject.AddComponent<Image>(); img.color = color; img.raycastTarget = false;
            return img;
        }

        /// <summary>판자: 밝은 테두리 + 어두운 속. 왼쪽 위에 못 두 개</summary>
        public static Image Panel(string name, Transform parent, Vector2 pos, Vector2 size, bool nails = true)
        {
            var edge = Image(name, parent, pos, size, BoardEdge);
            Image("Fill", edge.transform, Vector2.zero, size - new Vector2(Px * 2f, Px * 2f), Board);   // 테두리 = 한 픽셀
            if (nails)
            {
                var nail = new Color(0.75f, 0.7f, 0.6f);
                Image("NailL", edge.transform, new Vector2(-size.x * 0.5f + Px * 4f, size.y * 0.5f - Px * 4f), new Vector2(Px * 2f, Px * 2f), nail);
                Image("NailR", edge.transform, new Vector2(size.x * 0.5f - Px * 4f, size.y * 0.5f - Px * 4f), new Vector2(Px * 2f, Px * 2f), nail);
            }
            return edge;
        }

        /// <summary>글자: 검은 그림자 한 겹 아래에</summary>
        public static Text Text(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor, Color color, bool shadow = true)
        {
            var rt = Rect(name, parent, pos, size);
            if (shadow)
            {
                var sh = rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0f, 0f, 0f, 0.7f); sh.effectDistance = new Vector2(Px, -Px);   // 그림자 = 한 픽셀
            }
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.fontSize = Snap(fontSize); t.alignment = anchor; t.color = color; t.raycastTarget = false; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>마름모 아이콘 자리 (그림이 오면 교체)</summary>
        public static Image Diamond(string name, Transform parent, Vector2 pos, float size, Color edge, Color fill)
        {
            var e = Image(name, parent, pos, Vector2.one * size, edge);
            e.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image("Fill", e.transform, Vector2.zero, Vector2.one * (size - 8f), fill);
            return e;
        }
    }
}
