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
    /// 그림은 전부 TreeArt 자리에서 온다 (이미지만 바꾸면 됨). 움직임은 전부 여기.
    /// 움직임 원칙 (10-12, 리뷰 5천+ 인크리멘탈들의 공통 문법): **짧고 딱딱하게.** 들어갈 땐 2~3프레임, 나올 땐 6~10프레임.
    /// 넘침은 10% 안쪽, 회전 흔들림 없음, 흐린 번짐 대신 도트 조각. 흰 플래시 2프레임. 작은 흔들림 2~3픽셀.
    ///   열기 = 전체가 0.1초에 살짝 커지며 뜬다 (노드 하나씩 안 띄운다)
    ///   올리면 = 즉시 1.1배 + 네 귀퉁이 흰 꺾쇠가 딱 붙는다. 설명 상자는 0.06초에 톡
    ///   사면 = 흰 플래시 → 펀치 → "+" 조각이 튀어 떨어진다 → 이웃 선을 따라 빛이 달려가 새 노드를 깨운다 → 연속 구매면 소리가 올라간다
    ///   살 수 있는 노드 = 주변에서 점이 하나씩 떠오른다 (번짐 아님)
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
        Image veil; RectTransform world; CanvasGroup worldGroup;
        readonly Dictionary<string, NodeView> views = new Dictionary<string, NodeView>();
        readonly List<LinkView> links = new List<LinkView>();
        Image tip; Text tipTitle, tipDesc, tipCost; float tipPop;
        Text money; float moneyBump; long moneyShown;
        RectTransform cursor; Image[] corners;
        SkillNode hover;
        float zoom = 1f, zoomTarget = 1f;
        Vector2 dragLast; bool dragging;
        int buyStreak; float lastBuy;
        float dotTimer;

        class NodeView
        {
            public SkillNode Node; public RectTransform Root;
            public Image Frame, Fill, Icon; public Text Glyph, Lv;
            public float Scale = 1f, Punch, Flash, Wake;
        }
        class LinkView { public Image Img; public string A, B; public float Flash; }

        public void Build(Game g, List<SkillNode> list, Transform canvas)
        {
            game = g; nodes = list; nb = SkillTree.Neighbors(list);
            foreach (var n in nodes) byId[n.Id] = n;

            veil = Img("Tree", canvas, Vector2.zero, new Vector2(4000f, 3000f), "bg_tile", new Color(0.07f, 0.09f, 0.17f, 1f));
            veil.type = Image.Type.Tiled; veil.pixelsPerUnitMultiplier = Mult;
            world = Img("World", veil.transform, Vector2.zero, Vector2.zero, null, Color.clear).rectTransform;
            worldGroup = world.gameObject.AddComponent<CanvasGroup>();

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

            // 선택 꺾쇠: 네 귀퉁이 (world 안, 노드 위)
            cursor = Img("Cursor", world, Vector2.zero, Vector2.zero, null, Color.clear).rectTransform;
            corners = new Image[4];
            float off = NodePx * Px * 0.5f + Px * 3f;
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sy = i < 2 ? 1f : -1f;
                corners[i] = Img("Corner", cursor, new Vector2(sx * off, sy * off), Vector2.one * 7f * Px, "cursor_corner", Color.white);
                corners[i].rectTransform.localScale = new Vector3(sx, sy, 1f);   // 왼쪽 위 그림을 뒤집어 네 귀퉁이
            }
            cursor.gameObject.SetActive(false);

            tip = Img("Tip", veil.transform, Vector2.zero, new Vector2(420f, 141f), "tip_panel", new Color(0.05f, 0.1f, 0.08f, 0.97f));
            tip.type = Image.Type.Sliced; tip.pixelsPerUnitMultiplier = Mult;
            tipTitle = Txt("TipTitle", tip.transform, new Vector2(0f, 42f), new Vector2(390f, 36f), 24, TextAnchor.MiddleCenter, Color.white);
            tipDesc = Txt("TipDesc", tip.transform, new Vector2(0f, 3f), new Vector2(390f, 50f), 19, TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 0.9f));
            tipCost = Txt("TipCost", tip.transform, new Vector2(0f, -45f), new Vector2(390f, 34f), 22, TextAnchor.MiddleCenter, SkillTree.Yellow);
            tip.gameObject.SetActive(false);

            money = Txt("Money", veil.transform, new Vector2(760f, 455f), new Vector2(360f, 60f), 36, TextAnchor.MiddleRight, SkillTree.Yellow);
            var hint = Txt("Hint", veil.transform, new Vector2(0f, -500f), new Vector2(1400f, 40f), 20, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
            hint.text = "클릭 = 산다   ·   휠 = 확대   ·   드래그·방향키 = 이동   ·   Tab = 닫는다";
            veil.gameObject.SetActive(false);   // 처음엔 닫혀 있다 (연출 없이)
        }

        NodeView MakeNode(SkillNode n)
        {
            var v = new NodeView { Node = n };
            float size = NodePx * Px;
            var root = Img("Node_" + n.Id, world, n.Pos * Spacing, Vector2.one * size, null, Color.clear);
            v.Root = root.rectTransform;
            v.Fill = Img("Fill", root.transform, Vector2.zero, Vector2.one * size, "node_fill", new Color(0.08f, 0.1f, 0.2f));
            v.Frame = Img("Frame", root.transform, Vector2.zero, Vector2.one * size, "node_frame", n.Color);
            string iconName = "icon_" + n.Id;
            if (TreeArt.Has(iconName)) v.Icon = Img("Icon", root.transform, Vector2.zero, Vector2.one * 12f * Px, iconName, Color.white);
            else { v.Glyph = Txt("Glyph", root.transform, Vector2.zero, Vector2.one * size, 24, TextAnchor.MiddleCenter, Color.white); v.Glyph.text = n.Glyph; }
            v.Lv = Txt("Lv", root.transform, new Vector2(0f, -size * 0.75f), new Vector2(120f, 24f), 15, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.8f));
            return v;
        }

        public bool Available(SkillNode n)
        {
            if (n.Root) return false;
            foreach (var o in nb[n.Id]) if (byId.TryGetValue(o, out var m) && m.Level >= 1) return true;
            return false;
        }

        Coroutine anim; bool isOpen;
        public void SetOpen(bool open)
        {
            if (open == isOpen) return;
            isOpen = open;
            if (anim != null) StopCoroutine(anim);   // 닫히는 중에 열면 닫기 연출이 뒤늦게 꺼 버리지 않게
            if (open)
            {
                veil.gameObject.SetActive(true);
                moneyShown = game.Money;
                hover = null; cursor.gameObject.SetActive(false); tip.gameObject.SetActive(false);
                Refresh();
                anim = StartCoroutine(OpenAnim());
            }
            else anim = StartCoroutine(CloseAnim());
        }

        /// <summary>열기: 전체가 0.1초에 살짝 커지며 뜬다. 노드를 하나씩 띄우지 않는다 (기다리게 하지 않는다)</summary>
        IEnumerator OpenAnim()
        {
            Sfx.Play(Sfx.Click(), 0.4f, 1.3f);
            var vc = veil.color;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.1f)
            {
                float e = Ease.OutQuad(t);
                veil.color = new Color(vc.r, vc.g, vc.b, e);
                worldGroup.alpha = e;
                world.localScale = Vector3.one * zoom * Mathf.Lerp(0.94f, 1f, e);
                yield return null;
            }
            veil.color = vc; worldGroup.alpha = 1f;
        }

        IEnumerator CloseAnim()
        {
            var vc = veil.color;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.07f)
            {
                veil.color = new Color(vc.r, vc.g, vc.b, 1f - t);
                worldGroup.alpha = 1f - t;
                world.localScale = Vector3.one * zoom * (1f - t * 0.04f);
                yield return null;
            }
            veil.color = vc; worldGroup.alpha = 1f;
            veil.gameObject.SetActive(false);
        }

        void Refresh()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var kv in views)
            {
                var n = kv.Value.Node; var v = kv.Value;
                bool avail = Available(n), owned = n.Level >= 1, hot = hover == n;
                v.Flash = Mathf.Max(0f, v.Flash - dt / 0.07f);          // 흰 플래시: 2~4프레임
                v.Punch = Mathf.Max(0f, v.Punch - dt / 0.14f);
                v.Wake = Mathf.Max(0f, v.Wake - dt / 0.3f);
                var frame = owned ? n.Color : (avail ? Color.Lerp(n.Color, Color.white, 0.25f) : new Color(n.Color.r, n.Color.g, n.Color.b, 0.3f));
                var fill = n.Maxed ? Color.Lerp(n.Color, Color.black, 0.45f) : (owned ? Color.Lerp(n.Color, new Color(0.08f, 0.1f, 0.2f), 0.7f) : new Color(0.08f, 0.1f, 0.2f));
                if (v.Flash > 0f || v.Wake > 0f) { float w = Mathf.Max(v.Flash > 0f ? 1f : 0f, v.Wake); frame = Color.Lerp(frame, Color.white, w); fill = Color.Lerp(fill, Color.white, w * 0.8f); }
                v.Frame.color = frame; v.Fill.color = fill;
                var ink = owned || avail ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                if (v.Glyph != null) v.Glyph.color = ink; if (v.Icon != null) v.Icon.color = ink;
                v.Lv.text = n.Root ? "" : (n.Maxed ? "완료" : $"${n.NextCost:N0}");
                v.Lv.color = n.Maxed ? SkillTree.Green : avail ? (game.Money >= n.NextCost ? SkillTree.Green : new Color(1f, 0.45f, 0.4f)) : new Color(1f, 1f, 1f, 0.45f);
                // 크기: 올리면 즉시 1.1 (3프레임 안에), 펀치는 산 모양
                float target = hot ? 1.1f : 1f;
                v.Scale = Mathf.Lerp(v.Scale, target, 1f - Mathf.Exp(-dt * 45f));
                v.Root.localScale = Vector3.one * (v.Scale + Ease.Punch(v.Punch) * 0.22f + v.Wake * 0.1f);
            }
            foreach (var l in links)
            {
                var na = byId[l.A]; var nbn = byId[l.B];
                Vector2 pa = na.Pos * Spacing, pb = nbn.Pos * Spacing;
                var d = pb - pa;
                l.Img.rectTransform.anchoredPosition = pa;
                l.Img.rectTransform.sizeDelta = new Vector2(d.magnitude, 2f * Px);
                l.Img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                bool lit = na.Level >= 1 && nbn.Level >= 1;
                bool half = na.Level >= 1 || nbn.Level >= 1;
                var c = na.Root ? nbn.Color : na.Color;
                l.Img.sprite = TreeArt.Get(lit ? "link" : "link_dim");
                var col = lit ? c : (half ? new Color(c.r, c.g, c.b, 0.7f) : new Color(c.r, c.g, c.b, 0.22f));
                l.Flash = Mathf.Max(0f, l.Flash - dt / 0.2f);
                l.Img.color = Color.Lerp(col, Color.white, l.Flash);
            }
            if (moneyShown != game.Money) moneyShown = Mathf.Abs(game.Money - moneyShown) < 3 ? game.Money : (long)Mathf.Lerp(moneyShown, game.Money, 1f - Mathf.Exp(-dt * 14f)) + System.Math.Sign(game.Money - moneyShown);
            moneyBump = Mathf.Max(0f, moneyBump - dt / 0.15f);
            money.text = $"${moneyShown:N0}";
            money.transform.localScale = Vector3.one * (1f + Ease.Punch(moneyBump) * 0.12f);
        }

        void Update()
        {
            if (game == null || !veil.gameObject.activeSelf) return;
            var mouse = Mouse.current; var kb = Keyboard.current;
            float dt = Time.unscaledDeltaTime;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f) zoomTarget = Mathf.Clamp(zoomTarget * (wheel > 0 ? 1.15f : 0.87f), 0.5f, 1.8f);
            }
            zoom = Mathf.Lerp(zoom, zoomTarget, 1f - Mathf.Exp(-dt * 20f));
            world.localScale = Vector3.one * zoom;
            if (kb != null)
            {
                var pan = Vector2.zero;
                if (kb.upArrowKey.isPressed) pan.y -= 1f; if (kb.downArrowKey.isPressed) pan.y += 1f;
                if (kb.leftArrowKey.isPressed) pan.x += 1f; if (kb.rightArrowKey.isPressed) pan.x -= 1f;
                world.anchoredPosition += pan * 600f * dt;
            }
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
            if (hover != null && hover != prev) { Sfx.Play(Sfx.Click(), 0.2f, 2.6f); tipPop = 0f; StartCoroutine(CursorSnap()); }
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
            // 꺾쇠: 올린 노드 위에 딱. 상자: 0.06초에 톡, 자리 고정 (따라다니지 않는다)
            cursor.gameObject.SetActive(hover != null);
            if (hover != null)
            {
                cursor.anchoredPosition = hover.Pos * Spacing;
                tip.gameObject.SetActive(true);
                tip.rectTransform.anchoredPosition = hover.Pos * Spacing * zoom + world.anchoredPosition + new Vector2(260f, 70f);
                tipPop = Mathf.Min(1f, tipPop + dt / 0.06f);
                tip.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, Ease.OutQuad(tipPop));
                tipTitle.text = hover.Name;
                tipDesc.text = hover.Desc(hover.Level);
                bool avail = Available(hover);
                tipCost.text = hover.Root ? "" : hover.Maxed ? "" : !avail ? "<color=#888888>옆 노드를 먼저</color>" : (game.Money >= hover.NextCost ? $"<color=#8cff9a>${hover.NextCost:N0}</color>" : $"<color=#ff6666>${hover.NextCost:N0}</color>");
                tipCost.supportRichText = true;
            }
            else tip.gameObject.SetActive(false);
            AmbientDots(dt);
            Refresh();
        }

        /// <summary>꺾쇠가 바깥에서 안으로 딱 붙는다 (3프레임)</summary>
        IEnumerator CursorSnap()
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.05f)
            {
                cursor.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Ease.OutQuad(t));
                yield return null;
            }
            cursor.localScale = Vector3.one;
        }

        /// <summary>살 수 있는 노드 주변에 점이 하나씩 떠올라 사라진다 (번짐 대신 도트 조각)</summary>
        void AmbientDots(float dt)
        {
            dotTimer -= dt;
            if (dotTimer > 0f) return;
            dotTimer = 0.12f;
            var pool = new List<NodeView>();
            foreach (var v in views.Values) if (Available(v.Node) && v.Node.Level < 1) pool.Add(v);
            if (pool.Count == 0) return;
            var pick = pool[Random.Range(0, pool.Count)];
            var at = pick.Root.anchoredPosition + new Vector2(Random.Range(-1f, 1f), Random.Range(-0.6f, 0.6f)) * NodePx * Px * 0.9f;
            bool plus = Random.value < 0.3f;
            var dot = Img(plus ? "Plus" : "Dot", world, at, Vector2.one * (plus ? 5f : 2f) * Px, plus ? "fx_spark" : "fx_dot", Color.Lerp(pick.Node.Color, Color.white, 0.3f));
            dot.transform.SetSiblingIndex(pick.Root.GetSiblingIndex());   // 노드 뒤
            StartCoroutine(DotCo(dot));
        }

        IEnumerator DotCo(Image d)
        {
            var from = d.rectTransform.anchoredPosition;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.7f)
            {
                d.rectTransform.anchoredPosition = from + Vector2.up * t * 18f;
                var c = d.color; c.a = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f; d.color = c;
                yield return null;
            }
            Destroy(d.gameObject);
        }

        void TryBuy(SkillNode n)
        {
            var v = views[n.Id];
            if (n.Root || n.Maxed || !Available(n) || game.Money < n.NextCost)
            {
                Sfx.Play(Sfx.Click(), 0.5f, 0.7f);
                StartCoroutine(Shake(v.Root));
                return;
            }
            var before = new HashSet<string>(); foreach (var o in nodes) if (Available(o)) before.Add(o.Id);
            game.BuyNode(n);
            // 연속 구매면 소리가 올라간다 (1초 안에)
            buyStreak = Time.unscaledTime - lastBuy < 1f ? buyStreak + 1 : 0; lastBuy = Time.unscaledTime;
            Sfx.Play(Sfx.Tang(), 0.6f, 1.2f + Mathf.Min(buyStreak, 8) * 0.12f);
            moneyBump = 1f;
            v.Flash = 1f; v.Punch = 1f;
            Fx.Shake(0.08f);
            StartCoroutine(BuyFx(v));
            // 이웃 선을 따라 빛이 달려가 새로 열린 노드를 깨운다
            foreach (var l in links)
            {
                if (l.A != n.Id && l.B != n.Id) continue;
                l.Flash = 1f;
                string otherId = l.A == n.Id ? l.B : l.A;
                bool woke = !before.Contains(otherId) && Available(byId[otherId]);
                StartCoroutine(PulseAlong(n.Pos * Spacing, byId[otherId].Pos * Spacing, n.Color, woke ? views[otherId] : null));
            }
        }

        /// <summary>산 순간: "+" 조각 10개가 튀어 올랐다 떨어진다 (fx_spark), 작은 링 (fx_ring)</summary>
        IEnumerator BuyFx(NodeView v)
        {
            var c = v.Node.Color;
            var ring = Img("Ring", v.Root.parent, v.Root.anchoredPosition, Vector2.one * 16f * Px, "fx_ring", Color.white);
            StartCoroutine(RingCo(ring, c));
            for (int i = 0; i < 10; i++) StartCoroutine(SparkCo(Img("Spark", v.Root.parent, v.Root.anchoredPosition, Vector2.one * 5f * Px, "fx_spark", i % 3 == 0 ? Color.white : c)));
            yield return null;
        }

        IEnumerator RingCo(Image ring, Color c)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.22f)
            {
                ring.rectTransform.localScale = Vector3.one * (0.6f + Ease.OutExpo(t) * 1.6f);
                ring.color = Color.Lerp(Color.white, new Color(c.r, c.g, c.b, 0f), t);
                yield return null;
            }
            Destroy(ring.gameObject);
        }

        /// <summary>조각: 위로 튀었다가 중력에 떨어진다. 도트니까 회전 없이</summary>
        IEnumerator SparkCo(Image s)
        {
            var from = s.rectTransform.anchoredPosition;
            var vel = new Vector2(Random.Range(-220f, 220f), Random.Range(160f, 420f));
            var pos = from; float life = Random.Range(0.35f, 0.55f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / life)
            {
                float dt = Time.unscaledDeltaTime;
                vel.y -= 1400f * dt; pos += vel * dt;
                s.rectTransform.anchoredPosition = pos;
                if (t > 0.7f) { var c = s.color; c.a = 1f - (t - 0.7f) / 0.3f; s.color = c; }
                yield return null;
            }
            Destroy(s.gameObject);
        }

        /// <summary>선을 따라 빛 한 토막이 달려간다 (0.12초). 닿으면 그 노드가 깨어난다</summary>
        IEnumerator PulseAlong(Vector2 a, Vector2 b, Color c, NodeView wake)
        {
            var d = b - a;
            var p = Img("Pulse", world, a, new Vector2(10f * Px, 2f * Px), "link", Color.white);
            p.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            p.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.12f)
            {
                p.rectTransform.anchoredPosition = a + d * t;
                yield return null;
            }
            Destroy(p.gameObject);
            if (wake != null)
            {
                wake.Wake = 1f;
                Sfx.Play(Sfx.Click(), 0.35f, 2.2f);
                for (int i = 0; i < 4; i++) StartCoroutine(SparkCo(Img("Spark", wake.Root.parent, wake.Root.anchoredPosition, Vector2.one * 5f * Px, "fx_spark", Color.white)));
            }
        }

        IEnumerator Shake(RectTransform rt)
        {
            var home = rt.anchoredPosition;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.18f) { rt.anchoredPosition = home + Vector2.right * Mathf.Sin(t * Mathf.PI * 4f) * 2f * Px * (1f - t); yield return null; }
            rt.anchoredPosition = home;
        }

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
