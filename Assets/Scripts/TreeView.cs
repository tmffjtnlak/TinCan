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
        Image tip, tipRule; Text tipTitle, tipDesc, tipCost; float tipTilt, tipTiltV, tipS = 1f, tipSV, TipScale = 2f; Vector2 tipPos;
        static readonly System.Text.RegularExpressions.Regex NumToken = new System.Text.RegularExpressions.Regex(@"[+\-×x]?\d+(?:[.,]\d+)?(?:%|배|번|초|개|발)?");
        Text money; float moneyPop, moneyTilt; long moneyShown;
        RectTransform cursor; Image[] corners;
        Vector2 curPos, curVel; float curScale = 1f, curScaleV, curAlpha; bool curShown;   // 꺾쇠: 노드 사이를 용수철로 미끄러진다
        SkillNode hover;
        float zoom = 1f, zoomTarget = 1f, worldPop = 1f, worldPopV;
        Vector2 dragLast; bool dragging;
        int buyStreak; float lastBuy, holdTimer;
        float dotTimer;
        // 핵심 노드 충전 (사용자 10-12): 꾹 누르면 게이지가 차고 화면이 그 노드로 부드럽게 줌인, 쭈욱 차오르는 소리. 다 차면 해방
        const float ChargeSeconds = 2.4f;   // 석양이 지는 템포 (사용자 10-12: 더 길게, 더 느리게)
        SkillNode charging; float charge, tickTimer;
        Vector2 panBefore; float zoomBefore = 1f; bool panSaved; Vector2? panTarget;
        // 해방 연출 강화 (사용자 10-12, 캐서디 궁 느낌): 꾹 누르면 아래에서 총이 올라오고, 회전초가 게이지처럼 굴러가고,
        // 찰수록 화면이 떨리고, 다 차면 총이 이웃을 따다다당 쏜 뒤 한 바퀴 돌고 먼지만 남기고 사라진다
        Image gunImg, tumble; RectTransform gunRt;
        float gunY = -700f, gunYV, gunRot, gunRotV, gunKick, gunKickV, gunTarget = -700f, gunSpin, gunSpinV, gunAlpha = 1f, gunScale = 1f, gunScaleV, gunX;
        float camRot, camRotV, tumbleX = -760f;
        bool gunUp;

        class NodeView
        {
            public SkillNode Node; public RectTransform Root;
            public Image Frame, Fill, Bevel, Icon; public Text Glyph, Lv;
            public float Sx = 1f, Sy = 1f, Vx, Vy;      // 용수철 크기 (가로·세로 따로 = 찌그러짐)
            public float Rot, RotV;                      // 용수철 회전 (살 때·드러날 때 기울었다 흔들리며 돌아온다)
            public float Flash, Wake, LvPop, LvTilt;
            public float GlintTimer = -1f, Glint;        // 산 노드의 빛 반사 (주기적으로 한 번씩)
            public Image Gauge;                           // 핵심 노드: 꾹 누르면 차는 링
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
            // 꺾쇠는 45° 돌려 쓰므로 축 거리 off → 꼭짓점 거리 off·√2. 마름모 꼭짓점(반폭 15칸)보다 2칸만 바깥 (사용자 10-12: 한두 픽셀만)
            float off = (NodePx * 0.5f + 2f) * Px / 1.4142f;
            // 꺾쇠 그림은 왼쪽 위 ㄱ자. 왼쪽은 그대로, 오른쪽은 가로 뒤집기, 아래는 세로 뒤집기 → [ ] 모양. 마름모라 45° 돌려 꼭짓점을 감싼다
            for (int i = 0; i < 4; i++)
            {
                bool left = i % 2 == 0, top = i < 2;
                corners[i] = Img("Corner", cursor, new Vector2((left ? -1f : 1f) * off, (top ? 1f : -1f) * off), Vector2.one * 7f * Px, "cursor_corner", Color.white);
                corners[i].rectTransform.localScale = new Vector3(left ? 1f : -1f, top ? 1f : -1f, 1f);
            }
            cursor.gameObject.SetActive(false);

            // 설명 상자 (사용자 10-12 그림 tip_panel.png 262×117, 양옆 소용돌이 장식): 그림 픽셀 ×2 그대로, 늘리지 않는다.
            // 구성: 이름(볼드) / ──── 구분선 / 내용(수치만 볼드+반짝) / 값. 글자는 흰색
            var tipSprite = TreeArt.Get("tip_panel");
            TipScale = tipSprite != null && tipSprite.rect.width > 100f ? 2f : Px;   // 사용자 그림이면 ×2, 임시 그림이면 도트 칸
            var tipSize = tipSprite != null ? new Vector2(tipSprite.rect.width, tipSprite.rect.height) * TipScale : new Vector2(524f, 234f);
            tip = Img("Tip", veil.transform, Vector2.zero, tipSize, "tip_panel", Color.white);
            if (tipSprite != null && tipSprite.rect.width <= 100f) { tip.type = Image.Type.Sliced; tip.pixelsPerUnitMultiplier = Mult; tip.color = new Color(0.13f, 0.125f, 0.2f, 0.97f); }
            float inner = tipSize.x - 60f * TipScale;          // 장식 안쪽
            tipTitle = Txt("TipTitle", tip.transform, new Vector2(0f, tipSize.y * 0.5f - 38f), new Vector2(inner, 40f), 26, TextAnchor.MiddleCenter, Color.white);
            tipRule = Img("TipRule", tip.transform, new Vector2(0f, tipSize.y * 0.5f - 62f), new Vector2(inner * 0.8f, 3f), "link_dim", new Color(1f, 1f, 1f, 0.5f));
            tipRule.type = Image.Type.Tiled; tipRule.pixelsPerUnitMultiplier = 100f / (TreeArt.Ppu * 3f);
            tipDesc = Txt("TipDesc", tip.transform, new Vector2(0f, -4f), new Vector2(inner, 70f), 21, TextAnchor.MiddleCenter, Color.white);
            tipCost = Txt("TipCost", tip.transform, new Vector2(0f, -tipSize.y * 0.5f + 34f), new Vector2(inner, 34f), 24, TextAnchor.MiddleCenter, SkillTree.Yellow);
            tip.gameObject.SetActive(false);

            money = Txt("Money", veil.transform, new Vector2(760f, 455f), new Vector2(360f, 60f), 36, TextAnchor.MiddleRight, SkillTree.Yellow);
            var hint = Txt("Hint", veil.transform, new Vector2(0f, -500f), new Vector2(1400f, 40f), 20, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
            hint.text = "누르면 산다 (누르고 있으면 연달아)   ·   핵심 노드는 꾹 눌러 해방   ·   휠 = 확대   ·   드래그·방향키 = 이동   ·   Tab = 닫는다";

            // 해방용 총 (사용자 리볼버 그림, 피벗 = 방아쇠울) — 평소엔 화면 아래에 숨어 있다
            var gunSprite = GunView.LoadArt("gun_revolver_side");
            gunImg = Img("UnlockGun", veil.transform, new Vector2(0f, gunY), gunSprite != null ? new Vector2(gunSprite.rect.width, gunSprite.rect.height) * 3f : new Vector2(300f, 190f), null, Color.white);
            if (gunSprite != null) { gunImg.sprite = gunSprite; gunImg.rectTransform.pivot = new Vector2(40f / 101f, 1f - 38f / 64f); }
            gunRt = gunImg.rectTransform;
            gunImg.gameObject.SetActive(false);
            tumble = Img("Tumbleweed", veil.transform, new Vector2(tumbleX, -470f), Vector2.one * 16f * Px, "tumbleweed", new Color(0.75f, 0.62f, 0.35f));
            tumble.gameObject.SetActive(false);
            // 리볼버 실루엣이 화면에 다 들어오게: 조금 축소하고 총열 쪽으로 중심을 옮긴다
            zoom = zoomTarget = 0.72f;
            world.anchoredPosition = new Vector2(0f, 1.3f * Spacing * zoom);
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
            v.Lv = Txt("Lv", root.transform, new Vector2(0f, -size * 0.74f), new Vector2(160f, 30f), 24, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.8f));
            if (n.Key)
            {
                // 게이지: 노드를 감싸는 링이 시계 방향으로 찬다
                v.Gauge = Img("Gauge", root.transform, Vector2.zero, Vector2.one * 46f * Px, "fx_ring", Color.white);
                v.Gauge.type = Image.Type.Filled; v.Gauge.fillMethod = Image.FillMethod.Radial360; v.Gauge.fillOrigin = (int)Image.Origin360.Top; v.Gauge.fillClockwise = true;
                v.Gauge.fillAmount = 0f; v.Gauge.enabled = false;
                v.Gauge.transform.SetAsFirstSibling();
            }
            return v;
        }

        /// <summary>계열 이름 (색으로 판단): reload / shelf / money / auto / time / root</summary>
        static string Family(SkillNode n)
        {
            if (n.Root || n.Key) return "root";   // 핵심 노드는 별
            if (n.Color == SkillTree.Red) return "reload";
            if (n.Color == SkillTree.Cyan) return "shelf";
            if (n.Color == SkillTree.Yellow) return "money";
            if (n.Color == SkillTree.Green) return "auto";
            if (n.Color == SkillTree.Purple) return "time";
            return "root";
        }

        /// <summary>핵심 노드를 지금 채울 수 있나: 안 샀고, 뿌리거나 이웃이 열려 있고, 돈이 되면</summary>
        bool CanCharge(SkillNode n) => n.Key && n.Level < 1 && (n.Root || Available(n)) && game.Money >= n.NextCost;

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

        /// <summary>열기: 0.1초 페이드 + 전체가 용수철로 톡. 노드들은 흰 마름모로 떴다가 뿌리에서 가까운 순으로 제 색을 입는다 (Shelldiver 0.90~1.00초)</summary>
        IEnumerator OpenAnim()
        {
            Sfx.Play(Sfx.Click(), 0.4f, 1.3f);
            worldPop = 0.94f; worldPopV = 1.2f;
            var root = nodes.Find(n => n.Root); Vector2 origin = root != null ? root.Pos : Vector2.zero;
            foreach (var v in views.Values)
            {
                if (!v.Visible) continue;
                v.Flash = 1.2f + Vector2.Distance(v.Node.Pos, origin) * 0.12f;   // 멀수록 조금 더 오래 희다 = 순서대로 물든다
                v.Rot = Random.Range(-8f, 8f); v.RotV = 0f;
            }
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
                bool avail = Available(n) || (n.Key && n.Level < 1 && (n.Root || Available(n))), owned = n.Level >= 1, hot = hover == n;
                v.Flash = Mathf.Max(0f, v.Flash - dt / 0.22f);          // 흰 네모: 4프레임 꽉 찬 흰색 → 3프레임 사라짐
                v.Wake = Mathf.Max(0f, v.Wake - dt / 0.3f);
                v.LvPop = Mathf.Max(0f, v.LvPop - dt / 0.25f);
                var frame = owned ? n.Color : (avail ? Color.Lerp(n.Color, Color.white, 0.25f) : new Color(n.Color.r, n.Color.g, n.Color.b, 0.3f));
                var fill = n.Maxed ? Color.Lerp(n.Color, Color.black, 0.45f) : (owned ? Color.Lerp(n.Color, new Color(0.08f, 0.1f, 0.2f), 0.7f) : new Color(0.08f, 0.1f, 0.2f));
                // 꽉 찬 흰색(0.4 위) → 섞이며 돌아옴(0.4 아래). 드러날 땐 1.6에서 시작해 더 오래 희다
                float wf = v.Flash > 0.4f ? 1f : v.Flash / 0.4f;
                bool white = v.Flash > 0.4f;
                if (v.Flash > 0f) { frame = Color.Lerp(frame, Color.white, wf); fill = Color.Lerp(fill, Color.white, wf); }
                else if (v.Wake > 0f) { frame = Color.Lerp(frame, Color.white, v.Wake); fill = Color.Lerp(fill, Color.white, v.Wake * 0.6f); }
                // 산 노드: 빛 반사 (사용자 10-12: 안 산 노드는 먼지, 산 노드는 반짝). 2~4초마다 테두리가 흰빛으로 쓸리고 꼭짓점에 별
                if (owned && !n.Root)
                {
                    if (v.GlintTimer < 0f) v.GlintTimer = Random.Range(0.5f, 3f);
                    v.GlintTimer -= dt;
                    if (v.GlintTimer <= 0f) { v.GlintTimer = Random.Range(2f, 4.5f); v.Glint = 1f; StartCoroutine(GlintCo(v)); }
                    v.Glint = Mathf.Max(0f, v.Glint - dt / 0.3f);
                    frame = Color.Lerp(frame, Color.white, Ease.Punch(v.Glint) * 0.7f);
                    v.Bevel.color = new Color(1f, 1f, 1f, 0.12f + Ease.Punch(v.Glint) * 0.5f);
                }
                v.Frame.color = frame; v.Fill.color = fill;
                if (!(owned && !n.Root)) v.Bevel.color = new Color(1f, 1f, 1f, white ? 0f : 0.12f);
                // 아이콘: 살 수 있거나 샀으면 밝게, 아니면 어둡게 (사용자 10-12). 흰 네모일 땐 아이콘도 묻힌다
                var ink = white ? Color.white : (owned || avail ? Color.white : new Color(0.38f, 0.4f, 0.5f));
                if (v.Glyph != null) v.Glyph.color = ink; if (v.Icon != null) v.Icon.color = ink;
                v.Lv.text = "";   // 노드 밑 값 글자는 뺐다 (사용자 10-12): 값은 설명 상자에, 상태는 색으로
                if (v.Gauge != null) v.Gauge.enabled = false;   // 게이지는 링 대신 회전초가 굴러가는 걸로 (사용자 10-12)
                v.Lv.color = n.Maxed ? SkillTree.Green : avail ? (game.Money >= n.NextCost ? SkillTree.Green : new Color(1f, 0.45f, 0.4f)) : new Color(1f, 1f, 1f, 0.45f);
                // 값 글자: 바뀔 때 기울며 커졌다 돌아온다 (Click the Button 숫자)
                float lp = Ease.Punch(v.LvPop);
                v.Lv.transform.localScale = Vector3.one * (1f + lp * 0.5f);
                v.Lv.transform.localRotation = Quaternion.Euler(0f, 0f, v.LvTilt * lp);
                // 크기: 가로·세로 용수철. 올리면 1.12, 누르면 납작, 놓으면 길게 튕김
                float tx = v.Pressed ? 1.15f : (hot ? 1.12f : 1f), ty = v.Pressed ? 0.8f : (hot ? 1.12f : 1f);
                Ease.Spring(ref v.Sx, ref v.Vx, tx, 420f, 16f, dt);
                Ease.Spring(ref v.Sy, ref v.Vy, ty, 420f, 16f, dt);
                // 회전: 감쇠가 약해서 서너 번 좌우로 흔들리며 돌아온다 (Click the Button 3.37~3.67초, Shelldiver 등장 0.78~0.95초)
                Ease.Spring(ref v.Rot, ref v.RotV, 0f, 320f, 9f, dt);
                v.Root.localScale = new Vector3(v.Sx + v.Wake * 0.1f, v.Sy + v.Wake * 0.1f, 1f);
                v.Root.localRotation = Quaternion.Euler(0f, 0f, v.Rot);
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
            // 전체 톡 (열 때·살 때) + 화면 회전 떨림 (충전·사격)
            Ease.Spring(ref worldPop, ref worldPopV, 1f, 500f, 14f, dt);
            world.localScale = Vector3.one * zoom * worldPop;
            float jitter = charging != null ? charge * charge * Random.Range(-2.5f, 2.5f) : 0f;
            Ease.Spring(ref camRot, ref camRotV, 0f, 300f, 9f, dt);
            world.localRotation = Quaternion.Euler(0f, 0f, camRot + jitter);
            // 총: 올라옴/내려감, 반동, 한 바퀴, 사라짐
            Ease.Spring(ref gunY, ref gunYV, gunTarget, 260f, 14f, dt);
            Ease.Spring(ref gunKick, ref gunKickV, 0f, 500f, 16f, dt);
            Ease.Spring(ref gunSpin, ref gunSpinV, 0f, 220f, 11f, dt);
            Ease.Spring(ref gunScale, ref gunScaleV, 1f, 400f, 14f, dt);
            bool gunShow = gunY > -650f || gunUp;
            gunImg.gameObject.SetActive(gunShow);
            if (gunShow)
            {
                float aimDeg = gunRot + gunSpin;
                var dir = new Vector2(Mathf.Cos(gunRot * Mathf.Deg2Rad), Mathf.Sin(gunRot * Mathf.Deg2Rad));
                gunRt.anchoredPosition = new Vector2(gunX, gunY) - dir * gunKick * 40f;
                bool left = Mathf.Cos(gunRot * Mathf.Deg2Rad) < 0f;
                gunRt.localRotation = Quaternion.Euler(0f, 0f, aimDeg + (charging != null ? charge * charge * Random.Range(-3f, 3f) : 0f));
                gunRt.localScale = new Vector3(gunScale, left ? -gunScale : gunScale, 1f);
                gunImg.color = new Color(1f, 1f, 1f, gunAlpha);
            }
        }

        void Update()
        {
            if (game == null || !veil.gameObject.activeSelf) return;
            var mouse = Mouse.current; var kb = Keyboard.current;
            float dt = Time.unscaledDeltaTime;
            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(wheel) > 0.01f && charging == null) { zoomTarget = Mathf.Clamp(zoomTarget * (wheel > 0 ? 1.15f : 0.87f), 0.5f, 1.8f); panTarget = null; }
            }
            if (kb != null && charging == null && (kb.upArrowKey.isPressed || kb.downArrowKey.isPressed || kb.leftArrowKey.isPressed || kb.rightArrowKey.isPressed)) panTarget = null;
            zoom = Mathf.Lerp(zoom, zoomTarget, 1f - Mathf.Exp(-dt * (charging != null || panTarget.HasValue ? 3.5f : 20f)));
            if (panTarget.HasValue)
            {
                world.anchoredPosition = Vector2.Lerp(world.anchoredPosition, panTarget.Value, 1f - Mathf.Exp(-dt * 3.5f));
                if (charging == null && Vector2.Distance(world.anchoredPosition, panTarget.Value) < 1f) panTarget = null;
            }
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
                Sfx.Play(Sfx.Click(), 0.2f, 2.6f);
                if (!curShown) { curPos = hover.Pos * Spacing; curScale = 1.6f; }   // 처음엔 그 자리에서 조여들고, 그다음부턴 미끄러져 간다
                curScaleV = -6f; curShown = true;
                // 설명 상자가 바뀔 때: 작고 기울어진 채 들어와서 용수철로 커지며 풀린다 (흰 번쩍은 뺐다. 사용자 10-12)
                tipTilt = Random.Range(-13f, -9f); tipTiltV = 0f;
                tipS = prev == null ? 0.55f : 0.8f; tipSV = 0f;
                if (prev == null) tipPos = canvasPt + new Vector2(260f, 70f);
            }
            if (mouse != null)
            {
                bool down = mouse.leftButton.isPressed;
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    if (hover != null && hover.Key) { if (CanCharge(hover)) BeginCharge(hover); else { Sfx.Play(Sfx.Click(), 0.5f, 0.7f); StartCoroutine(Shake(views[hover.Id].Root)); } }
                    else if (hover != null) { Press(hover); holdTimer = 0.28f; }
                    else { dragging = true; dragLast = canvasPt; panTarget = null; }   // 드래그하면 카메라 자동 이동은 손을 뗀다
                }
                // 핵심 노드 충전 중
                if (charging != null)
                {
                    // 누르고만 있으면 계속 찬다 (줌인으로 노드가 커서 밑에서 벗어나도 안 끊긴다. 사용자 10-12)
                    hover = charging;
                    if (down) ChargeTick(dt);
                    else EndCharge(false);
                }
                // 누르고 있으면 연달아 산다 (Shelldiver: 초당 4번쯤). 핵심 노드는 제외
                else if (down && hover != null && !hover.Key && !dragging)
                {
                    holdTimer -= dt;
                    if (holdTimer <= 0f) { Press(hover); holdTimer = 0.25f; }
                }
                foreach (var v in views.Values) if (v.Pressed && (!down || hover != v.Node)) v.Pressed = false;
                if (dragging && down) { world.anchoredPosition += canvasPt - dragLast; dragLast = canvasPt; }
                if (mouse.leftButton.wasReleasedThisFrame) dragging = false;
            }
            // 꺾쇠: 노드를 따라 용수철로 이동, 노드 회전 + 45°(마름모 꼭짓점), 없으면 서서히 사라진다
            if (hover != null) curPos = Vector2.Lerp(curPos, hover.Pos * Spacing, 1f - Mathf.Exp(-dt * 22f));
            Ease.Spring(ref curScale, ref curScaleV, 1f, 600f, 18f, dt);
            curAlpha = Mathf.MoveTowards(curAlpha, hover != null ? 1f : 0f, dt / (hover != null ? 0.05f : 0.15f));
            if (hover == null && curAlpha <= 0f) curShown = false;
            cursor.gameObject.SetActive(curAlpha > 0f);
            cursor.anchoredPosition = curPos;
            cursor.localScale = Vector3.one * curScale;
            cursor.localRotation = Quaternion.Euler(0f, 0f, 45f + (hover != null ? views[hover.Id].Rot : 0f));
            foreach (var c in corners) c.color = new Color(1f, 1f, 1f, curAlpha);
            bool secret = hover != null && hover.Key && hover.Level < 1;   // 해방 전 핵심 노드는 정보를 안 보여 준다 (사용자 10-12)
            if (hover != null && !secret)
            {
                tip.gameObject.SetActive(true);
                // 마우스를 조금 늦게 따라오고, 움직이는 속도만큼 기운다
                var want = canvasPt + new Vector2(260f, 70f);
                var before = tipPos;
                tipPos = Vector2.Lerp(tipPos, want, 1f - Mathf.Exp(-dt * 16f));
                float velTilt = Mathf.Clamp((tipPos.x - before.x) / Mathf.Max(dt, 0.001f) * -0.004f, -6f, 6f);
                Ease.Spring(ref tipTilt, ref tipTiltV, velTilt, 520f, 20f, dt);   // 4프레임 안에 풀린다 (Shelldiver 1.13~1.20초)
                tip.rectTransform.anchoredPosition = tipPos;
                tip.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tipTilt);
                Ease.Spring(ref tipS, ref tipSV, 1f, 600f, 17f, dt);   // 살짝 넘쳤다 자리 잡는 크기
                tip.rectTransform.localScale = Vector3.one * tipS;
                tipTitle.text = $"<b>{hover.Name}</b>";
                // 수치만 볼드 + 반짝 (흰 → 금빛 → 흰, 가끔 더 환하게)
                float sh = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f);
                float spark = Mathf.PerlinNoise(Time.unscaledTime * 3f, 0.5f) > 0.8f ? 1f : 0f;
                var shine = Color.Lerp(Color.Lerp(Color.white, SkillTree.Yellow, sh), Color.white, spark);
                string hex = ColorUtility.ToHtmlStringRGB(shine);
                tipDesc.text = NumToken.Replace(hover.Desc(hover.Level), m => $"<b><color=#{hex}>{m.Value}</color></b>");
                bool avail = Available(hover);
                tipCost.text = hover.Key ? (hover.Level >= 1 ? "<color=#8cff9a>해방됨</color>" : CanCharge(hover) ? "<color=#ffd966>꾹 눌러 해방</color>" : "<color=#888888>옆 노드를 먼저</color>")
                    : hover.Maxed ? "<color=#8cff9a>구매 완료</color>" : !avail ? "<color=#888888>옆 노드를 먼저</color>" : (game.Money >= hover.NextCost ? $"<color=#8cff9a>${hover.NextCost:N0}</color>" : $"<color=#ff6666>${hover.NextCost:N0}</color>");
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

        // ---------- 핵심 노드: 꾹 눌러 해방 ----------
        /// <summary>노드의 캔버스 좌표 (총이 겨눌 곳)</summary>
        Vector2 NodeCanvas(SkillNode n) => n.Pos * Spacing * zoom * worldPop + world.anchoredPosition;

        void BeginCharge(SkillNode n)
        {
            charging = n; charge = 0f; tickTimer = 0f;
            if (!panSaved) { panBefore = world.anchoredPosition; zoomBefore = zoomTarget; panSaved = true; }
            zoomTarget = Mathf.Max(zoomTarget, 1.6f);
            panTarget = -n.Pos * Spacing * zoomTarget + new Vector2(0f, 60f);   // 노드가 가운데 조금 위로 (아래는 총 자리)
            views[n.Id].Sx = 1.1f; views[n.Id].Sy = 0.9f;
            // 총이 아래에서 올라온다 (캐서디 궁), 회전초가 왼쪽 끝에서 출발
            // 총은 아래쪽 어딘가에서 (정중앙은 피한다. 사용자 10-12)
            gunX = Random.Range(-420f, 420f); gunTarget = Random.Range(-380f, -300f);
            gunUp = true; gunYV = 600f; gunAlpha = 1f; gunScale = 1f; gunSpin = 0f;
            tumble.gameObject.SetActive(true); tumbleX = -760f;
            Sfx.Play(Sfx.Reload(), 0.7f, 0.8f);
        }

        void ChargeTick(float dt)
        {
            charge = Mathf.Min(1f, charge + dt / ChargeSeconds);
            var v = views[charging.Id];
            // 찰수록 떨리고, 커지고, 점이 빨려 들어온다
            v.Root.anchoredPosition = charging.Pos * Spacing + Random.insideUnitCircle * charge * charge * 3f * Px;
            v.Sx = v.Sy = 1f + charge * 0.25f; v.Vx = v.Vy = 0f;
            // 총이 노드를 겨눈다. 회전초가 게이지만큼 굴러간다 (먼지 남기며)
            var aim = NodeCanvas(charging) - new Vector2(gunX, gunY);
            gunRot = Mathf.LerpAngle(gunRot, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg, 1f - Mathf.Exp(-dt * 6f));
            float tx = Mathf.Lerp(-760f, 760f, charge);
            tumble.rectTransform.anchoredPosition = new Vector2(tx, -470f + Mathf.Abs(Mathf.Sin(charge * 40f)) * 10f);
            tumble.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -charge * 1400f);
            if (tx - tumbleX > 24f) { tumbleX = tx; StartCoroutine(DustCo(new Vector2(tx - 20f, -485f), new Color(0.8f, 0.7f, 0.5f, 0.6f))); }
            tickTimer -= dt;
            if (tickTimer <= 0f)
            {
                tickTimer = Mathf.Lerp(0.16f, 0.05f, charge);                   // 점점 촘촘하게 (느린 템포)
                Sfx.Play(Sfx.Click(), 0.3f + charge * 0.4f, 0.7f + charge * 2.0f); // 점점 높게 = 쭈우욱
                var dir = Random.insideUnitCircle.normalized;
                var d = Img("Suck", world, v.Root.anchoredPosition + dir * NodePx * Px * Random.Range(1.2f, 2.2f), Vector2.one * 2f * Px, "fx_dot", Color.Lerp(Color.white, SkillTree.Yellow, charge));
                StartCoroutine(SuckCo(d, v));
            }
            if (charge >= 1f) EndCharge(true);
        }

        IEnumerator SuckCo(Image d, NodeView v)
        {
            var from = d.rectTransform.anchoredPosition;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.18f)
            {
                d.rectTransform.anchoredPosition = Vector2.Lerp(from, v.Root.anchoredPosition, Ease.InQuad(t));
                yield return null;
            }
            Destroy(d.gameObject);
        }

        void EndCharge(bool done)
        {
            var n = charging; charging = null;
            var v = views[n.Id];
            v.Root.anchoredPosition = n.Pos * Spacing;
            if (!done)
            {
                charge = 0f;
                Sfx.Play(Sfx.Whoosh(), 0.3f, 1.6f);
                zoomTarget = zoomBefore; panTarget = panBefore; panSaved = false;
                gunUp = false; gunTarget = -700f; tumble.gameObject.SetActive(false);   // 총이 내려간다
                return;
            }
            charge = 0f;
            StartCoroutine(Unlock(n));
        }

        /// <summary>먼지 한 점: 회색 뭉게가 커지며 옅어진다</summary>
        IEnumerator DustCo(Vector2 at, Color c)
        {
            var d = Img("Dust", veil.transform, at, Vector2.one * Random.Range(6f, 10f) * Px, "fx_puff", c);
            d.transform.SetSiblingIndex(3);
            var from = d.rectTransform.anchoredPosition; float drift = Random.Range(-20f, 20f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / Random.Range(0.5f, 0.9f))
            {
                d.rectTransform.anchoredPosition = from + new Vector2(drift * t, 30f * t);
                d.rectTransform.localScale = Vector3.one * (0.6f + t * 0.9f);
                d.color = new Color(c.r, c.g, c.b, c.a * (1f - t));
                yield return null;
            }
            Destroy(d.gameObject);
        }

        /// <summary>
        /// 해방 (캐서디 궁): 노드가 흰색으로 터지고 → 아래 총이 이웃을 **따다다다당** 쏜다 (쏠 때마다 줌이 살짝 풀리고 화면이 돈다)
        /// → 총이 한 바퀴 돌고 → 트윈으로 사라지며 먼지 잔상만 → 회전초 퇴장 → 카메라 복귀
        /// </summary>
        IEnumerator Unlock(SkillNode n)
        {
            var v = views[n.Id];
            game.BuyNode(n);
            v.Flash = 1.6f; v.Sx = 1.5f; v.Sy = 1.5f; v.Vx = v.Vy = 0f; v.Rot = Random.Range(-20f, 20f);
            worldPop = 1.04f; worldPopV = 0f; camRotV = 40f;
            Fx.Shake(0.5f);
            Sfx.Play(Sfx.Bang(), 0.9f, 0.7f);
            Sfx.Play(Sfx.Tang(), 0.7f, 0.5f);
            StartCoroutine(Puff(v));
            StartCoroutine(RingCo(Img("Ring", v.Root.parent, v.Root.anchoredPosition, Vector2.one * 16f * Px, "fx_ring", Color.white), 0.7f));
            tumble.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(0.7f);   // 정오의 정적 — 한 박자 쉰다
            // 줌인이 끝났으니 바로 줌아웃하며 쏜다 (사용자 10-12). 쏘는 건 0.1초 간격
            zoomTarget = zoomBefore; panTarget = panBefore;
            var targets = new List<string>(nb[n.Id]);
            targets.Sort((a, b) => Mathf.Atan2(byId[a].Pos.y - n.Pos.y, byId[a].Pos.x - n.Pos.x).CompareTo(Mathf.Atan2(byId[b].Pos.y - n.Pos.y, byId[b].Pos.x - n.Pos.x)));
            int shot = 0;
            foreach (var id in targets)
            {
                var o = byId[id]; var ov = views[id];
                if (ov.Visible && o.Level >= 1) continue;
                // 총이 그 노드를 겨눈다 (즉시), 반동, 줌이 살짝 풀리며 화면이 돈다
                var aim = NodeCanvas(o) - new Vector2(gunX, gunY);
                gunRot = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
                gunKick = 1f; gunKickV = 0f;
                camRotV += (shot % 2 == 0 ? 1f : -1f) * 90f;
                StartCoroutine(ShootReveal(o));
                shot++;
                yield return new WaitForSecondsRealtime(0.1f);
            }
            yield return new WaitForSecondsRealtime(0.5f);
            // 한 바퀴 천천히 돌리고 (총 돌리기)
            Sfx.Play(Sfx.Twirl(), 0.6f, 0.9f);
            gunSpin = 360f; gunSpinV = 0f;
            yield return new WaitForSecondsRealtime(0.8f);
            // 트윈으로 사라진다: 살짝 커졌다 작아지며 투명, 그 자리에 먼지가 천천히
            gunScale = 1.2f; gunScaleV = 0f;
            var gunPos = gunRt.anchoredPosition;
            for (int i = 0; i < 9; i++) StartCoroutine(DustCo(gunPos + Random.insideUnitCircle * 70f, new Color(0.85f, 0.8f, 0.7f, 0.7f)));
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.5f) { gunAlpha = 1f - t; gunScale = Mathf.Lerp(1.2f, 0.5f, Ease.InQuad(t)); yield return null; }
            yield return new WaitForSecondsRealtime(0.3f);
            gunUp = false; gunTarget = -700f; gunY = -700f; gunAlpha = 1f; gunScale = 1f;
            zoomTarget = zoomBefore; panTarget = panBefore; panSaved = false;   // 카메라 돌아온다
        }

        /// <summary>아래 총에서 노드로: 총구 섬광 → 궤적 0.06초 → 명중 (깡통 맞은 것처럼 튀며 돈다)</summary>
        IEnumerator ShootReveal(SkillNode to)
        {
            // 총구 (캔버스 좌표): 피벗에서 총열 끝까지
            var dir = new Vector2(Mathf.Cos(gunRot * Mathf.Deg2Rad), Mathf.Sin(gunRot * Mathf.Deg2Rad));
            Vector2 muzzle = new Vector2(gunX, gunY) + dir * 190f;
            Vector2 target = NodeCanvas(to);
            var d = target - muzzle; float deg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var flash = Img("Muzzle", veil.transform, muzzle, Vector2.one * 7f * Px, "fx_puff", Color.white);
            Sfx.Play(Sfx.Bang(), 0.7f, 1.2f + Random.Range(-0.1f, 0.15f));
            Fx.Shake(0.18f);
            var tracer = Img("Tracer", veil.transform, muzzle, new Vector2(d.magnitude * 0.3f, 2f * Px), "link", Color.white);
            tracer.rectTransform.pivot = new Vector2(0f, 0.5f);
            tracer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, deg);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.06f)
            {
                tracer.rectTransform.anchoredPosition = muzzle + d * t * 0.7f;
                flash.rectTransform.localScale = Vector3.one * (1.3f - t);
                yield return null;
            }
            Destroy(tracer.gameObject); Destroy(flash.gameObject);
            // 명중
            // 명중: 불꽃 + 드러남 (깡통 맞은 것처럼 튀며 돈다)
            var ov = views[to.Id];
            ov.Visible = true; ov.Root.gameObject.SetActive(true);
            ov.Sx = 1.35f; ov.Sy = 0.75f; ov.Vx = ov.Vy = 0f;
            ov.Rot = Random.Range(25f, 40f) * (Random.value < 0.5f ? -1f : 1f); ov.RotV = 0f;
            ov.Flash = 1.4f; ov.Wake = 1f;
            Sfx.Play(Sfx.Tang(), 0.7f, 1.1f + Random.Range(-0.1f, 0.2f));
            Fx.Shake(0.2f);
            for (int i = 0; i < 8; i++) StartCoroutine(ShardCo(ov, deg + 180f + Random.Range(-50f, 50f)));
            Confetti(ov, 16);
            StartCoroutine(CornerDots(ov));
            StartCoroutine(Twinkle(ov));
            foreach (var l in links) if (l.A == to.Id || l.B == to.Id) l.Flash = 1f;
        }

        /// <summary>누름: 1프레임에 납작 (Pressed). 살 수 있으면 바로 산다 → 흰 네모 + 길게 튕김</summary>
        void Press(SkillNode n)
        {
            var v = views[n.Id];
            v.Pressed = true; v.Sx = 1.15f; v.Sy = 0.8f; v.Vx = v.Vy = 0f;   // 들어가는 이징 없음: 즉시
            if (n.Key || n.Maxed || !Available(n) || game.Money < n.NextCost)
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
            tipSV = -1.5f; tipTiltV = -60f;             // 설명 상자는 그 자리에서 살짝 움찔만 (사라졌다 다시 뜨지 않는다. 사용자 10-12)
            v.Rot = (Random.value < 0.5f ? -1f : 1f) * Random.Range(14f, 20f); v.RotV = 0f;   // 회전 킥
            Fx.Shake(0.08f);
            StartCoroutine(Rebound(v));
            StartCoroutine(RingCo(Img("Ring", v.Root.parent, v.Root.anchoredPosition, Vector2.one * 16f * Px, "fx_ring", n.Color), 0.5f));
            for (int i = 0; i < 6; i++) StartCoroutine(ShardCo(v, i * 60f + Random.Range(-15f, 15f)));
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

        /// <summary>링: 가는 테두리가 노드 네 배 반경까지 0.5초에 걸쳐 천천히 퍼진다 (Click the Button 3.33~3.67초: 빠르게 시작해 느려지며 계속 커짐)</summary>
        IEnumerator RingCo(Image ring, float life)
        {
            ring.transform.SetSiblingIndex(0);   // 선·노드 뒤
            var c0 = ring.color;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / life)
            {
                ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.8f, 4.5f, Ease.OutCubic(t));
                ring.color = new Color(c0.r, c0.g, c0.b, t < 0.5f ? 1f : 1f - (t - 0.5f) / 0.5f);
                yield return null;
            }
            Destroy(ring.gameObject);
        }

        /// <summary>흰 조각: 짧은 흰 막대가 방사형으로 날아가며 줄어든다 (Click the Button의 흰 네모 조각)</summary>
        IEnumerator ShardCo(NodeView v, float deg)
        {
            var dir = new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
            var s = Img("Shard", v.Root.parent, v.Root.anchoredPosition + dir * NodePx * Px * 0.5f, new Vector2(Random.Range(5f, 9f) * Px, 2f * Px), "link", Color.white);
            s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, deg);
            var from = s.rectTransform.anchoredPosition; float dist = Random.Range(90f, 170f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
            {
                s.rectTransform.anchoredPosition = from + dir * dist * Ease.OutCubic(t);
                s.rectTransform.localScale = new Vector3(1f - t * 0.7f, 1f, 1f);
                if (t > 0.6f) { var c = s.color; c.a = 1f - (t - 0.6f) / 0.4f; s.color = c; }
                yield return null;
            }
            Destroy(s.gameObject);
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
                // Shelldiver 등장 (0.78~1.08초): 흰 마름모가 ±25°로 흔들리며 6프레임, 그다음 4프레임에 걸쳐 제 색으로. 꼭짓점에 계열 색 점
                reveal.Sx = reveal.Sy = 1.3f; reveal.Vx = reveal.Vy = 0f;
                reveal.Rot = (Random.value < 0.5f ? -1f : 1f) * Random.Range(22f, 30f); reveal.RotV = 0f;
                reveal.Flash = 1.6f; reveal.Wake = 1f;
                Sfx.Play(Sfx.Click(), 0.4f, 2.2f);
                StartCoroutine(CornerDots(reveal));
                Confetti(reveal, 14);
                StartCoroutine(Twinkle(reveal));
            }
        }

        /// <summary>
        /// 빛 반사: 흰 빛 토막이 마름모 왼쪽 위 변을 따라 쓸고 지나가고(0.2초), 지나간 끝 꼭짓점에 흰 별이 커졌다 작아진다.
        /// 산 노드가 살아 있다는 표시. 그림(fx_spark·link)만 바꾸면 된다.
        /// </summary>
        IEnumerator GlintCo(NodeView v)
        {
            float half = NodePx * Px * 0.5f;
            Vector2 top = new Vector2(0f, half - Px * 1.5f), right = new Vector2(half - Px * 1.5f, 0f);
            var bar = Img("Glint", v.Root, top, new Vector2(6f * Px, 2f * Px), "link", Color.white);
            bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -45f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.2f)
            {
                bar.rectTransform.anchoredPosition = Vector2.Lerp(top, right, Ease.OutQuad(t));
                var c = bar.color; c.a = 1f - t * t; bar.color = c;
                yield return null;
            }
            Destroy(bar.gameObject);
            var star = Img("GlintStar", v.Root, right, Vector2.one * 5f * Px, "fx_spark", Color.white);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.22f)
            {
                star.rectTransform.localScale = Vector3.one * (0.4f + Ease.Punch(t) * 1.4f);
                star.rectTransform.localRotation = Quaternion.Euler(0f, 0f, t * 90f);
                yield return null;
            }
            Destroy(star.gameObject);
        }

        /// <summary>꼭짓점 점: 드러나는 노드의 네 꼭짓점에 계열 색 점이 붙어 0.4초 깜빡이다 흩어진다</summary>
        IEnumerator CornerDots(NodeView v)
        {
            var dots = new List<Image>();
            for (int i = 0; i < 4; i++)
            {
                float ang = i * 90f * Mathf.Deg2Rad;
                var d = Img("CornerDot", v.Root.parent, v.Root.anchoredPosition + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * NodePx * Px * 0.55f, Vector2.one * 3f * Px, "fx_dot", v.Node.Color);
                dots.Add(d);
            }
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.4f)
            {
                for (int i = 0; i < 4; i++)
                {
                    float ang = (i * 90f + v.Rot) * Mathf.Deg2Rad;
                    dots[i].rectTransform.anchoredPosition = v.Root.anchoredPosition + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * NodePx * Px * (0.55f + t * 0.5f);
                    dots[i].enabled = ((int)(t * 12f) + i) % 3 != 0;   // 깜빡
                }
                yield return null;
            }
            foreach (var d in dots) Destroy(d.gameObject);
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
