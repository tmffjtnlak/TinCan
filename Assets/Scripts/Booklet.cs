using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 총기 가게 = 설명서 종이 (사용자 10-12 스케치). 왼쪽에 총마다 작은 카드(총 그림)가 세로로,
    /// 가운데에 종이 두 장(뒤 파랑, 앞 흰색) — 앞 종이에 총의 **밑그림**(어두운 실루엣), 이름, 설명 줄, 수치 알약(탄·장전).
    /// 창을 열고 잠시 뒤, 밑그림 위에 **진짜 총이 생겨난다** (흰 실루엣 → 제 색). 오른쪽 아래 파란 카드에 BUY.
    ///
    /// 도트판 (사용자 10-14 "상점도 도트 스타일에 맞춰서"): 화면 1도트 = 1080p 4픽셀 규칙에 맞춘다.
    ///  - 전체 0.8배 축소를 없앴다 (도트가 3.2픽셀로 뭉개졌다). 자리·크기는 전부 4픽셀 배수, 움직임도 4픽셀 칸으로 끊는다
    ///  - 종이·카드·버튼은 기울이지 않는다 (돌린 도트는 계단이 깨진다). 등장·반응은 세로 용수철로
    ///  - 큰 총은 그림 1칸 = 2도트(8배), 제목 글자도 2도트(96), 본문 1도트(48), 작은 글 반도트(24) — DenkiChip은 12픽셀 글꼴
    ///  - 배경 블러 대신 240×135로 뭉갠 도트 화면 (한 칸 = 8픽셀)
    /// </summary>
    public class Booklet : MonoBehaviour
    {
        static float Px => TreeView.Px;
        static float Mult => 100f / (TreeArt.Ppu * Px);
        const float Grid = 4f;   // 화면 도트 한 칸 (1080p 픽셀)
        static float Snap(float v) => Mathf.Round(v / Grid) * Grid;

        class Card { public GunKind Kind; public Image Frame, Thumb; public float X, XV; public Vector2 Home; }
        class Stat { public Image Icon, Pill; public Text Value; }

        Game game;
        Image veil, sheetBack, sheetFront, sketch, gunImg, gunWhite, buyCard, buyPill;
        Text title, line1, line2, buyText, state, hintEquip;
        readonly Stat[] statRows = new Stat[2];
        readonly List<Card> cards = new List<Card>();
        GunKind selected;
        float scroll, scrollV;
        float backY, backYV, frontY, frontYV, frontDelay;              // 종이 두 장: 아래에서 올라온다
        float gunY, gunYV, gunWhiteA, gunA, revealTimer;               // 진짜 총: 위에서 툭
        float buyY, buyYV;
        Sprite gunSprite, gunWhiteSprite;
        RenderTexture blurRt; Camera blurCam;
        RectTransform content; const float Fit = 1f;
        static readonly Dictionary<Sprite, Sprite> whites = new Dictionary<Sprite, Sprite>();
        static readonly Color Ink = new Color(0.12f, 0.1f, 0.14f);
        static readonly Vector2 BackHome = new Vector2(160f, -8f), FrontHome = new Vector2(120f, 32f), BuyHome = new Vector2(700f, -400f);
        static readonly Vector2 GunPos = new Vector2(-180f, 136f);   // 앞 종이 위 총 자리 (왼쪽 위)
        const float GunScale = 8f;                                   // 총 그림 1칸 = 2도트
        const float ThumbScale = 2f;                                 // 카드 속 작은 총 1칸 = 반도트

        public bool Visible => veil.gameObject.activeSelf;

        public void Build(Game g, Transform canvas)
        {
            game = g;
            veil = Ui.Image("Booklet", canvas, Vector2.zero, new Vector2(4000f, 3000f), Color.clear);
            // 뒤 화면: 부드러운 블러 대신 큰 도트로 뭉갠다 (240×135, 점 필터 → 한 칸 8픽셀)
            blurRt = new RenderTexture(240, 135, 0) { filterMode = FilterMode.Point };
            var camGo = new GameObject("BlurCam"); camGo.transform.SetParent(Camera.main.transform, false);
            blurCam = camGo.AddComponent<Camera>(); blurCam.CopyFrom(Camera.main); blurCam.targetTexture = blurRt; blurCam.depth = Camera.main.depth - 1; blurCam.enabled = false;
            var blur = new GameObject("Blur", typeof(RectTransform)); blur.transform.SetParent(veil.transform, false);
            var brt = (RectTransform)blur.transform; brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.sizeDelta = new Vector2(1920f, 1080f);
            var raw = blur.AddComponent<RawImage>(); raw.texture = blurRt; raw.raycastTarget = false; raw.color = new Color(0.8f, 0.75f, 0.7f);
            Ui.Image("Tint", veil.transform, Vector2.zero, new Vector2(4000f, 3000f), new Color(0.12f, 0.08f, 0.06f, 0.62f));
            content = Ui.Rect("Content", veil.transform, Vector2.zero, Vector2.zero);
            content.localScale = Vector3.one * Fit;
            var root = content;

            // 왼쪽: 작은 카드 (총 그림 축소)
            for (int i = 0; i < GunKind.ForSale.Count; i++)
            {
                var k = GunKind.ForSale[i];
                var frame = Img("Card_" + k.Id, root, Vector2.zero, new Vector2(44f, 32f) * Px, "card_small", Color.white);
                var sp = GunView.LoadArt($"gun_{k.Id}_side") ?? GunView.LoadArt("gun_revolver_side");
                var thumb = Img("Thumb", frame.transform, new Vector2(8f, 0f), sp != null ? new Vector2(sp.rect.width, sp.rect.height) * ThumbScale : new Vector2(128f, 80f), null, Color.white);
                thumb.sprite = sp; thumb.preserveAspect = true;
                cards.Add(new Card { Kind = k, Frame = frame, Thumb = thumb, Home = new Vector2(-792f, 400f - i * 160f) });
            }
            // 가운데: 종이 두 장 (기울이지 않고 겹쳐 놓는다)
            sheetBack = Img("SheetBack", root, BackHome, new Vector2(1184f, 824f), "paper_sheet", new Color(0.72f, 0.8f, 0.95f));
            sheetBack.type = Image.Type.Sliced; sheetBack.pixelsPerUnitMultiplier = Mult;
            sheetFront = Img("SheetFront", root, FrontHome, new Vector2(1136f, 784f), "paper_sheet", new Color(0.98f, 0.97f, 0.94f));
            sheetFront.type = Image.Type.Sliced; sheetFront.pixelsPerUnitMultiplier = Mult;
            // 앞 종이 위: 이름(오른쪽 위), 밑그림(왼쪽 위 크게), 설명 줄, 수치(왼쪽 아래)
            title = Ui.Text("Title", sheetFront.transform, new Vector2(352f, 280f), new Vector2(400f, 112f), 96, TextAnchor.MiddleCenter, Ink, shadow: false);
            sketch = Img("Sketch", sheetFront.transform, GunPos, Vector2.one, null, new Color(0.25f, 0.22f, 0.28f, 0.9f));
            gunWhite = Img("GunWhite", sheetFront.transform, GunPos, Vector2.one, null, Color.clear);
            gunImg = Img("Gun", sheetFront.transform, GunPos, Vector2.one, null, Color.clear);
            gunWhite.transform.SetAsLastSibling();
            line1 = Ui.Text("Line1", sheetFront.transform, new Vector2(0f, -100f), new Vector2(1056f, 56f), 48, TextAnchor.MiddleCenter, Ink, shadow: false);
            line2 = Ui.Text("Line2", sheetFront.transform, new Vector2(0f, -152f), new Vector2(1056f, 32f), 24, TextAnchor.MiddleCenter, new Color(0.3f, 0.28f, 0.32f), shadow: false);
            for (int i = 0; i < 2; i++)
            {
                var s = new Stat();
                float y = -228f - i * 88f;
                s.Icon = Img("StatIcon" + i, sheetFront.transform, new Vector2(-440f, y), Vector2.one * 16f * Px, i == 0 ? "icon_reload" : "icon_time", Ink);
                s.Pill = Img("StatPill" + i, sheetFront.transform, new Vector2(-288f, y), new Vector2(40f, 16f) * Px, "stat_pill", new Color(0.9f, 0.88f, 0.84f));
                s.Pill.type = Image.Type.Sliced; s.Pill.pixelsPerUnitMultiplier = Mult;
                s.Value = Ui.Text("StatValue", s.Pill.transform, Vector2.zero, new Vector2(152f, 56f), 48, TextAnchor.MiddleCenter, Ink, shadow: false);
                statRows[i] = s;
            }
            state = Ui.Text("State", sheetFront.transform, new Vector2(352f, -300f), new Vector2(400f, 56f), 48, TextAnchor.MiddleCenter, new Color(0.35f, 0.3f, 0.25f), shadow: false);
            // 오른쪽 아래: BUY 카드
            buyCard = Img("BuyCard", root, BuyHome, new Vector2(60f, 30f) * Px, "paper_sheet", new Color(0.72f, 0.8f, 0.95f));
            buyCard.type = Image.Type.Sliced; buyCard.pixelsPerUnitMultiplier = Mult;
            buyPill = Img("BuyPill", buyCard.transform, new Vector2(16f, -12f), new Vector2(44f, 16f) * Px, "stat_pill", new Color(1f, 0.92f, 0.6f));
            buyPill.type = Image.Type.Sliced; buyPill.pixelsPerUnitMultiplier = Mult;
            buyText = Ui.Text("BuyText", buyPill.transform, Vector2.zero, new Vector2(168f, 56f), 24, TextAnchor.MiddleCenter, Ink, shadow: false);
            hintEquip = Ui.Text("HintEquip", root, new Vector2(160f, -488f), new Vector2(900f, 32f), 24, TextAnchor.MiddleCenter, Ui.Dim);
            veil.gameObject.SetActive(false);
        }

        public void Show()
        {
            veil.gameObject.SetActive(true);
            veil.transform.SetAsLastSibling();
            if (blurCam != null) { blurCam.CopyFrom(Camera.main); blurCam.targetTexture = blurRt; blurCam.enabled = true; }
            // 종이가 아래에서 올라온다 (뒤 먼저, 앞은 반 박자 뒤). 기울임 없이 세로 용수철만
            backY = -1100f; backYV = 0f;
            frontY = -1200f; frontYV = 0f; frontDelay = 0.06f;
            buyY = -700f; buyYV = 0f;
            Select(selected ?? game.Primary);
            Sfx.Play(Sfx.Whoosh(), 0.5f, 1.3f);
        }
        public void Hide() { veil.gameObject.SetActive(false); if (blurCam != null) blurCam.enabled = false; }

        /// <summary>카드를 고른다: 밑그림이 바뀌고, 잠시 뒤 진짜 총이 그 위에 생겨난다</summary>
        void Select(GunKind k)
        {
            selected = k;
            gunSprite = GunView.LoadArt($"gun_{k.Id}_side") ?? GunView.LoadArt("gun_revolver_side");
            gunWhiteSprite = WhiteOf(gunSprite);
            var size = gunSprite != null ? new Vector2(gunSprite.rect.width, gunSprite.rect.height) * GunScale : new Vector2(512f, 320f);
            sketch.sprite = gunSprite; gunImg.sprite = gunSprite; gunWhite.sprite = gunWhiteSprite;
            gunWhite.enabled = gunWhiteSprite != null;
            foreach (var img in new[] { sketch, gunImg, gunWhite }) img.rectTransform.sizeDelta = size;
            sketch.color = new Color(0.25f, 0.22f, 0.28f, 0.9f);
            gunA = 0f; gunWhiteA = 0f; gunY = 0f; gunYV = 0f; revealTimer = 0.6f;   // 잠시 뒤 등장
            frontYV -= 260f;                                                       // 종이가 움찔 (아래로 톡)
            Sfx.Play(Sfx.Click(), 0.4f, 1.8f);
        }

        static Sprite WhiteOf(Sprite s)
        {
            if (s == null) return null;
            if (whites.TryGetValue(s, out var w)) return w;
            try
            {
                var r = s.textureRect;
                var tex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = s.texture.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
                for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, px[i].a);
                tex.SetPixels(px); tex.Apply();
                w = Sprite.Create(tex, new Rect(0, 0, r.width, r.height), s.pivot / new Vector2(r.width, r.height), s.pixelsPerUnit);
            }
            catch { w = null; }
            whites[s] = w;
            return w;
        }

        void Update()
        {
            if (game == null || !Visible) return;
            float dt = Time.unscaledDeltaTime;
            var mouse = Mouse.current; var kb = Keyboard.current;
            Vector2 mp = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            float k = 1080f / Screen.height;
            Vector2 canvasPt = new Vector2(mp.x - Screen.width * 0.5f, mp.y - Screen.height * 0.5f) * k / Fit;
            if (mouse != null) { float wheel = mouse.scroll.ReadValue().y; if (Mathf.Abs(wheel) > 0.01f) scrollV += wheel * 90f; }
            Ease.Spring(ref scroll, ref scrollV, Mathf.Clamp(scroll, 0f, Mathf.Max(0f, (cards.Count - 6) * 160f)), 200f, 14f, dt);
            // 왼쪽 카드: 고르면 오른쪽으로 10도트, 올리면 4도트 (크기는 그대로 — 키우면 도트가 어긋난다)
            Card hot = null;
            foreach (var c in cards)
            {
                var pos = c.Home + new Vector2(0f, scroll);
                bool over = Mathf.Abs(canvasPt.x - pos.x) < 88f && Mathf.Abs(canvasPt.y - pos.y) < 64f;
                if (over) hot = c;
                bool owned = game.OwnedGuns.Contains(c.Kind.Id), sel = selected == c.Kind;
                Ease.Spring(ref c.X, ref c.XV, sel ? 40f : over ? 16f : 0f, 500f, 16f, dt);
                c.Frame.rectTransform.anchoredPosition = new Vector2(Snap(pos.x + c.X), Snap(pos.y));
                var tint = c.Kind.IsCursed ? new Color(0.5f, 0.3f, 0.6f) : owned ? new Color(0.8f, 0.55f, 0.3f) : new Color(0.55f, 0.45f, 0.35f);
                c.Frame.color = sel ? Color.Lerp(tint, Color.white, 0.3f) : tint;
                c.Thumb.color = owned || sel ? Color.white : new Color(0.5f, 0.5f, 0.55f);
            }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && hot != null && hot.Kind != selected) { Select(hot.Kind); hot.XV += 200f; }
            // 종이 두 장 + BUY 카드: 아래에서 세로 용수철로 올라와 자리 (4픽셀 칸으로 끊어 움직임)
            Ease.Spring(ref backY, ref backYV, 0f, 260f, 15f, dt);
            if (frontDelay > 0f) frontDelay -= dt; else Ease.Spring(ref frontY, ref frontYV, 0f, 260f, 15f, dt);
            sheetBack.rectTransform.anchoredPosition = BackHome + new Vector2(0f, Snap(backY));
            sheetFront.rectTransform.anchoredPosition = FrontHome + new Vector2(0f, Snap(frontY));
            // 진짜 총: 잠시 뒤 밑그림 위에 생겨난다 (흰 실루엣 → 제 색, 위에서 4도트 떨어져 톡)
            if (revealTimer > 0f)
            {
                revealTimer -= dt;
                if (revealTimer <= 0f)
                {
                    gunA = 1f; gunWhiteA = 1f; gunY = 32f; gunYV = 0f;
                    frontYV -= 160f;
                    Sfx.Play(Sfx.Tang(), 0.6f, 1.2f);
                }
            }
            Ease.Spring(ref gunY, ref gunYV, 0f, 420f, 14f, dt);
            gunWhiteA = Mathf.Max(0f, gunWhiteA - dt / 0.12f);
            // 숨쉬기: 1도트씩 위아래 (부드럽게 미끄러지지 않고 칸으로)
            float bob = gunA > 0f ? (Mathf.Sin(Time.unscaledTime * 1.6f) > 0f ? Grid : 0f) : 0f;
            foreach (var img in new[] { gunImg, gunWhite })
            {
                img.rectTransform.anchoredPosition = GunPos + new Vector2(0f, Snap(gunY) + bob);
                img.rectTransform.localScale = Vector3.one;
                img.rectTransform.localRotation = Quaternion.identity;
            }
            gunImg.color = new Color(1f, 1f, 1f, gunA);
            gunWhite.color = new Color(1f, 1f, 1f, gunWhiteA > 0f ? 1f : 0f);                 // 흰 실루엣은 반투명 없이 켜졌다 꺼진다
            sketch.color = new Color(0.25f, 0.22f, 0.28f, gunA > 0f ? 0.25f : 0.9f);           // 총이 오면 밑그림은 옅어진다
            // 글·수치·BUY
            if (selected != null)
            {
                var g = selected;
                bool owned = game.OwnedGuns.Contains(g.Id), primary = game.Primary == g, secondary = game.Secondary == g;
                title.text = g.Name;
                line1.text = g.Desc;
                line2.text = $"값 ×{g.ValueScale:0.#}  ·  튐 ×{g.BounceScale:0.##}" + (g.HitAll ? "  ·  원 안 전부" : "") + (g.IsCursed ? "  ·  저주" : "");
                statRows[0].Value.text = $"{g.Capacity}";
                statRows[1].Value.text = $"{g.ReloadSeconds:0.0}s";
                state.text = primary ? "주무장" : secondary ? "보조무기" : owned ? "보유" : "";
                bool afford = game.Tokens >= g.Price;   // 총은 현상금 증표로 산다 (10-14)
                buyText.text = owned ? "보유" : $"BUY  증표 {g.Price}";
                buyPill.color = owned ? new Color(0.75f, 0.9f, 0.7f) : afford ? new Color(1f, 0.92f, 0.6f) : new Color(0.95f, 0.65f, 0.6f);
                bool overBuy = Inside(buyCard.rectTransform, canvasPt * Fit);
                // BUY: 올리면 1도트 들림, 누르면 2도트 눌림 (기울임·확대 없음)
                Ease.Spring(ref buyY, ref buyYV, overBuy && !owned ? Grid : 0f, 500f, 16f, dt);
                buyCard.rectTransform.anchoredPosition = BuyHome + new Vector2(0f, Snap(buyY));
                hintEquip.text = owned ? "1 = 주무장으로   ·   2 = 보조무기로" : afford ? "" : $"<color=#ff8877>증표가 모자라다 (가진 것 {game.Tokens}) — 현상금을 잡으면 1개</color>";
                bool buyClick = mouse != null && mouse.leftButton.wasPressedThisFrame && overBuy;
                if (!owned && (buyClick || (kb != null && kb.bKey.wasPressedThisFrame))) Buy(g);
                if (owned && kb != null && kb.digit1Key.wasPressedThisFrame) Equip(g, 0);
                if (owned && kb != null && kb.digit2Key.wasPressedThisFrame) Equip(g, 1);
            }
        }

        static bool Inside(RectTransform rt, Vector2 canvasPt)
        {
            var corners = new Vector3[4]; rt.GetWorldCorners(corners);
            float s = 1080f / Screen.height;
            Vector2 min = new Vector2(corners[0].x - Screen.width * 0.5f, corners[0].y - Screen.height * 0.5f) * s;
            Vector2 max = new Vector2(corners[2].x - Screen.width * 0.5f, corners[2].y - Screen.height * 0.5f) * s;
            return canvasPt.x >= Mathf.Min(min.x, max.x) && canvasPt.x <= Mathf.Max(min.x, max.x) && canvasPt.y >= Mathf.Min(min.y, max.y) && canvasPt.y <= Mathf.Max(min.y, max.y);
        }

        void Buy(GunKind g)
        {
            if (game.Tokens < g.Price) { Sfx.Play(Sfx.Click(), 0.5f, 0.7f); buyYV -= 300f; return; }
            game.PickGun(g);
            buyY = -8f; buyYV = 0f;                         // 도장 찍히듯 2도트 눌림
            gunY = 16f; gunYV = 0f; gunWhiteA = 0.08f; frontYV -= 200f;
            Fx.Shake(0.15f);
            Sfx.Play(Sfx.Tang(), 0.7f, 1.4f);
        }

        void Equip(GunKind g, int slot)
        {
            if (slot == 0) { if (game.Primary != g) { if (game.Secondary != g) game.PickGun(g); game.SwapGuns(); } }
            else { if (game.Secondary != g) game.PickGun(g); }
            gunY = 12f; gunYV = 0f;
            Sfx.Play(Sfx.Reload(), 0.6f, 1.5f);
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
    }
}
