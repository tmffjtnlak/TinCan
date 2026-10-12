using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 총기 가게 = 종이 책자 (사용자 10-12 초안). 왼쪽에 총마다 책자 한 권이 세로로 쌓여 있고(휠로 스크롤),
    /// 누르면 그 책자가 가운데에서 펼쳐지고 종이 위에 총이 생겨난다. 이름·한 줄·수치·가격표, 사기 / 장착.
    /// 그림 자리: book_cover / book_open(9슬라이스) / paper_tag(9슬라이스), 총 그림은 gun_{id}_side (없으면 리볼버 그림).
    /// 움직임: 책자 호버 = 앞으로 살짝, 선택 = 가로 0→1 용수철로 펼쳐짐, 총 = 흰 실루엣으로 작고 기울어져 떴다가 튕김,
    /// 구매 = 가격표가 찍히듯 내려오며 흔들림. 전부 Ease.Spring.
    /// </summary>
    public class Booklet : MonoBehaviour
    {
        static float Px => TreeView.Px;
        static float Mult => 100f / (TreeArt.Ppu * Px);

        class Book { public GunKind Kind; public Image Cover; public Text Label; public float X, XV, Scale = 1f, ScaleV; public Vector2 Home; }

        Game game;
        Image veil, open, gunImg, gunWhite, tag;
        Text title, desc, stats, tagText, state, hintBuy, hintEquip;
        readonly List<Book> books = new List<Book>();
        GunKind selected;
        float scroll, scrollV, openSx, openSxV, gunS, gunSV, gunRot, gunRotV, gunWhiteA, tagY, tagYV, tagRot, tagRotV, tagS = 1f, tagSV;
        Sprite gunSprite, gunWhiteSprite;
        RenderTexture blurRt; Camera blurCam;
        static readonly Dictionary<Sprite, Sprite> whites = new Dictionary<Sprite, Sprite>();

        public bool Visible => veil.gameObject.activeSelf;

        public void Build(Game g, Transform canvas)
        {
            game = g;
            // 배경 = 게임 화면을 흐리게 (사용자 10-12: 반투명 블러 느낌). 작은 렌더텍스처(192×108)에 그려서 크게 늘리면 흐려진다. 그 위에 어두운 반투명
            veil = Ui.Image("Booklet", canvas, Vector2.zero, new Vector2(4000f, 3000f), Color.clear);
            blurRt = new RenderTexture(192, 108, 0) { filterMode = FilterMode.Bilinear };
            var camGo = new GameObject("BlurCam"); camGo.transform.SetParent(Camera.main.transform, false);
            blurCam = camGo.AddComponent<Camera>(); blurCam.CopyFrom(Camera.main); blurCam.targetTexture = blurRt; blurCam.depth = Camera.main.depth - 1; blurCam.enabled = false;
            var blur = new GameObject("Blur", typeof(RectTransform)); blur.transform.SetParent(veil.transform, false);
            var brt = (RectTransform)blur.transform; brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f); brt.sizeDelta = new Vector2(1920f, 1080f);
            var raw = blur.AddComponent<RawImage>(); raw.texture = blurRt; raw.raycastTarget = false; raw.color = new Color(0.8f, 0.75f, 0.7f);
            Ui.Image("Tint", veil.transform, Vector2.zero, new Vector2(4000f, 3000f), new Color(0.12f, 0.08f, 0.06f, 0.62f));
            // 왼쪽: 책자 목록
            for (int i = 0; i < GunKind.ForSale.Count; i++)
            {
                var k = GunKind.ForSale[i];
                var cover = Img("Book_" + k.Id, veil.transform, Vector2.zero, new Vector2(44f, 30f) * Px, "book_cover", Color.white);
                var label = Ui.Text("Label", cover.transform, new Vector2(8f, 0f), new Vector2(150f, 40f), 24, TextAnchor.MiddleCenter, new Color(0.15f, 0.1f, 0.06f), shadow: false);
                label.text = k.Name;
                books.Add(new Book { Kind = k, Cover = cover, Label = label, Home = new Vector2(-660f, 300f - i * 150f) });
            }
            // 가운데: 펼친 책 + 총 + 글
            open = Img("Open", veil.transform, new Vector2(170f, 20f), new Vector2(160f, 100f) * Px, "book_open", new Color(0.93f, 0.86f, 0.7f));
            open.type = Image.Type.Sliced; open.pixelsPerUnitMultiplier = Mult;
            gunWhite = Img("GunWhite", open.transform, new Vector2(0f, 60f), Vector2.one, null, Color.clear);
            gunImg = Img("Gun", open.transform, new Vector2(0f, 60f), Vector2.one, null, Color.white);
            gunWhite.transform.SetAsLastSibling();
            title = Ui.Text("Title", open.transform, new Vector2(0f, -40f), new Vector2(560f, 48f), 36, TextAnchor.MiddleCenter, new Color(0.2f, 0.12f, 0.06f), shadow: false);
            desc = Ui.Text("Desc", open.transform, new Vector2(0f, -86f), new Vector2(560f, 40f), 24, TextAnchor.MiddleCenter, new Color(0.3f, 0.22f, 0.14f), shadow: false);
            stats = Ui.Text("Stats", open.transform, new Vector2(0f, -126f), new Vector2(560f, 36f), 24, TextAnchor.MiddleCenter, new Color(0.3f, 0.22f, 0.14f), shadow: false);
            state = Ui.Text("State", open.transform, new Vector2(0f, -170f), new Vector2(560f, 36f), 24, TextAnchor.MiddleCenter, new Color(0.45f, 0.3f, 0.15f), shadow: false);
            tag = Img("Tag", open.transform, new Vector2(220f, 150f), new Vector2(36f, 16f) * Px, "paper_tag", new Color(1f, 0.9f, 0.55f));
            tag.type = Image.Type.Sliced; tag.pixelsPerUnitMultiplier = Mult;
            tagText = Ui.Text("TagText", tag.transform, new Vector2(6f, 0f), new Vector2(130f, 40f), 24, TextAnchor.MiddleCenter, new Color(0.25f, 0.15f, 0.05f), shadow: false);
            hintBuy = Ui.Text("HintBuy", veil.transform, new Vector2(170f, -300f), new Vector2(800f, 36f), 24, TextAnchor.MiddleCenter, Ui.Dim);
            hintEquip = Ui.Text("HintEquip", veil.transform, new Vector2(170f, -336f), new Vector2(800f, 36f), 24, TextAnchor.MiddleCenter, Ui.Dim);
            veil.gameObject.SetActive(false);
        }

        public void Show()
        {
            veil.gameObject.SetActive(true);
            veil.transform.SetAsLastSibling();
            if (blurCam != null) { blurCam.CopyFrom(Camera.main); blurCam.targetTexture = blurRt; blurCam.enabled = true; }
            if (selected == null) Select(game.Primary, instant: true);
        }
        public void Hide() { veil.gameObject.SetActive(false); if (blurCam != null) blurCam.enabled = false; }

        /// <summary>책자를 고른다: 펼쳐지는 용수철 + 총이 종이 위에 생겨난다</summary>
        void Select(GunKind k, bool instant = false)
        {
            selected = k;
            gunSprite = GunView.LoadArt($"gun_{k.Id}_side") ?? GunView.LoadArt("gun_revolver_side");
            gunWhiteSprite = WhiteOf(gunSprite);
            gunImg.sprite = gunSprite; gunWhite.sprite = gunWhiteSprite;
            var size = gunSprite != null ? new Vector2(gunSprite.rect.width, gunSprite.rect.height) * 3f : new Vector2(300f, 190f);
            gunImg.rectTransform.sizeDelta = size; gunWhite.rectTransform.sizeDelta = size;
            if (instant) { openSx = 1f; gunS = 1f; gunRot = 0f; gunWhiteA = 0f; return; }
            openSx = 0.05f; openSxV = 12f;                 // 책이 펼쳐진다
            gunS = 0.25f; gunSV = 10f; gunRot = Random.Range(-14f, 14f); gunRotV = 0f; gunWhiteA = 1f;   // 총 등장
            tagS = 0.8f; tagSV = 4f;
            Sfx.Play(Sfx.Reload(), 0.6f, 1.3f);
            Sfx.Play(Sfx.Click(), 0.4f, 2f);
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
            Vector2 canvasPt = new Vector2(mp.x - Screen.width * 0.5f, mp.y - Screen.height * 0.5f) * k;
            // 스크롤 (책자가 여섯 권 넘으면 휠로)
            if (mouse != null) { float wheel = mouse.scroll.ReadValue().y; if (Mathf.Abs(wheel) > 0.01f) scrollV += wheel * 90f; }
            Ease.Spring(ref scroll, ref scrollV, Mathf.Clamp(scroll, 0f, Mathf.Max(0f, (books.Count - 5) * 150f)), 200f, 14f, dt);
            // 책자 목록
            Book hot = null;
            foreach (var b in books)
            {
                var pos = b.Home + new Vector2(0f, scroll);
                bool over = Mathf.Abs(canvasPt.x - pos.x) < 100f && Mathf.Abs(canvasPt.y - pos.y) < 62f;
                if (over) hot = b;
                bool owned = game.OwnedGuns.Contains(b.Kind.Id), sel = selected == b.Kind;
                float targetX = sel ? 46f : over ? 22f : 0f;
                Ease.Spring(ref b.X, ref b.XV, targetX, 500f, 16f, dt);
                Ease.Spring(ref b.Scale, ref b.ScaleV, sel ? 1.08f : 1f, 500f, 16f, dt);
                b.Cover.rectTransform.anchoredPosition = pos + new Vector2(b.X, 0f);
                b.Cover.rectTransform.localScale = Vector3.one * b.Scale;
                var tint = b.Kind.IsCursed ? new Color(0.45f, 0.25f, 0.55f) : owned ? new Color(0.75f, 0.5f, 0.28f) : new Color(0.5f, 0.42f, 0.32f);
                b.Cover.color = sel ? Color.Lerp(tint, Color.white, 0.25f) : tint;
                b.Label.color = owned || sel ? new Color(0.15f, 0.1f, 0.06f) : new Color(0.15f, 0.1f, 0.06f, 0.5f);
            }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && hot != null && hot.Kind != selected) { Select(hot.Kind); hot.ScaleV = 3f; }
            // 펼친 책: 가로 용수철
            Ease.Spring(ref openSx, ref openSxV, 1f, 420f, 15f, dt);
            open.rectTransform.localScale = new Vector3(Mathf.Max(0.02f, openSx), 1f, 1f);
            // 총: 용수철 + 흰 실루엣 + 둥실
            Ease.Spring(ref gunS, ref gunSV, 1f, 420f, 14f, dt);
            Ease.Spring(ref gunRot, ref gunRotV, 0f, 300f, 9f, dt);
            gunWhiteA = Mathf.Max(0f, gunWhiteA - dt / 0.14f);
            float bob = Mathf.Sin(Time.unscaledTime * 1.6f) * 6f;
            foreach (var img in new[] { gunImg, gunWhite })
            {
                img.rectTransform.anchoredPosition = new Vector2(0f, 60f + bob);
                img.rectTransform.localScale = Vector3.one * gunS;
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, gunRot + Mathf.Sin(Time.unscaledTime * 1.1f) * 1.5f);
            }
            gunWhite.color = new Color(1f, 1f, 1f, gunWhiteA);
            // 글·가격표·상태
            if (selected != null)
            {
                var g = selected;
                bool owned = game.OwnedGuns.Contains(g.Id), primary = game.Primary == g, secondary = game.Secondary == g;
                string fam = g.Kin switch { GunKind.Family.Revolver => "리볼버", GunKind.Family.Lever => "레버액션", GunKind.Family.Single => "단발", _ => "샷건" };
                title.text = $"<b>{g.Name}</b>";
                desc.text = g.Desc;
                stats.text = $"[{fam}]   {g.Capacity}발 · 장전 {g.ReloadSeconds:0.0}초 · 값 ×{g.ValueScale:0.#} · 튐 ×{g.BounceScale:0.##}";
                state.text = primary ? "주무장" : secondary ? "보조무기" : owned ? "보유" : "";
                bool afford = game.Money >= g.Price;
                tagText.text = owned ? "보유" : $"${g.Price:N0}";
                tag.color = owned ? new Color(0.75f, 0.9f, 0.7f) : afford ? new Color(1f, 0.9f, 0.55f) : new Color(0.9f, 0.6f, 0.55f);
                Ease.Spring(ref tagS, ref tagSV, 1f, 500f, 16f, dt);
                Ease.Spring(ref tagRot, ref tagRotV, -6f, 300f, 10f, dt);
                tag.rectTransform.localScale = Vector3.one * tagS;
                tag.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tagRot);
                hintBuy.text = owned ? "" : afford ? "B 또는 가격표 클릭 = 산다" : "<color=#ff8877>돈이 모자라다</color>";
                hintEquip.text = owned ? "1 = 주무장으로   ·   2 = 보조무기로" : "";
                // 입력: B / 가격표 클릭 = 산다, 1·2 = 장착
                bool tagClick = mouse != null && mouse.leftButton.wasPressedThisFrame && Inside(tag.rectTransform, canvasPt);
                if (!owned && (tagClick || (kb != null && kb.bKey.wasPressedThisFrame))) Buy(g);
                if (owned && kb != null && kb.digit1Key.wasPressedThisFrame) Equip(g, 0);
                if (owned && kb != null && kb.digit2Key.wasPressedThisFrame) Equip(g, 1);
            }
        }

        static bool Inside(RectTransform rt, Vector2 canvasPt)
        {
            // 상자 안의 자식이라 월드 좌표로 비교
            var corners = new Vector3[4]; rt.GetWorldCorners(corners);
            float s = 1080f / Screen.height;
            Vector2 min = new Vector2(corners[0].x - Screen.width * 0.5f, corners[0].y - Screen.height * 0.5f) * s;
            Vector2 max = new Vector2(corners[2].x - Screen.width * 0.5f, corners[2].y - Screen.height * 0.5f) * s;
            return canvasPt.x >= min.x && canvasPt.x <= max.x && canvasPt.y >= min.y && canvasPt.y <= max.y;
        }

        void Buy(GunKind g)
        {
            if (game.Money < g.Price) { Sfx.Play(Sfx.Click(), 0.5f, 0.7f); tagRotV += 60f; return; }
            game.PickGun(g);                                  // 사서 보조무기로
            tagS = 1.5f; tagSV = -4f; tagRot = -20f; tagRotV = 0f; // 가격표가 찍히듯
            gunS = 1.15f; gunSV = 0f; gunWhiteA = 0.6f;
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
