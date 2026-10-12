using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 총기 가게 = 설명서 종이 (사용자 10-12 스케치). 왼쪽에 총마다 작은 카드(총 그림)가 세로로,
    /// 가운데에 기울어진 종이 두 장(뒤 파랑, 앞 흰색) — 앞 종이에 총의 **밑그림**(어두운 실루엣), 이름, 설명 줄, 수치 알약(탄·장전).
    /// 창을 열고 잠시 뒤, 밑그림 위에 **진짜 총이 생겨난다** (흰 실루엣 → 제 색, 용수철). 오른쪽 아래 파란 카드에 BUY.
    /// 그림 자리: card_small / paper_sheet(9슬라이스) / stat_pill(9슬라이스) / paper_tag / 총 gun_{id}_side.
    /// 배경은 게임 화면 블러 + 어두운 반투명.
    /// </summary>
    public class Booklet : MonoBehaviour
    {
        static float Px => TreeView.Px;
        static float Mult => 100f / (TreeArt.Ppu * Px);

        class Card { public GunKind Kind; public Image Frame, Thumb; public float X, XV, Scale = 1f, ScaleV; public Vector2 Home; }
        class Stat { public Image Icon, Pill; public Text Value; }

        Game game;
        Image veil, sheetBack, sheetFront, sketch, gunImg, gunWhite, buyCard, buyPill;
        Text title, line1, line2, buyText, state, hintEquip;
        readonly Stat[] statRows = new Stat[2];
        readonly List<Card> cards = new List<Card>();
        GunKind selected;
        float scroll, scrollV;
        float backRot, backRotV, backS, backSV, frontRot, frontRotV, frontS, frontSV;   // 종이 두 장
        float gunS, gunSV, gunRot, gunRotV, gunWhiteA, gunA, revealTimer;             // 진짜 총
        float buyS = 1f, buySV, buyRot, buyRotV;
        Sprite gunSprite, gunWhiteSprite;
        RenderTexture blurRt; Camera blurCam;
        RectTransform content; const float Fit = 0.8f;
        static readonly Dictionary<Sprite, Sprite> whites = new Dictionary<Sprite, Sprite>();
        static readonly Color Ink = new Color(0.12f, 0.1f, 0.14f);
        static readonly Vector2 GunPos = new Vector2(-180f, 120f);   // 앞 종이 위 총 자리 (왼쪽 위)
        const float GunScale = 5f;                                   // 총 그림 배율 (도트 칸: 5픽셀)

        public bool Visible => veil.gameObject.activeSelf;

        public void Build(Game g, Transform canvas)
        {
            game = g;
            veil = Ui.Image("Booklet", canvas, Vector2.zero, new Vector2(4000f, 3000f), Color.clear);
            blurRt = new RenderTexture(192, 108, 0) { filterMode = FilterMode.Bilinear };
            var camGo = new GameObject("BlurCam"); camGo.transform.SetParent(Camera.main.transform, false);
            blurCam = camGo.AddComponent<Camera>(); blurCam.CopyFrom(Camera.main); blurCam.targetTexture = blurRt; blurCam.depth = Camera.main.depth - 1; blurCam.enabled = false;
            var blur = new GameObject("Blur", typeof(RectTransform)); blur.transform.SetParent(veil.transform, false);
            var brt = (RectTransform)blur.transform; brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.sizeDelta = new Vector2(1920f, 1080f);
            var raw = blur.AddComponent<RawImage>(); raw.texture = blurRt; raw.raycastTarget = false; raw.color = new Color(0.8f, 0.75f, 0.7f);
            Ui.Image("Tint", veil.transform, Vector2.zero, new Vector2(4000f, 3000f), new Color(0.12f, 0.08f, 0.06f, 0.62f));
            // 전체 구성은 이 안에 (사용자 10-12: 조금 커서 0.8배)
            content = Ui.Rect("Content", veil.transform, Vector2.zero, Vector2.zero);
            content.localScale = Vector3.one * Fit;
            var root = content;

            // 왼쪽: 작은 카드 (총 그림 축소)
            for (int i = 0; i < GunKind.ForSale.Count; i++)
            {
                var k = GunKind.ForSale[i];
                // 스케치대로 화면을 꽉 채운다 (사용자 10-12): 왼쪽 카드 열은 화면 1/6, 종이가 나머지를 거의 다 차지
                var frame = Img("Card_" + k.Id, root, Vector2.zero, new Vector2(44f, 32f) * Px, "card_small", Color.white);
                var sp = GunView.LoadArt($"gun_{k.Id}_side") ?? GunView.LoadArt("gun_revolver_side");
                var thumb = Img("Thumb", frame.transform, new Vector2(8f, 0f), sp != null ? new Vector2(sp.rect.width, sp.rect.height) * 1.3f : new Vector2(130f, 80f), null, Color.white);
                thumb.sprite = sp; thumb.preserveAspect = true;
                cards.Add(new Card { Kind = k, Frame = frame, Thumb = thumb, Home = new Vector2(-790f, 400f - i * 160f) });
            }
            // 가운데: 종이 두 장 (크게)
            sheetBack = Img("SheetBack", root, new Vector2(160f, -10f), new Vector2(1180f, 820f), "paper_sheet", new Color(0.72f, 0.8f, 0.95f));
            sheetBack.type = Image.Type.Sliced; sheetBack.pixelsPerUnitMultiplier = Mult;
            sheetFront = Img("SheetFront", root, new Vector2(120f, 30f), new Vector2(1140f, 780f), "paper_sheet", new Color(0.98f, 0.97f, 0.94f));
            sheetFront.type = Image.Type.Sliced; sheetFront.pixelsPerUnitMultiplier = Mult;
            // 앞 종이 위: 이름(오른쪽 위), 밑그림(왼쪽 위 크게), 설명 줄, 수치(왼쪽 아래)
            title = Ui.Text("Title", sheetFront.transform, new Vector2(300f, 270f), new Vector2(460f, 90f), 60, TextAnchor.MiddleCenter, Ink, shadow: false);
            sketch = Img("Sketch", sheetFront.transform, GunPos, Vector2.one, null, new Color(0.25f, 0.22f, 0.28f, 0.9f));
            gunWhite = Img("GunWhite", sheetFront.transform, GunPos, Vector2.one, null, Color.clear);
            gunImg = Img("Gun", sheetFront.transform, GunPos, Vector2.one, null, Color.clear);
            gunWhite.transform.SetAsLastSibling();
            line1 = Ui.Text("Line1", sheetFront.transform, new Vector2(60f, -60f), new Vector2(980f, 44f), 36, TextAnchor.MiddleCenter, Ink, shadow: false);
            line2 = Ui.Text("Line2", sheetFront.transform, new Vector2(60f, -110f), new Vector2(980f, 40f), 24, TextAnchor.MiddleCenter, new Color(0.3f, 0.28f, 0.32f), shadow: false);
            for (int i = 0; i < 2; i++)
            {
                var s = new Stat();
                float y = -200f - i * 90f;
                s.Icon = Img("StatIcon" + i, sheetFront.transform, new Vector2(-400f, y), Vector2.one * 16f * Px, i == 0 ? "icon_reload" : "icon_time", Ink);
                s.Pill = Img("StatPill" + i, sheetFront.transform, new Vector2(-240f, y), new Vector2(40f, 16f) * Px, "stat_pill", new Color(0.9f, 0.88f, 0.84f));
                s.Pill.type = Image.Type.Sliced; s.Pill.pixelsPerUnitMultiplier = Mult;
                s.Value = Ui.Text("StatValue", s.Pill.transform, Vector2.zero, new Vector2(150f, 50f), 36, TextAnchor.MiddleCenter, Ink, shadow: false);
                statRows[i] = s;
            }
            state = Ui.Text("State", sheetFront.transform, new Vector2(300f, -300f), new Vector2(400f, 48f), 36, TextAnchor.MiddleCenter, new Color(0.35f, 0.3f, 0.25f), shadow: false);
            // 오른쪽 아래: BUY 카드
            buyCard = Img("BuyCard", root, new Vector2(700f, -400f), new Vector2(60f, 30f) * Px, "paper_sheet", new Color(0.72f, 0.8f, 0.95f));
            buyCard.type = Image.Type.Sliced; buyCard.pixelsPerUnitMultiplier = Mult;
            buyPill = Img("BuyPill", buyCard.transform, new Vector2(20f, -12f), new Vector2(44f, 16f) * Px, "stat_pill", new Color(1f, 0.92f, 0.6f));
            buyPill.type = Image.Type.Sliced; buyPill.pixelsPerUnitMultiplier = Mult;
            buyText = Ui.Text("BuyText", buyPill.transform, Vector2.zero, new Vector2(170f, 50f), 36, TextAnchor.MiddleCenter, Ink, shadow: false);
            hintEquip = Ui.Text("HintEquip", root, new Vector2(160f, -490f), new Vector2(900f, 36f), 24, TextAnchor.MiddleCenter, Ui.Dim);
            veil.gameObject.SetActive(false);
        }

        public void Show()
        {
            veil.gameObject.SetActive(true);
            veil.transform.SetAsLastSibling();
            if (blurCam != null) { blurCam.CopyFrom(Camera.main); blurCam.targetTexture = blurRt; blurCam.enabled = true; }
            // 종이가 날아 들어온다 (뒤 먼저, 앞은 반 박자 뒤)
            backS = 0.4f; backSV = 9f; backRot = 20f; backRotV = 0f;
            frontS = 0.3f; frontSV = 7f; frontRot = -24f; frontRotV = 0f;
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
            var size = gunSprite != null ? new Vector2(gunSprite.rect.width, gunSprite.rect.height) * GunScale : new Vector2(500f, 320f);
            // 밑그림 = 총 그림을 어둡게 물들인 것 (읽기 불가 텍스처여도 된다). 흰 실루엣은 읽을 수 있을 때만
            sketch.sprite = gunSprite; gunImg.sprite = gunSprite; gunWhite.sprite = gunWhiteSprite;
            gunWhite.enabled = gunWhiteSprite != null;
            foreach (var img in new[] { sketch, gunImg, gunWhite }) img.rectTransform.sizeDelta = size;
            sketch.color = new Color(0.25f, 0.22f, 0.28f, 0.9f);
            gunA = 0f; gunWhiteA = 0f; gunS = 1f; gunRot = 0f; revealTimer = 0.9f;   // 잠시 뒤 등장
            frontSV += 2f; frontRotV -= 20f;                                         // 종이가 움찔
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
            Vector2 canvasPt = new Vector2(mp.x - Screen.width * 0.5f, mp.y - Screen.height * 0.5f) * k / Fit;   // 구성이 0.8배라 좌표도 나눈다
            if (mouse != null) { float wheel = mouse.scroll.ReadValue().y; if (Mathf.Abs(wheel) > 0.01f) scrollV += wheel * 90f; }
            Ease.Spring(ref scroll, ref scrollV, Mathf.Clamp(scroll, 0f, Mathf.Max(0f, (cards.Count - 6) * 160f)), 200f, 14f, dt);
            // 왼쪽 카드
            Card hot = null;
            foreach (var c in cards)
            {
                var pos = c.Home + new Vector2(0f, scroll);
                bool over = Mathf.Abs(canvasPt.x - pos.x) < 95f && Mathf.Abs(canvasPt.y - pos.y) < 68f;
                if (over) hot = c;
                bool owned = game.OwnedGuns.Contains(c.Kind.Id), sel = selected == c.Kind;
                Ease.Spring(ref c.X, ref c.XV, sel ? 40f : over ? 18f : 0f, 500f, 16f, dt);
                Ease.Spring(ref c.Scale, ref c.ScaleV, sel ? 1.1f : 1f, 500f, 16f, dt);
                c.Frame.rectTransform.anchoredPosition = pos + new Vector2(c.X, 0f);
                c.Frame.rectTransform.localScale = Vector3.one * c.Scale;
                var tint = c.Kind.IsCursed ? new Color(0.5f, 0.3f, 0.6f) : owned ? new Color(0.8f, 0.55f, 0.3f) : new Color(0.55f, 0.45f, 0.35f);
                c.Frame.color = sel ? Color.Lerp(tint, Color.white, 0.3f) : tint;
                c.Thumb.color = owned || sel ? Color.white : new Color(0.5f, 0.5f, 0.55f);
            }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && hot != null && hot.Kind != selected) { Select(hot.Kind); hot.ScaleV = 3f; }
            // 종이 두 장: 크기·기울기 용수철 (뒤 +8°, 앞 -6°로 자리 잡는다)
            Ease.Spring(ref backS, ref backSV, 1f, 300f, 12f, dt); Ease.Spring(ref backRot, ref backRotV, 6f, 260f, 11f, dt);
            Ease.Spring(ref frontS, ref frontSV, 1f, 300f, 12f, dt); Ease.Spring(ref frontRot, ref frontRotV, -4f, 260f, 11f, dt);   // 두 장 합쳐 10° 안쪽 (사용자 10-12)
            sheetBack.rectTransform.localScale = Vector3.one * backS; sheetBack.rectTransform.localRotation = Quaternion.Euler(0f, 0f, backRot);
            sheetFront.rectTransform.localScale = Vector3.one * frontS; sheetFront.rectTransform.localRotation = Quaternion.Euler(0f, 0f, frontRot);
            // 진짜 총: 잠시 뒤 밑그림 위에 생겨난다
            if (revealTimer > 0f)
            {
                revealTimer -= dt;
                if (revealTimer <= 0f)
                {
                    gunA = 1f; gunWhiteA = 1f; gunS = 0.6f; gunSV = 7f; gunRot = Random.Range(-10f, 10f); gunRotV = 0f;
                    frontSV += 1.5f;
                    Sfx.Play(Sfx.Tang(), 0.6f, 1.2f);
                }
            }
            Ease.Spring(ref gunS, ref gunSV, 1f, 420f, 14f, dt);
            Ease.Spring(ref gunRot, ref gunRotV, 0f, 300f, 9f, dt);
            gunWhiteA = Mathf.Max(0f, gunWhiteA - dt / 0.18f);
            float bob = gunA > 0f ? Mathf.Sin(Time.unscaledTime * 1.6f) * 5f : 0f;
            foreach (var img in new[] { gunImg, gunWhite })
            {
                img.rectTransform.anchoredPosition = GunPos + new Vector2(0f, bob);
                img.rectTransform.localScale = Vector3.one * gunS;
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, gunRot);
            }
            gunImg.color = new Color(1f, 1f, 1f, gunA);
            gunWhite.color = new Color(1f, 1f, 1f, gunWhiteA);
            sketch.color = new Color(0.25f, 0.22f, 0.28f, Mathf.Lerp(0.9f, 0.25f, gunA));   // 총이 오면 밑그림은 옅어진다
            // 글·수치·BUY
            if (selected != null)
            {
                var g = selected;
                bool owned = game.OwnedGuns.Contains(g.Id), primary = game.Primary == g, secondary = game.Secondary == g;
                title.text = $"<b>{g.Name}</b>";
                line1.text = g.Desc;
                line2.text = $"값 ×{g.ValueScale:0.#}  ·  튐 ×{g.BounceScale:0.##}" + (g.HitAll ? "  ·  원 안 전부" : "") + (g.IsCursed ? "  ·  저주" : "");
                statRows[0].Value.text = $"{g.Capacity}";
                statRows[1].Value.text = $"{g.ReloadSeconds:0.0}s";
                state.text = primary ? "주무장" : secondary ? "보조무기" : owned ? "보유" : "";
                bool afford = game.Money >= g.Price;
                buyText.text = owned ? "보유" : $"BUY  ${g.Price:N0}";
                buyPill.color = owned ? new Color(0.75f, 0.9f, 0.7f) : afford ? new Color(1f, 0.92f, 0.6f) : new Color(0.95f, 0.65f, 0.6f);
                bool overBuy = Inside(buyCard.rectTransform, canvasPt);
                Ease.Spring(ref buyS, ref buySV, overBuy && !owned ? 1.08f : 1f, 500f, 16f, dt);
                Ease.Spring(ref buyRot, ref buyRotV, overBuy && !owned ? 2f : 5f, 300f, 10f, dt);
                buyCard.rectTransform.localScale = Vector3.one * buyS;
                buyCard.rectTransform.localRotation = Quaternion.Euler(0f, 0f, buyRot);
                hintEquip.text = owned ? "1 = 주무장으로   ·   2 = 보조무기로" : afford ? "" : "<color=#ff8877>돈이 모자라다</color>";
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
            if (game.Money < g.Price) { Sfx.Play(Sfx.Click(), 0.5f, 0.7f); buyRotV += 60f; return; }
            game.PickGun(g);
            buyS = 0.8f; buySV = 6f; buyRot = -12f; buyRotV = 0f;      // 도장 찍히듯
            gunS = 1.15f; gunSV = 0f; gunWhiteA = 0.7f; frontSV += 2f;
            Fx.Shake(0.15f);
            Sfx.Play(Sfx.Tang(), 0.7f, 1.4f);
        }

        void Equip(GunKind g, int slot)
        {
            if (slot == 0) { if (game.Primary != g) { if (game.Secondary != g) game.PickGun(g); game.SwapGuns(); } }
            else { if (game.Secondary != g) game.PickGun(g); }
            gunS = 1.1f; gunSV = 0f; gunRot = Random.Range(-6f, 6f);
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
