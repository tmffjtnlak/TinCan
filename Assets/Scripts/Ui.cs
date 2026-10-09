using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>UI 공통 부품: 테두리 있는 판자, 그림자 있는 글자. 한 그림체로 통일한다 (어두운 나무판 + 밝은 모서리 + 노란 돈).</summary>
    public static class Ui
    {
        static Font font;
        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

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
            Image("Fill", edge.transform, Vector2.zero, size - new Vector2(6f, 6f), Board);
            if (nails)
            {
                var nail = new Color(0.75f, 0.7f, 0.6f);
                Image("NailL", edge.transform, new Vector2(-size.x * 0.5f + 14f, size.y * 0.5f - 14f), new Vector2(6f, 6f), nail);
                Image("NailR", edge.transform, new Vector2(size.x * 0.5f - 14f, size.y * 0.5f - 14f), new Vector2(6f, 6f), nail);
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
                sh.effectColor = new Color(0f, 0f, 0f, 0.7f); sh.effectDistance = new Vector2(2f, -2f);
            }
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font; t.fontSize = fontSize; t.alignment = anchor; t.color = color; t.raycastTarget = false; t.supportRichText = true;
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
