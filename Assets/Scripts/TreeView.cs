using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 스킬트리 화면 (Shelldiver식). 어두운 배경, 마름모 노드, 계열 색 선.
    /// 마우스 올리면 설명 상자, 클릭하면 산다. 휠 = 확대, 드래그(빈 곳) 또는 WASD = 이동. Tab = 닫기.
    /// </summary>
    public class TreeView : MonoBehaviour
    {
        const float Spacing = 150f;     // 트리 좌표 1 = 150px
        const float NodeSize = 58f;

        Game game;
        List<SkillNode> nodes;
        Dictionary<string, HashSet<string>> nb;
        Dictionary<string, SkillNode> byId = new Dictionary<string, SkillNode>();
        Font font;
        Image veil; RectTransform world;
        readonly Dictionary<string, NodeView> views = new Dictionary<string, NodeView>();
        readonly List<(Image img, string a, string b)> lines = new List<(Image, string, string)>();
        Image tip; Text tipTitle, tipDesc, tipCost;
        Text money;
        SkillNode hover;
        float zoom = 1f;
        Vector2 dragLast; bool dragging;

        class NodeView { public RectTransform Root; public Image Edge, Fill; public Text Glyph, Lv; public SkillNode Node; }

        public void Build(Game g, List<SkillNode> list, Transform canvas)
        {
            game = g; nodes = list; nb = SkillTree.Neighbors(list);
            foreach (var n in nodes) byId[n.Id] = n;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            veil = Panel("Tree", canvas, Vector2.zero, new Vector2(4000f, 3000f), new Color(0.06f, 0.08f, 0.16f, 1f));
            // 배경 점 무늬: 희미한 마름모 몇 개
            var rng = new System.Random(3);
            for (int i = 0; i < 40; i++)
            {
                var d = Panel("Bg", veil.transform, new Vector2(rng.Next(-1100, 1100), rng.Next(-650, 650)), Vector2.one * rng.Next(30, 90), new Color(1f, 1f, 1f, 0.025f));
                d.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }
            world = Panel("World", veil.transform, Vector2.zero, Vector2.zero, Color.clear).rectTransform;
            // 선 먼저 (노드 뒤)
            var done = new HashSet<string>();
            foreach (var n in nodes) foreach (var o in nb[n.Id])
            {
                string key = string.CompareOrdinal(n.Id, o) < 0 ? n.Id + "|" + o : o + "|" + n.Id;
                if (!done.Add(key)) continue;
                var line = Panel("Line", world, Vector2.zero, Vector2.zero, Color.white);
                line.rectTransform.pivot = new Vector2(0f, 0.5f);
                lines.Add((line, n.Id, o));
            }
            foreach (var n in nodes) views[n.Id] = MakeNode(n);
            // 설명 상자
            tip = Panel("Tip", veil.transform, Vector2.zero, new Vector2(420f, 140f), new Color(0.05f, 0.1f, 0.08f, 0.96f));
            var tipEdge = Panel("TipEdge", tip.transform, Vector2.zero, new Vector2(426f, 146f), new Color(0.5f, 1f, 0.6f, 0.7f)); tipEdge.transform.SetAsFirstSibling();
            tipTitle = Txt("TipTitle", tip.transform, new Vector2(0f, 42f), new Vector2(390f, 36f), 24, TextAnchor.MiddleCenter, Color.white);
            tipDesc = Txt("TipDesc", tip.transform, new Vector2(0f, 2f), new Vector2(390f, 50f), 19, TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 0.9f));
            tipCost = Txt("TipCost", tip.transform, new Vector2(0f, -45f), new Vector2(390f, 34f), 22, TextAnchor.MiddleCenter, SkillTree.Yellow);
            tip.gameObject.SetActive(false);
            // 위 돈, 아래 안내
            money = Txt("Money", veil.transform, new Vector2(760f, 455f), new Vector2(360f, 60f), 36, TextAnchor.MiddleRight, SkillTree.Yellow);
            var hint = Txt("Hint", veil.transform, new Vector2(0f, -500f), new Vector2(1400f, 40f), 20, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
            hint.text = "클릭 = 산다   ·   휠 = 확대   ·   드래그·방향키 = 이동   ·   Tab = 닫는다";
        }

        NodeView MakeNode(SkillNode n)
        {
            var v = new NodeView { Node = n };
            var root = Panel("Node_" + n.Id, world, n.Pos * Spacing, Vector2.one * NodeSize, Color.clear);
            v.Root = root.rectTransform;
            v.Edge = Panel("Edge", root.transform, Vector2.zero, Vector2.one * NodeSize, n.Color);
            v.Edge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            v.Fill = Panel("Fill", root.transform, Vector2.zero, Vector2.one * (NodeSize - 8f), new Color(0.08f, 0.1f, 0.2f));
            v.Fill.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            v.Glyph = Txt("Glyph", root.transform, Vector2.zero, Vector2.one * NodeSize, 24, TextAnchor.MiddleCenter, Color.white);
            v.Glyph.text = n.Glyph;
            v.Lv = Txt("Lv", root.transform, new Vector2(0f, -NodeSize * 0.75f), new Vector2(120f, 24f), 15, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.8f));
            return v;
        }

        public bool Available(SkillNode n)
        {
            if (n.Root) return false;
            foreach (var o in nb[n.Id]) if (byId.TryGetValue(o, out var m) && m.Level >= 1) return true;
            return false;
        }

        public void SetOpen(bool open)
        {
            veil.gameObject.SetActive(open);
            if (open) Refresh();
        }

        void Refresh()
        {
            foreach (var kv in views)
            {
                var n = kv.Value.Node; var v = kv.Value;
                bool avail = Available(n);
                bool owned = n.Level >= 1;
                v.Edge.color = owned ? n.Color : (avail ? Color.Lerp(n.Color, Color.white, 0.2f) : new Color(n.Color.r, n.Color.g, n.Color.b, 0.25f));
                v.Fill.color = n.Maxed ? Color.Lerp(n.Color, Color.black, 0.45f) : (owned ? Color.Lerp(n.Color, new Color(0.08f, 0.1f, 0.2f), 0.7f) : new Color(0.08f, 0.1f, 0.2f));
                v.Glyph.color = owned || avail ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                v.Lv.text = n.Root ? "" : (n.MaxLevel == 1 ? (n.Maxed ? "완료" : "") : $"{n.Level}/{n.MaxLevel}");
                v.Lv.color = n.Maxed ? SkillTree.Green : new Color(1f, 1f, 1f, 0.75f);
                bool hot = hover == n;
                v.Root.localScale = Vector3.one * (hot ? 1.18f : 1f);
            }
            foreach (var (img, a, b) in lines)
            {
                var na = byId[a]; var nbn = byId[b];
                Vector2 pa = na.Pos * Spacing, pb = nbn.Pos * Spacing;
                var d = pb - pa;
                img.rectTransform.anchoredPosition = pa;
                img.rectTransform.sizeDelta = new Vector2(d.magnitude, 3f);
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                bool lit = na.Level >= 1 && nbn.Level >= 1;
                bool half = na.Level >= 1 || nbn.Level >= 1;
                var c = na.Root ? nbn.Color : na.Color;
                img.color = lit ? c : (half ? new Color(c.r, c.g, c.b, 0.45f) : new Color(c.r, c.g, c.b, 0.15f));
            }
            money.text = $"${game.Money:N0}";
        }

        void Update()
        {
            if (game == null || !veil.gameObject.activeSelf) return;
            var mouse = Mouse.current; var kb = Keyboard.current;
            // 이동·확대
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) zoom = Mathf.Clamp(zoom * (wheel > 0 ? 1.12f : 0.9f), 0.5f, 1.8f);
            }
            world.localScale = Vector3.one * zoom;
            if (kb != null)
            {
                var pan = Vector2.zero;
                if (kb.upArrowKey.isPressed) pan.y -= 1f; if (kb.downArrowKey.isPressed) pan.y += 1f;
                if (kb.leftArrowKey.isPressed) pan.x += 1f; if (kb.rightArrowKey.isPressed) pan.x -= 1f;
                world.anchoredPosition += pan * 600f * Time.unscaledDeltaTime;
            }
            // 마우스 → 트리 좌표
            Vector2 mp = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            float k = 1080f / Screen.height;
            Vector2 canvasPt = new Vector2(mp.x - Screen.width * 0.5f, mp.y - Screen.height * 0.5f) * k;
            Vector2 worldPt = (canvasPt - world.anchoredPosition) / zoom;
            hover = null; float best = NodeSize * 0.8f;
            foreach (var n in nodes)
            {
                float d = Vector2.Distance(worldPt, n.Pos * Spacing);
                if (d < best) { best = d; hover = n; }
            }
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (hover != null) TryBuy(hover);
                    else { dragging = true; dragLast = canvasPt; }
                }
                if (dragging && mouse.leftButton.isPressed) { world.anchoredPosition += canvasPt - dragLast; dragLast = canvasPt; }
                if (mouse.leftButton.wasReleasedThisFrame) dragging = false;
            }
            // 설명 상자
            if (hover != null)
            {
                tip.gameObject.SetActive(true);
                tip.rectTransform.anchoredPosition = canvasPt + new Vector2(240f, 90f);
                tipTitle.text = hover.MaxLevel == 1 ? hover.Name : $"{hover.Name}  Lv {hover.Level:00} / {hover.MaxLevel:00}";
                tipDesc.text = hover.Maxed ? "최대" : hover.Desc(hover.Level);
                bool avail = Available(hover);
                tipCost.text = hover.Root ? "" : hover.Maxed ? "" : !avail ? "<color=#888888>옆 노드를 먼저</color>" : (game.Money >= hover.NextCost ? $"${hover.NextCost:N0}" : $"<color=#ff6666>${hover.NextCost:N0}</color>");
                tipCost.supportRichText = true;
            }
            else tip.gameObject.SetActive(false);
            Refresh();
        }

        void TryBuy(SkillNode n)
        {
            if (n.Root || n.Maxed || !Available(n) || game.Money < n.NextCost) { Sfx.Play(Sfx.Click(), 0.5f, 0.8f); return; }
            game.BuyNode(n);
            StartCoroutine(Pulse(views[n.Id].Root));
        }

        System.Collections.IEnumerator Pulse(RectTransform rt)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 5f) { rt.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.5f); yield return null; }
        }

        Image Panel(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.AddComponent<Image>(); img.color = color; img.raycastTarget = false;
            return img;
        }

        Text Txt(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.alignment = anchor; t.color = color; t.raycastTarget = false; t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
