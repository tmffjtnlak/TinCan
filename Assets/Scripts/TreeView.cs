using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 스킬트리 화면 (Shelldiver식). 어두운 타일 배경, 마름모 노드, 계열 색 선.
    /// 마우스 올리면 설명 상자, 누르면 산다 (누르고 있으면 연달아). 휠 = 확대, 드래그(빈 곳) 또는 방향키 = 이동. Tab = 닫기.
    /// **산 노드와 그 이웃만 보인다** (사용자 10-12). 사면 이웃이 드러난다.
    ///
    /// 그림은 전부 TreeArt 자리에서 온다 (이미지만 바꾸면 됨). 움직임은 전부 여기.
    /// 움직임 문법 (10-12, 3판 — Click the Button·Shelldiver 상점 클립을 1/30초로 뜯어 본 것):
    ///   Click the Button 버튼: 누르면 **1프레임**에 푹 꺼지고, 2프레임 뒤 **원래보다 길게** 튕겨 올랐다가(약 130%) 3~4프레임에 가라앉는다.
    ///     숫자는 오를 때 기울어지며 커졌다 돌아온다. 튕기는 순간 흰 빗금 조각이 옆으로 튄다. 들어가는 이징 없음, 나오는 건 용수철.
    ///   Click the Button 가챠: 느리게 커지는 기대(0.25초) → 1프레임에 쩍 → 2프레임 안에 완성 + 빛살.
    ///   Shelldiver 구매: 노드가 **통째로 흰 네모**가 되어 3프레임쯤 유지 → 색색(초록·분홍·하늘) 점가루 20개+가 노드 두 개 반경으로 넓게 퍼져 0.3초 머문다.
    ///     누르고 있으면 초당 4번쯤 연달아 산다.
    /// </summary>
    public class TreeView : MonoBehaviour
    {
        // Shelldiver 치수 (10-12, 스크린샷 측정, 1080p 환산): 마름모 ≈126px · 테두리 ≈7px · 아이콘 ≈56px · 간격 ≈195px · 도트 ≈4~5px.
        // 트리 화면은 도트 4px (본 게임 3px보다 한 단계 굵게. 사용자: "픽셀 한 칸이 커야"). 마름모 30칸=120px, 아이콘 14칸=56px, 간격 190px
        const float Spacing = 190f;
        public const float Px = 4f;
        const float NodePx = 30f;
        const float IconPx = 14f;
        static float Mult => 100f / (TreeArt.Ppu * Px);

        Game game;
        List<SkillNode> nodes;
        Dictionary<string, HashSet<string>> nb;
        readonly Dictionary<string, SkillNode> byId = new Dictionary<string, SkillNode>();
        Image veil, bgFar, bgNear; RectTransform world; CanvasGroup worldGroup;
        float bgDotTimer;
        readonly Dictionary<string, NodeView> views = new Dictionary<string, NodeView>();
        readonly List<LinkView> links = new List<LinkView>();
        Image tip; Text tipTitle, tipDesc, tipCost; float tipPop, tipFlash, tipTilt, tipTiltV; Vector2 tipPos;
        Text money; float moneyPop, moneyTilt; long moneyShown;
        RectTransform cursor; Image[] corners;
        SkillNode hover;
        float zoom = 1f, zoomTarget = 1f, worldPop = 1f, worldPopV;
        Vector2 dragLast; bool dragging;
        int buyStreak; float lastBuy, holdTimer;
        float dotTimer;

        class NodeView
        {
            public SkillNode Node; public RectTransform Root;
            public Image Frame, Fill, Bevel, Icon; public Text Glyph, Lv;
            public float Sx = 1f, Sy = 1f, Vx, Vy;      // 용수철 크기 (가로·세로 따로 = 찌그러짐)
            public float Flash, Wake, LvPop, LvTilt;
            public bool Visible, Pressed;
        }
        class LinkView { public Image Img; public string A, B; public float Flash; }

        public void Build(Game g, List<SkillNode> list, Transform canvas)
        {
            game = g; nodes = list; nb = SkillTree.Neighbors(list);
            foreach (var n in nodes) byId[n.Id] = n;

            // 배경 세 겹 (전부 느리게 흐른다): 바닥색 → 먼 큰 마름모(bg_far, 아주 느림) → 가까운 점무늬(bg_tile, 조금 빠름)
            veil = Img("Tree", canvas, Vector2.zero, new Vector2(4000f, 3000f), null, new Color(0.06f, 0.08f, 0.15f, 1f));
            bgFar = Img("BgFar", veil.transform, Vector2.zero, new Vector2(4400f, 3400f), "bg_far", new Color(0.12f, 0.15f, 0.28f, 1f));
            bgFar.type = Image.Type.Tiled; bgFar.pixelsPerUnitMultiplier = Mult * 0.5f;   // 먼 건 두 배 크게
            bgNear = Img("BgNear", veil.transform, Vector2.zero, new Vector2(4400f, 3400f), "bg_tile", new Color(0.075f, 0.095f, 0.18f, 1f));
            bgNear.type = Image.Type.Tiled; bgNear.pixelsPerUnitMultiplier = Mult;
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

            cursor = Img("Cursor", world, Vector2.zero, Vector2.zero, null, Color.clear).rectTransform;
            corners = new Image[4];
            float off = NodePx * Px * 0.5f + Px * 3f;
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -1f : 1f, sy = i < 2 ? 1f : -1f;
                corners[i] = Img("Corner", cursor, new Vector2(sx * off, sy * off), Vector2.one * 7f * Px, "cursor_corner", Color.white);
                corners[i].rectTransform.localScale = new Vector3(sx, sy, 1f);
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
            hint.text = "누르면 산다 (누르고 있으면 연달아)   ·   휠 = 확대   ·   드래그·방향키 = 이동   ·   Tab = 닫는다";
            veil.gameObject.SetActive(false);
        }

        NodeView MakeNode(SkillNode n)
        {
            var v = new NodeView { Node = n };
            float size = NodePx * Px;
            var root = Img("Node_" + n.Id, world, n.Pos * Spacing, Vector2.one * size, null, Color.clear);
            v.Root = root.rectTransform;
            v.Fill = Img("Fill", root.transform, Vector2.zero, Vector2.one * size, "node_fill", new Color(0.08f, 0.1f, 0.2f));
            v.Bevel = Img("Bevel", root.transform, Vector2.zero, Vector2.one * size, "node_bevel", new Color(1f, 1f, 1f, 0.12f));
            v.Frame = Img("Frame", root.transform, Vector2.zero, Vector2.one * size, "node_frame", n.Color);
            // 아이콘: 노드별 그림(icon_{id}) → 없으면 계열 그림 → 그것도 없으면 글자
            string iconName = TreeArt.Has("icon_" + n.Id) ? "icon_" + n.Id : "icon_" + Family(n);
            if (TreeArt.Has(iconName)) v.Icon = Img("Icon", root.transform, Vector2.zero, Vector2.one * IconPx * Px, iconName, Color.white);
            else { v.Glyph = Txt("Glyph", root.transform, Vector2.zero, Vector2.one * size, 28, TextAnchor.MiddleCenter, Color.white); v.Glyph.text = n.Glyph; }
            v.Lv = Txt("Lv", root.transform, new Vector2(0f, -size * 0.72f), new Vector2(140f, 26f), 17, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.8f));
            return v;
        }

        /// <summary>계열 이름 (색으로 판단): reload / shelf / money / auto / time / root</summary>
        static string Family(SkillNode n)
        {
            if (n.Root) return "root";
            if (n.Color == SkillTree.Red) return "reload";
            if (n.Color == SkillTree.Cyan) return "shelf";
            if (n.Color == SkillTree.Yellow) return "money";
            if (n.Color == SkillTree.Green) return "auto";
            if (n.Color == SkillTree.Purple) return "time";
            return "root";
        }

        public bool Available(SkillNode n)
        {
            if (n.Root) return false;
            foreach (var o in nb[n.Id]) if (byId.TryGetValue(o, out var m) && m.Level >= 1) return true;
            return false;
        }

        /// <summary>보이는 노드 = 뿌리, 산 것, 살 수 있는 것 (산 노드의 이웃)</summary>
        bool ShouldShow(SkillNode n) => n.Root || n.Level >= 1 || Available(n);

        Coroutine anim; bool isOpen;
        public void SetOpen(bool open)
        {
            if (open == isOpen) return;
            isOpen = open;
            if (anim != null) StopCoroutine(anim);
            if (open)
            {
                veil.gameObject.SetActive(true);
                moneyShown = game.Money;
                hover = null; cursor.gameObject.SetActive(false); tip.gameObject.SetActive(false);
                foreach (var v in views.Values) { v.Visible = ShouldShow(v.Node); v.Root.gameObject.SetActive(v.Visible); v.Sx = v.Sy = 1f; v.Vx = v.Vy = 0f; }
                Refresh();
                anim = StartCoroutine(OpenAnim());
            }
            else anim = StartCoroutine(CloseAnim());
        }

        /// <summary>열기: 0.1초 페이드 + 전체가 용수철로 톡 (0.94 → 1.02 → 1)</summary>
        IEnumerator OpenAnim()
        {
            Sfx.Play(Sfx.Click(), 0.4f, 1.3f);
            worldPop = 0.94f; worldPopV = 1.2f;
            var vc = veil.color;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.1f)
            {
                veil.color = new Color(vc.r, vc.g, vc.b, Ease.OutQuad(t));
                worldGroup.alpha = Ease.OutQuad(t);
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
                if (!v.Visible) continue;
                bool avail = Available(n), owned = n.Level >= 1, hot = hover == n;
                v.Flash = Mathf.Max(0f, v.Flash - dt / 0.22f);          // 흰 네모: 4프레임 꽉 찬 흰색 → 3프레임 사라짐
                v.Wake = Mathf.Max(0f, v.Wake - dt / 0.3f);
                v.LvPop = Mathf.Max(0f, v.LvPop - dt / 0.25f);
                var frame = owned ? n.Color : (avail ? Color.Lerp(n.Color, Color.white, 0.25f) : new Color(n.Color.r, n.Color.g, n.Color.b, 0.3f));
                var fill = n.Maxed ? Color.Lerp(n.Color, Color.black, 0.45f) : (owned ? Color.Lerp(n.Color, new Color(0.08f, 0.1f, 0.2f), 0.7f) : new Color(0.08f, 0.1f, 0.2f));
                // 꽉 찬 흰색(앞 60%) → 섞이며 돌아옴(뒤 40%)
                float wf = v.Flash > 0.4f ? 1f : v.Flash / 0.4f;
                bool white = v.Flash > 0.4f;
                if (v.Flash > 0f) { frame = Color.Lerp(frame, Color.white, wf); fill = Color.Lerp(fill, Color.white, wf); }
                else if (v.Wake > 0f) { frame = Color.Lerp(frame, Color.white, v.Wake); fill = Color.Lerp(fill, Color.white, v.Wake * 0.6f); }
                v.Frame.color = frame; v.Fill.color = fill;
                v.Bevel.color = new Color(1f, 1f, 1f, white ? 0f : 0.12f);
                var ink = white ? Color.white : (owned || avail ? Color.white : new Color(1f, 1f, 1f, 0.3f));   // 흰 네모일 땐 아이콘도 묻힌다
                if (v.Glyph != null) v.Glyph.color = ink; if (v.Icon != null) v.Icon.color = ink;
                v.Lv.text = n.Root ? "" : (n.Maxed ? "완료" : $"${n.NextCost:N0}");
                v.Lv.color = n.Maxed ? SkillTree.Green : avail ? (game.Money >= n.NextCost ? SkillTree.Green : new Color(1f, 0.45f, 0.4f)) : new Color(1f, 1f, 1f, 0.45f);
                // 값 글자: 바뀔 때 기울며 커졌다 돌아온다 (Click the Button 숫자)
                float lp = Ease.Punch(v.LvPop);
                v.Lv.transform.localScale = Vector3.one * (1f + lp * 0.5f);
                v.Lv.transform.localRotation = Quaternion.Euler(0f, 0f, v.LvTilt * lp);
                // 크기: 가로·세로 용수철. 올리면 1.12, 누르면 납작, 놓으면 길게 튕김
                float tx = v.Pressed ? 1.15f : (hot ? 1.12f : 1f), ty = v.Pressed ? 0.8f : (hot ? 1.12f : 1f);
                Ease.Spring(ref v.Sx, ref v.Vx, tx, 420f, 16f, dt);
                Ease.Spring(ref v.Sy, ref v.Vy, ty, 420f, 16f, dt);
                v.Root.localScale = new Vector3(v.Sx + v.Wake * 0.1f, v.Sy + v.Wake * 0.1f, 1f);
            }
            foreach (var l in links)
            {
                var na = byId[l.A]; var nbn = byId[l.B];
                bool show = views[l.A].Visible && views[l.B].Visible;
                l.Img.gameObject.SetActive(show);
                if (!show) continue;
                Vector2 pa = na.Pos * Spacing, pb = nbn.Pos * Spacing;
                var d = pb - pa;
                l.Img.rectTransform.anchoredPosition = pa;
                l.Img.rectTransform.sizeDelta = new Vector2(d.magnitude, 2f * Px);
                l.Img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                bool lit = na.Level >= 1 && nbn.Level >= 1;
                var c = na.Root ? nbn.Color : na.Color;
                l.Img.sprite = TreeArt.Get(lit ? "link" : "link_dim");
                var col = lit ? c : new Color(c.r, c.g, c.b, 0.7f);
                l.Flash = Mathf.Max(0f, l.Flash - dt / 0.2f);
                l.Img.color = Color.Lerp(col, Color.white, l.Flash);
            }
            // 돈: 굴러가고, 바뀔 때 기울며 튄다
            if (moneyShown != game.Money) moneyShown = Mathf.Abs(game.Money - moneyShown) < 3 ? game.Money : (long)Mathf.Lerp(moneyShown, game.Money, 1f - Mathf.Exp(-dt * 14f)) + System.Math.Sign(game.Money - moneyShown);
            moneyPop = Mathf.Max(0f, moneyPop - dt / 0.22f);
            money.text = $"${moneyShown:N0}";
            float mp = Ease.Punch(moneyPop);
            money.transform.localScale = Vector3.one * (1f + mp * 0.25f);
            money.transform.localRotation = Quaternion.Euler(0f, 0f, moneyTilt * mp);
            // 전체 톡 (열 때·살 때)
            Ease.Spring(ref worldPop, ref worldPopV, 1f, 500f, 14f, dt);
            world.localScale = Vector3.one * zoom * worldPop;
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
            Vector2 worldPt = (canvasPt - world.anchoredPosition) / (zoom * worldPop);
            var prev = hover;
            hover = null; float best = NodePx * Px * 0.8f;
            foreach (var n in nodes)
            {
                if (!views[n.Id].Visible) continue;
                float d = Vector2.Distance(worldPt, n.Pos * Spacing);
                if (d < best) { best = d; hover = n; }
            }
            if (hover != null && hover != prev)
            {
                Sfx.Play(Sfx.Click(), 0.2f, 2.6f); tipPop = 0f; StartCoroutine(CursorSnap());
                // 설명 상자가 바뀔 때: 통째로 흰색 3~4프레임, 기울어진 채 들어와서 풀린다 (Shelldiver 3.77~3.93초)
                tipFlash = 1f; tipTilt = Random.Range(-9f, -5f); tipTiltV = 0f;
                if (prev == null) tipPos = canvasPt + new Vector2(260f, 70f);
            }
            if (mouse != null)
            {
                bool down = mouse.leftButton.isPressed;
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (hover != null) { Press(hover); holdTimer = 0.28f; }
                    else { dragging = true; dragLast = canvasPt; }
                }
                // 누르고 있으면 연달아 산다 (Shelldiver: 초당 4번쯤)
                if (down && hover != null && !dragging)
                {
                    holdTimer -= dt;
                    if (holdTimer <= 0f) { Press(hover); holdTimer = 0.25f; }
                }
                foreach (var v in views.Values) if (v.Pressed && (!down || hover != v.Node)) v.Pressed = false;
                if (dragging && down) { world.anchoredPosition += canvasPt - dragLast; dragLast = canvasPt; }
                if (mouse.leftButton.wasReleasedThisFrame) dragging = false;
            }
            cursor.gameObject.SetActive(hover != null);
            if (hover != null)
            {
                cursor.anchoredPosition = hover.Pos * Spacing;
                tip.gameObject.SetActive(true);
                // 마우스를 조금 늦게 따라오고, 움직이는 속도만큼 기운다
                var want = canvasPt + new Vector2(260f, 70f);
                var before = tipPos;
                tipPos = Vector2.Lerp(tipPos, want, 1f - Mathf.Exp(-dt * 16f));
                float velTilt = Mathf.Clamp((tipPos.x - before.x) / Mathf.Max(dt, 0.001f) * -0.004f, -6f, 6f);
                Ease.Spring(ref tipTilt, ref tipTiltV, velTilt, 260f, 14f, dt);
                tip.rectTransform.anchoredPosition = tipPos;
                tip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tipTilt);
                tipPop = Mathf.Min(1f, tipPop + dt / 0.06f);
                tip.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, Ease.OutQuad(tipPop));
                // 흰 번쩍: 상자가 통째로 흰색, 글자 숨김 → 3~4프레임 뒤 글자가 찬다
                tipFlash = Mathf.Max(0f, tipFlash - dt / 0.12f);
                bool white = tipFlash > 0f;
                tip.color = white ? Color.white : new Color(0.05f, 0.1f, 0.08f, 0.97f);
                tipTitle.enabled = tipDesc.enabled = tipCost.enabled = !white;
                tipTitle.text = hover.Name;
                tipDesc.text = hover.Desc(hover.Level);
                bool avail = Available(hover);
                tipCost.text = hover.Root ? "" : hover.Maxed ? "" : !avail ? "<color=#888888>옆 노드를 먼저</color>" : (game.Money >= hover.NextCost ? $"<color=#8cff9a>${hover.NextCost:N0}</color>" : $"<color=#ff6666>${hover.NextCost:N0}</color>");
                tipCost.supportRichText = true;
            }
            else tip.gameObject.SetActive(false);
            AmbientDots(dt);
            Background(dt);
            Refresh();
        }

        /// <summary>배경이 산다: 두 겹이 다른 속도로 흐르고(시차), 먼 겹은 아주 천천히 밝아졌다 어두워지고, 희미한 점이 가끔 떠오른다</summary>
        void Background(float dt)
        {
            float t = Time.unscaledTime;
            float farTile = 64f * Px * 2f, nearTile = 32f * Px;      // 타일 한 칸 크기 (캔버스 px)
            bgFar.rectTransform.anchoredPosition = new Vector2(Mathf.Repeat(t * 4f, farTile), Mathf.Repeat(-t * 2.5f, farTile)) - new Vector2(farTile, farTile) * 0.5f;
            bgNear.rectTransform.anchoredPosition = new Vector2(Mathf.Repeat(-t * 9f, nearTile), Mathf.Repeat(t * 6f, nearTile)) - new Vector2(nearTile, nearTile) * 0.5f;
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 0.35f);
            bgFar.color = Color.Lerp(new Color(0.11f, 0.14f, 0.26f), new Color(0.14f, 0.17f, 0.32f), breathe);
            bgDotTimer -= dt;
            if (bgDotTimer <= 0f)
            {
                bgDotTimer = Random.Range(0.15f, 0.35f);
                var d = Img("BgDot", veil.transform, new Vector2(Random.Range(-980f, 980f), Random.Range(-600f, -300f)), Vector2.one * (Random.value < 0.3f ? 3f : 2f) * Px, "fx_dot", new Color(0.6f, 0.8f, 1f, 0f));
                d.transform.SetSiblingIndex(2);   // 배경 위, 세계 아래
                StartCoroutine(BgDotCo(d));
            }
        }

        IEnumerator BgDotCo(Image d)
        {
            var from = d.rectTransform.anchoredPosition; float life = Random.Range(5f, 9f), sway = Random.Range(10f, 30f), ph = Random.value * 6f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / life)
            {
                d.rectTransform.anchoredPosition = from + new Vector2(Mathf.Sin(t * 6f + ph) * sway, t * 900f);
                var c = d.color; c.a = Mathf.Min(t / 0.15f, 1f - t) * 0.35f; d.color = c;
                yield return null;
            }
            Destroy(d.gameObject);
        }

        IEnumerator CursorSnap()
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.05f) { cursor.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, Ease.OutQuad(t)); yield return null; }
            cursor.localScale = Vector3.one;
        }

        void AmbientDots(float dt)
        {
            dotTimer -= dt;
            if (dotTimer > 0f) return;
            dotTimer = 0.12f;
            var pool = new List<NodeView>();
            foreach (var v in views.Values) if (v.Visible && Available(v.Node) && v.Node.Level < 1) pool.Add(v);
            if (pool.Count == 0) return;
            var pick = pool[Random.Range(0, pool.Count)];
            var at = pick.Root.anchoredPosition + new Vector2(Random.Range(-1f, 1f), Random.Range(-0.6f, 0.6f)) * NodePx * Px * 0.9f;
            bool plus = Random.value < 0.3f;
            var dot = Img(plus ? "Plus" : "Dot", world, at, Vector2.one * (plus ? 5f : 2f) * Px, plus ? "fx_spark" : "fx_dot", Color.Lerp(pick.Node.Color, Color.white, 0.3f));
            dot.transform.SetSiblingIndex(pick.Root.GetSiblingIndex());
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

        /// <summary>누름: 1프레임에 납작 (Pressed). 살 수 있으면 바로 산다 → 흰 네모 + 길게 튕김</summary>
        void Press(SkillNode n)
        {
            var v = views[n.Id];
            v.Pressed = true; v.Sx = 1.15f; v.Sy = 0.8f; v.Vx = v.Vy = 0f;   // 들어가는 이징 없음: 즉시
            if (n.Root || n.Maxed || !Available(n) || game.Money < n.NextCost)
            {
                Sfx.Play(Sfx.Click(), 0.5f, 0.7f);
                StartCoroutine(Shake(v.Root));
                return;
            }
            var before = new HashSet<string>(); foreach (var o in nodes) if (ShouldShow(o)) before.Add(o.Id);
            game.BuyNode(n);
            buyStreak = Time.unscaledTime - lastBuy < 1f ? buyStreak + 1 : 0; lastBuy = Time.unscaledTime;
            Sfx.Play(Sfx.Tang(), 0.6f, 1.2f + Mathf.Min(buyStreak, 8) * 0.12f);
            moneyPop = 1f; moneyTilt = Random.Range(-10f, 10f);
            v.Flash = 1f;
            v.LvPop = 1f; v.LvTilt = Random.Range(-14f, 14f);
            worldPop = 1f; worldPopV = 0.5f;            // 화면 전체가 살짝 톡
            tipFlash = 1f; tipTilt = Random.Range(-10f, -6f); tipTiltV = 0f;   // 설명 상자도 흰색으로 번쩍, 기울어져 들어옴
            Fx.Shake(0.08f);
            StartCoroutine(Rebound(v));
            StartCoroutine(Puff(v));          // 흰 구름 → 점가루 → 반짝이
            StartCoroutine(Twinkle(v));
            foreach (var l in links)
            {
                if (l.A != n.Id && l.B != n.Id) continue;
                l.Flash = 1f;
                string otherId = l.A == n.Id ? l.B : l.A;
                var ov = views[otherId];
                bool reveal = !before.Contains(otherId) && ShouldShow(byId[otherId]);
                if (reveal) ov.Visible = true;   // 선은 Refresh에서 켜진다
                StartCoroutine(PulseAlong(n.Pos * Spacing, byId[otherId].Pos * Spacing, n.Color, reveal ? ov : null));
            }
        }

        /// <summary>놓임: 2프레임 뒤 원래보다 길게 (가로 0.85, 세로 1.3) 튕겼다가 용수철로 가라앉는다. 그때 흰 빗금 조각</summary>
        IEnumerator Rebound(NodeView v)
        {
            yield return null; yield return null;
            v.Pressed = false;
            v.Sx = 0.85f; v.Sy = 1.3f; v.Vx = 0f; v.Vy = 0f;
            for (int i = 0; i < 4; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                var s = Img("Slash", v.Root.parent, v.Root.anchoredPosition + new Vector2(side * NodePx * Px * 0.7f, (i < 2 ? 1f : -1f) * 10f), new Vector2(10f * Px, 2f * Px), "link", Color.white);
                s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, side * (i < 2 ? 35f : -35f));
                StartCoroutine(SlashCo(s, side));
            }
        }

        IEnumerator SlashCo(Image s, float side)
        {
            var from = s.rectTransform.anchoredPosition;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.14f)
            {
                s.rectTransform.anchoredPosition = from + Vector2.right * side * Ease.OutExpo(t) * 40f;
                s.rectTransform.localScale = new Vector3(1f + (1f - t), 1f, 1f);
                var c = s.color; c.a = 1f - t * t; s.color = c;
                yield return null;
            }
            Destroy(s.gameObject);
        }

        /// <summary>
        /// 흰 구름 (Shelldiver 2.20~2.43초): 울퉁불퉁한 흰 덩어리 7~8개가 노드를 덮고 6프레임쯤 꿈틀대다가 한 번에 꺼지며
        /// 그 자리에서 점가루가 터진다 (2.47~2.60초: 색색 점 수십 개가 노드 두세 개 반경으로 빽빽하게).
        /// </summary>
        IEnumerator Puff(NodeView v)
        {
            var puffs = new List<Image>();
            var home = v.Root.anchoredPosition;
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f * Mathf.Deg2Rad;
                var p = Img("Puff", v.Root.parent, home + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * NodePx * Px * 0.28f, Vector2.one * Random.Range(10f, 16f) * Px, "fx_puff", Color.white);
                p.transform.SetSiblingIndex(v.Root.GetSiblingIndex() + 1);   // 노드 위
                puffs.Add(p);
            }
            var core = Img("PuffCore", v.Root.parent, home, Vector2.one * 18f * Px, "fx_puff", Color.white);
            core.transform.SetSiblingIndex(v.Root.GetSiblingIndex() + 1);
            puffs.Add(core);
            float life = 0.2f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / life)
            {
                float grow = t < 0.3f ? Ease.OutQuad(t / 0.3f) : 1f;
                for (int i = 0; i < puffs.Count; i++)
                {
                    float wob = 1f + Mathf.Sin(Time.unscaledTime * 40f + i * 1.7f) * 0.12f;   // 꿈틀
                    puffs[i].rectTransform.localScale = Vector3.one * grow * wob;
                }
                yield return null;
            }
            foreach (var p in puffs) Destroy(p.gameObject);   // 한 번에 꺼진다
            Confetti(v, 56);
        }

        /// <summary>점가루: 색색 점이 빽빽하게 터져 0.25초 안에 노드 두세 개 반경까지, 그 뒤 성긴 몇 개만 남아 떠다닌다</summary>
        void Confetti(NodeView v, int count)
        {
            var c = v.Node.Color;
            var palette = new[] { c, c, Color.white, Color.Lerp(c, Color.white, 0.5f), SkillTree.Green, new Color(1f, 0.5f, 0.8f), SkillTree.Cyan, SkillTree.Yellow };
            for (int i = 0; i < count; i++)
            {
                bool big = i % 5 == 0;
                var d = Img("Confetti", v.Root.parent, v.Root.anchoredPosition, Vector2.one * (big ? 3f : 2f) * Px, "fx_dot", palette[i % palette.Length]);
                d.transform.SetSiblingIndex(v.Root.GetSiblingIndex());
                StartCoroutine(ConfettiCo(d, i < count / 6));   // 1/6은 오래 남는다
            }
        }

        IEnumerator ConfettiCo(Image d, bool linger)
        {
            var dir = Random.insideUnitCircle.normalized;
            float dist = Random.Range(50f, 230f), life = linger ? Random.Range(0.7f, 1.1f) : Random.Range(0.25f, 0.4f);
            var from = d.rectTransform.anchoredPosition;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / life)
            {
                float e = Ease.OutExpo(linger ? Mathf.Min(1f, t * 2.5f) : t);
                d.rectTransform.anchoredPosition = from + dir * dist * e + Vector2.down * (t * t * (linger ? 60f : 25f)) + (linger ? Vector2.right * Mathf.Sin(t * 12f) * 6f : Vector2.zero);
                if (t > 0.7f) { var c = d.color; c.a = 1f - (t - 0.7f) / 0.3f; d.color = c; }
                yield return null;
            }
            Destroy(d.gameObject);
        }

        /// <summary>흰 반짝이: 노드 둘레에 "+"와 점이 0.5초 동안 하나씩 깜빡 켜졌다 꺼진다 (2~3프레임씩)</summary>
        IEnumerator Twinkle(NodeView v)
        {
            float until = Time.unscaledTime + 0.55f;
            while (Time.unscaledTime < until)
            {
                int n = Random.Range(1, 3);
                for (int i = 0; i < n; i++)
                {
                    bool plus = Random.value < 0.6f;
                    var at = v.Root.anchoredPosition + Random.insideUnitCircle * NodePx * Px * Random.Range(0.5f, 0.95f);
                    var s = Img("Twinkle", v.Root.parent, at, Vector2.one * (plus ? 5f : 2f) * Px, plus ? "fx_spark" : "fx_dot", Color.white);
                    StartCoroutine(Blink(s, Random.Range(0.06f, 0.12f)));
                }
                yield return new WaitForSecondsRealtime(Random.Range(0.03f, 0.08f));
            }
        }

        IEnumerator Blink(Image s, float life)
        {
            s.rectTransform.localScale = Vector3.one * 1.4f;
            yield return null;
            s.rectTransform.localScale = Vector3.one;
            yield return new WaitForSecondsRealtime(life);
            Destroy(s.gameObject);
        }

        /// <summary>선을 따라 빛이 달려간다 (0.12초). 닿으면 그 노드가 드러난다: 1프레임에 쩍 (작은 점 → 큰 흰 네모 → 용수철로 자리)</summary>
        IEnumerator PulseAlong(Vector2 a, Vector2 b, Color c, NodeView reveal)
        {
            var d = b - a;
            var p = Img("Pulse", world, a, new Vector2(10f * Px, 2f * Px), "link", Color.white);
            p.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            if (reveal != null)
            {
                // 기대: 점만 보인다 (작게), 선은 아직 희미
                reveal.Root.gameObject.SetActive(true);
                reveal.Sx = reveal.Sy = 0.15f; reveal.Vx = reveal.Vy = 0f;
            }
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.12f)
            {
                p.rectTransform.anchoredPosition = a + d * t;
                if (reveal != null) { reveal.Sx = reveal.Sy = 0.15f; reveal.Vx = reveal.Vy = 0f; }   // 올 때까지 붙잡아 둔다
                yield return null;
            }
            Destroy(p.gameObject);
            if (reveal != null)
            {
                reveal.Sx = reveal.Sy = 1.35f; reveal.Vx = reveal.Vy = 0f;   // 1프레임에 쩍, 용수철이 가라앉힌다
                reveal.Flash = 1f; reveal.Wake = 1f;
                Sfx.Play(Sfx.Click(), 0.4f, 2.2f);
                Confetti(reveal, 18);
                StartCoroutine(Twinkle(reveal));
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
