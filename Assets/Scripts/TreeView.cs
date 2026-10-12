using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 스킬트리 화면 (Shelldiver식). 어두운 타일 배경, 마름모 노드, 계열 색 선.
    /// 마우스 올리면 설명 상자, 클릭하면 산다. 휠 = 확대, 드래그(빈 곳) 또는 방향키 = 이동. Tab = 닫기.
    ///
    /// 그림은 전부 TreeArt 자리에서 온다 (이미지만 바꾸면 됨). 움직임은 전부 여기 (사용자 10-12: 트윈 들어간 트리):
    ///   열기 = 뿌리에서 가까운 순으로 노드가 톡톡 튀어나오고 선이 자란다
    ///   올리면 = 커지며 흔들, 설명 상자가 톡 뜨고 따라온다
    ///   사면 = 펀치 + 링 + 조각 + 이웃 선이 흰색으로 번쩍 + 새로 열린 이웃이 깨어남
    ///   살 수 있는 노드 = 숨쉬듯 번짐
    /// </summary>
    public class TreeView : MonoBehaviour
    {
        const float Spacing = 150f;                 // 트리 좌표 1 = 150px
        static float Px => Ui.Px;
        const float NodePx = 20f;                   // node_frame 그림 크기(픽셀)
        /// <summary>타일·9슬라이스가 그림 1픽셀 = 화면 Px 픽셀이 되게 하는 배수 (캔버스 기준 PPU 100)</summary>
        static float Mult => 100f / (TreeArt.Ppu * Px);

        Game game;
        List<SkillNode> nodes;
        Dictionary<string, HashSet<string>> nb;
        readonly Dictionary<string, SkillNode> byId = new Dictionary<string, SkillNode>();
        Image veil; RectTransform world;
        readonly Dictionary<string, NodeView> views = new Dictionary<string, NodeView>();
        readonly List<LinkView> links = new List<LinkView>();
        Image tip; Text tipTitle, tipDesc, tipCost; float tipScale;
        Text money; float moneyBump; long moneyShown;
        SkillNode hover;
        float zoom = 1f, zoomTarget = 1f;
        Vector2 dragLast; bool dragging;
        float openTime;

        class NodeView
        {
            public SkillNode Node; public RectTransform Root;
            public Image Glow, Frame, Fill, Icon; public Text Glyph, Lv;
            public float Scale = 1f, Target = 1f, Punch, Wobble, Wake, Seed;
            public bool Shown;
        }
        class LinkView { public Image Img; public string A, B; public float Grow = 1f, Flash; }

        public void Build(Game g, List<SkillNode> list, Transform canvas)
        {
            game = g; nodes = list; nb = SkillTree.Neighbors(list);
            foreach (var n in nodes) byId[n.Id] = n;

            // 배경: 타일 반복 (bg_tile). 색은 코드
            veil = Img("Tree", canvas, Vector2.zero, new Vector2(4000f, 3000f), "bg_tile", new Color(0.07f, 0.09f, 0.17f, 1f));
            veil.type = Image.Type.Tiled; veil.pixelsPerUnitMultiplier = Mult;
            world = Img("World", veil.transform, Vector2.zero, Vector2.zero, null, Color.clear).rectTransform;

            // 선 먼저 (노드 뒤)
            var done = new HashSet<string>();
            foreach (var n in nodes) foreach (var o in nb[n.Id])
            {
                string key = string.CompareOrdinal(n.Id, o) < 0 ? n.Id + "|" + o : o + "|" + n.Id;
                if (!done.Add(key)) continue;
                var line = Img("Line", world, Vector2.zero, Vector2.zero, "link", Color.white);
                line.type = Image.Type.Tiled; line.pixelsPerUnitMultiplier = Mult;
                line.rectTransform.pivot = new Vector2(0f, 0.5f);
                links.Add(new LinkView { Img = line, A = n.Id, B = o });
            }
            foreach (var n in nodes) views[n.Id] = MakeNode(n);

            // 설명 상자 (tip_panel 9슬라이스)
            tip = Img("Tip", veil.transform, Vector2.zero, new Vector2(420f, 141f), "tip_panel", new Color(0.05f, 0.1f, 0.08f, 0.97f));
            tip.type = Image.Type.Sliced; tip.pixelsPerUnitMultiplier = Mult;
            tipTitle = Txt("TipTitle", tip.transform, new Vector2(0f, 42f), new Vector2(390f, 36f), 24, TextAnchor.MiddleCenter, Color.white);
            tipDesc = Txt("TipDesc", tip.transform, new Vector2(0f, 3f), new Vector2(390f, 50f), 19, TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 0.9f));
            tipCost = Txt("TipCost", tip.transform, new Vector2(0f, -45f), new Vector2(390f, 34f), 22, TextAnchor.MiddleCenter, SkillTree.Yellow);
            tip.gameObject.SetActive(false);

            // 위 돈, 아래 안내
            money = Txt("Money", veil.transform, new Vector2(760f, 455f), new Vector2(360f, 60f), 36, TextAnchor.MiddleRight, SkillTree.Yellow);
            var hint = Txt("Hint", veil.transform, new Vector2(0f, -500f), new Vector2(1400f, 40f), 20, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
            hint.text = "클릭 = 산다   ·   휠 = 확대   ·   드래그·방향키 = 이동   ·   Tab = 닫는다";
        }

        NodeView MakeNode(SkillNode n)
        {
            var v = new NodeView { Node = n, Seed = Random.value * 10f };
            float size = NodePx * Px;
            var root = Img("Node_" + n.Id, world, n.Pos * Spacing, Vector2.one * size, null, Color.clear);
            v.Root = root.rectTransform;
            v.Glow = Img("Glow", root.transform, Vector2.zero, Vector2.one * 28f * Px, "node_glow", Color.clear);
            v.Fill = Img("Fill", root.transform, Vector2.zero, Vector2.one * size, "node_fill", new Color(0.08f, 0.1f, 0.2f));
            v.Frame = Img("Frame", root.transform, Vector2.zero, Vector2.one * size, "node_frame", n.Color);
            // 아이콘: icon_{id} 그림이 있으면 그걸, 없으면 글자
            string iconName = "icon_" + n.Id;
            if (TreeArt.Has(iconName)) v.Icon = Img("Icon", root.transform, Vector2.zero, Vector2.one * 12f * Px, iconName, Color.white);
            else { v.Glyph = Txt("Glyph", root.transform, Vector2.zero, Vector2.one * size, 24, TextAnchor.MiddleCenter, Color.white); v.Glyph.text = n.Glyph; }
            v.Lv = Txt("Lv", root.transform, new Vector2(0f, -size * 0.75f), new Vector2(120f, 24f), 15, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.8f));
            v.Root.localScale = Vector3.zero;
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
            if (open && !veil.gameObject.activeSelf)
            {
                veil.gameObject.SetActive(true);
                openTime = Time.unscaledTime;
                moneyShown = game.Money;
                StartCoroutine(OpenAnim());
            }
            else if (!open && veil.gameObject.activeSelf) StartCoroutine(CloseAnim());
        }

        /// <summary>열기: 뿌리에서 가까운 순으로 노드가 톡톡, 선은 그 사이에 자란다</summary>
        IEnumerator OpenAnim()
        {
            foreach (var v in views.Values) { v.Shown = false; v.Scale = 0f; v.Root.localScale = Vector3.zero; }
            foreach (var l in links) l.Grow = 0f;
            var root = nodes.Find(n => n.Root);
            Vector2 origin = root != null ? root.Pos : Vector2.zero;
            var order = new List<NodeView>(views.Values);
            order.Sort((a, b) => Vector2.Distance(a.Node.Pos, origin).CompareTo(Vector2.Distance(b.Node.Pos, origin)));
            var veilC = veil.color; veil.color = new Color(veilC.r, veilC.g, veilC.b, 0f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.12f) { veil.color = new Color(veilC.r, veilC.g, veilC.b, Ease.OutQuad(t)); yield return null; }
            veil.color = veilC;
            float started = Time.unscaledTime;
            foreach (var v in order)
            {
                float delay = Vector2.Distance(v.Node.Pos, origin) * 0.045f;
                StartCoroutine(PopNode(v, delay));
                foreach (var l in links) if (l.Grow <= 0f && (l.A == v.Node.Id || l.B == v.Node.Id)) StartCoroutine(GrowLink(l, delay + 0.06f));
            }
            yield return null;
        }

        IEnumerator PopNode(NodeView v, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            v.Shown = true;
            Sfx.Play(Sfx.Click(), 0.12f, Random.Range(1.6f, 2.2f));
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.22f) { v.Scale = Ease.OutBack(t, 2.2f); yield return null; }
            v.Scale = 1f;
        }

        IEnumerator GrowLink(LinkView l, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.18f) { l.Grow = Ease.OutCubic(t); yield return null; }
            l.Grow = 1f;
        }

        IEnumerator CloseAnim()
        {
            var c = veil.color;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.08f)
            {
                world.localScale = Vector3.one * zoom * (1f - t * 0.06f);
                veil.color = new Color(c.r, c.g, c.b, 1f - t);
                yield return null;
            }
            veil.color = c;
            veil.gameObject.SetActive(false);
        }

        void Refresh()
        {
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            foreach (var kv in views)
            {
                var n = kv.Value.Node; var v = kv.Value;
                bool avail = Available(n), owned = n.Level >= 1, hot = hover == n;
                // 색
                v.Frame.color = owned ? n.Color : (avail ? Color.Lerp(n.Color, Color.white, 0.2f) : new Color(n.Color.r, n.Color.g, n.Color.b, 0.25f));
                v.Fill.color = n.Maxed ? Color.Lerp(n.Color, Color.black, 0.45f) : (owned ? Color.Lerp(n.Color, new Color(0.08f, 0.1f, 0.2f), 0.7f) : new Color(0.08f, 0.1f, 0.2f));
                var ink = owned || avail ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                if (v.Glyph != null) v.Glyph.color = ink; if (v.Icon != null) v.Icon.color = ink;
                v.Lv.text = n.Root ? "" : (n.Maxed ? "완료" : $"${n.NextCost:N0}");
                v.Lv.color = n.Maxed ? SkillTree.Green : new Color(1f, 1f, 1f, 0.75f);
                // 살 수 있으면 숨쉬는 번짐, 깨어날 때 확 밝게
                float breathe = avail && !owned ? 0.35f + Mathf.Sin(now * 3f + v.Seed) * 0.15f : 0f;
                v.Wake = Mathf.Max(0f, v.Wake - dt * 2f);
                v.Glow.color = new Color(n.Color.r, n.Color.g, n.Color.b, Mathf.Clamp01(breathe + v.Wake * 0.8f + (hot ? 0.25f : 0f)));
                v.Glow.rectTransform.localScale = Vector3.one * (1f + v.Wake * 0.4f);
                // 크기: 목표로 부드럽게 + 펀치 + 흔들
                v.Target = hot ? 1.2f : 1f;
                if (!v.Shown) continue;
                v.Scale = Mathf.Lerp(v.Scale, v.Target, 1f - Mathf.Exp(-dt * 18f));
                v.Punch = Mathf.Max(0f, v.Punch - dt * 4f);
                v.Wobble = hot ? Mathf.Sin(now * 14f) * 4f : Mathf.Lerp(v.Wobble, 0f, 1f - Mathf.Exp(-dt * 20f));
                v.Root.localScale = Vector3.one * (v.Scale + Ease.Punch(v.Punch) * 0.5f);
                v.Root.localRotation = Quaternion.Euler(0f, 0f, v.Wobble);
            }
            foreach (var l in links)
            {
                var na = byId[l.A]; var nbn = byId[l.B];
                Vector2 pa = na.Pos * Spacing, pb = nbn.Pos * Spacing;
                var d = pb - pa;
                l.Img.rectTransform.anchoredPosition = pa;
                l.Img.rectTransform.sizeDelta = new Vector2(d.magnitude * l.Grow, 2f * Px);
                l.Img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                bool lit = na.Level >= 1 && nbn.Level >= 1;
                bool half = na.Level >= 1 || nbn.Level >= 1;
                var c = na.Root ? nbn.Color : na.Color;
                l.Img.sprite = TreeArt.Get(lit || half ? "link" : "link_dim");
                var col = lit ? c : (half ? new Color(c.r, c.g, c.b, 0.55f) : new Color(c.r, c.g, c.b, 0.2f));
                l.Flash = Mathf.Max(0f, l.Flash - dt * 3f);
                l.Img.color = Color.Lerp(col, Color.white, l.Flash);
            }
            // 돈: 굴러가고 튄다
            if (moneyShown != game.Money) { moneyShown = Mathf.Abs(game.Money - moneyShown) < 3 ? game.Money : (long)Mathf.Lerp(moneyShown, game.Money, 1f - Mathf.Exp(-dt * 12f)) + System.Math.Sign(game.Money - moneyShown); }
            moneyBump = Mathf.Max(0f, moneyBump - dt * 5f);
            money.text = $"${moneyShown:N0}";
            money.transform.localScale = Vector3.one * (1f + Ease.Punch(moneyBump) * 0.2f);
        }

        void Update()
        {
            if (game == null || !veil.gameObject.activeSelf) return;
            var mouse = Mouse.current; var kb = Keyboard.current;
            float dt = Time.unscaledDeltaTime;
            // 이동·확대 (확대는 목표로 부드럽게)
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) zoomTarget = Mathf.Clamp(zoomTarget * (wheel > 0 ? 1.15f : 0.87f), 0.5f, 1.8f);
            }
            zoom = Mathf.Lerp(zoom, zoomTarget, 1f - Mathf.Exp(-dt * 12f));
            world.localScale = Vector3.one * zoom;
            if (kb != null)
            {
                var pan = Vector2.zero;
                if (kb.upArrowKey.isPressed) pan.y -= 1f; if (kb.downArrowKey.isPressed) pan.y += 1f;
                if (kb.leftArrowKey.isPressed) pan.x += 1f; if (kb.rightArrowKey.isPressed) pan.x -= 1f;
                world.anchoredPosition += pan * 600f * dt;
            }
            // 마우스 → 트리 좌표
            Vector2 mp = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            float k = 1080f / Screen.height;
            Vector2 canvasPt = new Vector2(mp.x - Screen.width * 0.5f, mp.y - Screen.height * 0.5f) * k;
            Vector2 worldPt = (canvasPt - world.anchoredPosition) / zoom;
            var prev = hover;
            hover = null; float best = NodePx * Px * 0.8f;
            foreach (var n in nodes)
            {
                float d = Vector2.Distance(worldPt, n.Pos * Spacing);
                if (d < best) { best = d; hover = n; }
            }
            if (hover != null && hover != prev) { Sfx.Play(Sfx.Click(), 0.15f, 2.4f); tipScale = 0f; }
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
            // 설명 상자: 톡 뜨고 마우스를 부드럽게 따라온다
            if (hover != null)
            {
                bool was = tip.gameObject.activeSelf;
                tip.gameObject.SetActive(true);
                var want = canvasPt + new Vector2(240f, 90f);
                tip.rectTransform.anchoredPosition = was ? Vector2.Lerp(tip.rectTransform.anchoredPosition, want, 1f - Mathf.Exp(-dt * 22f)) : want;
                tipScale = Mathf.Min(1f, tipScale + dt / 0.12f);
                tip.rectTransform.localScale = Vector3.one * Ease.OutBack(tipScale, 2f);
                tipTitle.text = hover.Name;
                tipDesc.text = hover.Desc(hover.Level);
                bool avail = Available(hover);
                tipCost.text = hover.Root ? "" : hover.Maxed ? "" : !avail ? "<color=#888888>옆 노드를 먼저</color>" : (game.Money >= hover.NextCost ? $"${hover.NextCost:N0}" : $"<color=#ff6666>${hover.NextCost:N0}</color>");
                tipCost.supportRichText = true;
            }
            else tip.gameObject.SetActive(false);
            Refresh();
        }

        void TryBuy(SkillNode n)
        {
            var v = views[n.Id];
            if (n.Root || n.Maxed || !Available(n) || game.Money < n.NextCost)
            {
                Sfx.Play(Sfx.Click(), 0.5f, 0.8f);
                StartCoroutine(Shake(v.Root));   // 못 사면 고개 젓기
                return;
            }
            var before = new HashSet<string>(); foreach (var o in nodes) if (Available(o)) before.Add(o.Id);
            game.BuyNode(n);
            moneyBump = 1f;
            v.Punch = 1f;
            StartCoroutine(BuyFx(v));
            // 이웃 선 번쩍, 새로 열린 이웃 깨어남
            foreach (var l in links) if (l.A == n.Id || l.B == n.Id) l.Flash = 1f;
            foreach (var o in nodes) if (!before.Contains(o.Id) && Available(o)) { views[o.Id].Wake = 1f; views[o.Id].Punch = 0.6f; }
        }

        /// <summary>산 순간: 링이 퍼지고 조각 8개가 사방으로 (fx_ring, fx_spark). 색은 계열 색</summary>
        IEnumerator BuyFx(NodeView v)
        {
            var c = v.Node.Color;
            var ring = Img("Ring", v.Root.parent, v.Root.anchoredPosition, Vector2.one * 16f * Px, "fx_ring", c);
            StartCoroutine(RingCo(ring));
            for (int i = 0; i < 8; i++) StartCoroutine(SparkCo(Img("Spark", v.Root.parent, v.Root.anchoredPosition, Vector2.one * 5f * Px, "fx_spark", Color.Lerp(c, Color.white, 0.4f)), (i + Random.value * 0.5f) * 45f));
            Fx.Shake(0.12f);
            yield return null;
        }

        IEnumerator RingCo(Image ring)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                ring.rectTransform.localScale = Vector3.one * (0.5f + Ease.OutExpo(t) * 3f);
                var c = ring.color; c.a = 1f - Ease.InQuad(t); ring.color = c;
                yield return null;
            }
            Destroy(ring.gameObject);
        }

        IEnumerator SparkCo(Image s, float deg)
        {
            var dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
            var from = s.rectTransform.anchoredPosition; float dist = Random.Range(70f, 130f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.4f)
            {
                s.rectTransform.anchoredPosition = from + dir * dist * Ease.OutExpo(t);
                s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 180f);
                s.rectTransform.localScale = Vector3.one * (1f - t * 0.6f);
                var c = s.color; c.a = 1f - t * t; s.color = c;
                yield return null;
            }
            Destroy(s.gameObject);
        }

        IEnumerator Shake(RectTransform rt)
        {
            var home = rt.anchoredPosition;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.25f) { rt.anchoredPosition = home + Vector2.right * Mathf.Sin(t * Mathf.PI * 4f) * 8f * (1f - t); yield return null; }
            rt.anchoredPosition = home;
        }

        // ---------- 부품 ----------
        Image Img(string name, Transform parent, Vector2 pos, Vector2 size, string art, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var img = go.AddComponent<Image>(); img.color = color; img.raycastTarget = false;
            if (art != null) img.sprite = TreeArt.Get(art);
            return img;
        }

        Text Txt(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor, Color color)
            => Ui.Text(name, parent, pos, size, fontSize, anchor, color);
    }
}
