using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 진열장 (사용자 10-15 UI 컨셉): 연 깡통 종류와 잡은 현상금의 장신구가 뒤쪽 벽 선반에 깡통 소환처럼 나타나 쌓인다 (테두리 없이 배경처럼).
    /// "진열장" 버튼을 누르면 레터박스가 올라오고, 놓인 물건으로 카메라가 줌인 — 왼쪽에 물건, 오른쪽에 설명.
    /// ←/→ (A/D, 휠)로 넘기고 Tab/Esc로 닫는다.
    /// 선반은 임시 나무판 (사용자가 배경에 진열장을 그리면 Slots 좌표만 맞춘다).
    /// </summary>
    public class DisplayCase : MonoBehaviour
    {
        const float D = 1f / 27f;
        // 선반 두 줄 (뒤쪽 벽, 탁상 위): 아래 = 깡통 종류, 위 = 현상금 장신구
        const float CanShelfY = 1.6f, TrophyShelfY = 3.2f, ShelfHalfW = 3.6f, Gap = 0.25f;   // 물건 사이 간격 (폭에 맞춰 나란히, 가운데 정렬)
        const int Order = -1;

        class Item { public string Key, Name, Desc; public CanKind Kind; public Vector2 Pos; public SpriteRenderer Sr; public float W; public int Order; }
        readonly List<Item> items = new List<Item>();
        readonly HashSet<string> placed = new HashSet<string>();
        bool loaded;
        Game game;

        public static DisplayCase Make(Game g)
        {
            var d = new GameObject("DisplayCase").AddComponent<DisplayCase>();
            d.game = g;
            d.BuildShelves();
            d.BuildPanel();
            return d;
        }

        void BuildShelves()
        {
            // 임시 나무 선반 두 장 (그림자 + 판)
            foreach (var y in new[] { CanShelfY, TrophyShelfY })
            {
                Look.Quad("ShelfShadow", new Vector2(0f, y - 0.1f), new Vector2(ShelfHalfW * 2f + 0.2f, 0.12f), new Color(0.35f, 0.2f, 0.14f, 0.5f), Order, transform);
                var p = Look.Quad("ShelfPlank", new Vector2(0f, y - 0.03f), new Vector2(ShelfHalfW * 2f + 0.3f, 0.1f), new Color(0.48f, 0.3f, 0.2f), Order, transform);
                p.transform.localPosition += Vector3.back * 0.001f;
            }
        }

        // ---------- 무엇이 놓이나 ----------
        static readonly (CanKind kind, string effect)[] CanInfo =
        {
            (CanKind.Tin, "기본 깡통. 띄우고, 맞히고, 터뜨린다."),
            (CanKind.Bottle, "작고 무겁다. 깨질 때 값이 크다."),
            (CanKind.Bundle, "터지면 깡통 셋으로 풀린다. 풀린 깡통은 처음 맞기 전까지 느리다."),
            (CanKind.Blue, "터지면 반투명 깡통이 날아오른다."),
            (CanKind.Magnet, "맞을 때마다 옆 깡통을 끌어당긴다."),
        };
        static readonly (string art, string name)[] Trophies =
        {
            ("prop_hat_brown", "갈색 모자"), ("prop_stache_wavy", "꼬불 콧수염"), ("prop_gun", "녹슨 권총"),
            ("prop_hat_white", "흰 모자"), ("prop_stache_flat", "일자 콧수염"),
        };

        bool CanOpen(CanKind k) =>
            k == CanKind.Tin ? game.SummonUnlocked :
            k == CanKind.Bottle ? game.BottleChance > 0f :
            k == CanKind.Bundle ? game.BundleChance > 0f :
            k == CanKind.Blue ? game.BlueChance > 0f :
            k == CanKind.Magnet && game.MagnetChance > 0f;

        void Update()
        {
            if (game == null || game.State != GameState.Play) return;
            // 새로 연 것이 있으면 선반에 나타난다 (불러온 판의 처음엔 조용히 놓기만)
            int ci = 0;
            foreach (var (k, effect) in CanInfo)
            {
                if (!CanOpen(k)) continue;
                var key = "can_" + k.Id;
                if (!placed.Contains(key))
                    Place(new Item { Key = key, Kind = k, Name = k.Name, Desc = effect, Pos = new Vector2(0f, CanShelfY) }, GunView.LoadArt("can_" + k.Id), k.Body);
                ci++;
            }
            for (int i = 0; i < game.BountiesCaught && i < 9; i++)
            {
                var key = "trophy_" + i;
                if (placed.Contains(key)) continue;
                var t = Trophies[i % Trophies.Length];
                Place(new Item { Key = key, Name = $"{i + 1}번째 현상금의 {t.name}", Desc = "잡은 무법자에게서 거둔 장신구. 그 영혼은 주인이 보관한다.", Pos = new Vector2(0f, TrophyShelfY) }, GunView.LoadArt(t.art), new Color(0.6f, 0.4f, 0.25f));
            }
            loaded = true;
            if (viewing) ViewInput();
        }

        /// <summary>선반마다 놓인 순서대로, 물건 폭에 맞춰 나란히 가운데 정렬 (새 물건이 오면 다시 줄 세운다)</summary>
        void Relayout(float shelfY)
        {
            var row = items.FindAll(o => Mathf.Approximately(o.Pos.y, shelfY));
            row.Sort((a, b) => a.Order.CompareTo(b.Order));
            float total = -Gap; foreach (var o in row) total += o.W + Gap;
            float x = -total * 0.5f;
            foreach (var o in row)
            {
                var p = o.Sr.transform.localPosition;
                o.Sr.transform.localPosition = new Vector3(x + o.W * 0.5f, p.y, p.z);
                x += o.W + Gap;
            }
        }
        int nextOrder;

        void Place(Item it, Sprite art, Color fallback)
        {
            placed.Add(it.Key);
            var sr = Look.Quad("Item_" + it.Key, it.Pos, Vector2.one, fallback, Order, transform);
            if (art != null) { sr.sprite = art; sr.color = Color.white; sr.transform.localScale = Vector3.one; }
            else sr.transform.localScale = new Vector3(14f * D, 18f * D, 1f);
            // 선반 위에 서게: 그림 아래 끝을 판에 맞춘다
            float h = art != null ? art.bounds.size.y : 18f * D;
            sr.transform.localPosition = new Vector3(it.Pos.x, it.Pos.y + h * 0.5f, -0.002f);
            sr.color = new Color(sr.color.r * 0.9f, sr.color.g * 0.9f, sr.color.b * 0.9f, sr.color.a);   // 배경처럼 살짝 어둡게
            it.Sr = sr;
            it.W = art != null ? art.bounds.size.x : 14f * D;
            it.Order = nextOrder++;
            items.Add(it);
            Relayout(it.Pos.y);
            items.Sort((a, b) => a.Pos.y != b.Pos.y ? a.Pos.y.CompareTo(b.Pos.y) : a.Sr.transform.position.x.CompareTo(b.Sr.transform.position.x));
            if (loaded) StartCoroutine(SpawnPop(sr));   // 깡통 소환처럼: 구름 + 톡 커졌다 제자리
        }

        IEnumerator SpawnPop(SpriteRenderer sr)
        {
            var home = sr.transform.localScale;
            Fx.Cloud(sr.transform.position, 0.5f);
            Sfx.Play(Sfx.Tang(), 0.4f, 1.5f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                float s = t < 0.5f ? Mathf.Lerp(0f, 1.25f, t / 0.5f) : Mathf.Lerp(1.25f, 1f, (t - 0.5f) / 0.5f);
                sr.transform.localScale = home * s;
                yield return null;
            }
            sr.transform.localScale = home;
        }

        // ---------- 보기 (줌인 + 설명) ----------
        CanvasGroup panelGroup; Text pTitle, pValues, pDesc, pIndex;
        bool viewing; int index; float size0 = -1f;
        Coroutine camCo;

        void BuildPanel()
        {
            var cg = new GameObject("DisplayCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var c = cg.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 55; c.pixelPerfect = true;
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920f, 1080f);
            panelGroup = cg.GetComponent<CanvasGroup>(); panelGroup.alpha = 0f; panelGroup.blocksRaycasts = false;
            var panel = Ui.Panel("Page", cg.transform, new Vector2(470f, 0f), new Vector2(760f, 560f), nails: false);
            pTitle = Ui.Text("Title", panel.transform, new Vector2(0f, 220f), new Vector2(700f, 60f), 40, TextAnchor.MiddleLeft, Ui.Gold);
            pValues = Ui.Text("Values", panel.transform, new Vector2(0f, 130f), new Vector2(700f, 100f), 26, TextAnchor.UpperLeft, Ui.Ink);
            pDesc = Ui.Text("Desc", panel.transform, new Vector2(0f, -40f), new Vector2(700f, 220f), 24, TextAnchor.UpperLeft, Ui.Dim);
            pIndex = Ui.Text("Index", panel.transform, new Vector2(0f, -240f), new Vector2(700f, 40f), 20, TextAnchor.MiddleCenter, Ui.Dim);
            foreach (var t in new[] { pTitle, pValues, pDesc, pIndex }) { t.supportRichText = true; t.horizontalOverflow = HorizontalWrapMode.Wrap; }
        }

        public bool HasItems => items.Count > 0;

        public void Open()
        {
            if (items.Count == 0) return;
            viewing = true;
            Letterbox.Force = true; Letterbox.HoldCamera = true;
            index = Mathf.Clamp(index, 0, items.Count - 1);
            FocusOn(items[index]);
            ShowPage();
        }

        public void Close()
        {
            if (!viewing) return;
            viewing = false;
            Letterbox.Force = false;
            panelGroup.alpha = 0f;
            var cam = Camera.main;
            if (camCo != null) StopCoroutine(camCo);
            camCo = StartCoroutine(CamTo(new Vector3(0f, 0f, -10f), size0, () => { Letterbox.HoldCamera = false; Fx.CamFocus = null; }));
        }

        void FocusOn(Item it)
        {
            var cam = Camera.main;
            if (size0 < 0f) size0 = cam.orthographicSize;
            float size = size0 * 0.42f, halfW = size * cam.aspect;
            // 물건이 화면 왼쪽 1/4쯤에 오게 (오른쪽은 설명)
            var to = new Vector3(it.Sr.transform.position.x + halfW * 0.45f, it.Sr.transform.position.y + 0.1f, -10f);
            if (camCo != null) StopCoroutine(camCo);
            camCo = StartCoroutine(CamTo(to, size, null));
            foreach (var o in items) o.Sr.color = o == it ? Color.white : new Color(0.55f, 0.55f, 0.55f);   // 고른 것만 밝게
        }

        IEnumerator CamTo(Vector3 to, float size, System.Action done)
        {
            var cam = Camera.main;
            Vector3 from = cam.transform.position; float s0 = cam.orthographicSize;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.45f)
            {
                float e = Ease.OutCubic(t);
                var p = Vector3.Lerp(from, to, e);
                cam.transform.position = p; Fx.CamFocus = p;
                cam.orthographicSize = Mathf.Lerp(s0, size, e);
                yield return null;
            }
            cam.transform.position = to; Fx.CamFocus = to; cam.orthographicSize = size;
            if (!viewing) foreach (var o in items) o.Sr.color = new Color(0.9f, 0.9f, 0.9f);
            done?.Invoke();
        }

        void ViewInput()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            int dir = 0;
            if (kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)) dir = 1;
            if (kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)) dir = -1;
            if (mouse != null) { float w = mouse.scroll.ReadValue().y; if (w < -0.01f) dir = 1; else if (w > 0.01f) dir = -1; }
            if (dir != 0 && items.Count > 1)
            {
                index = (index + dir + items.Count) % items.Count;
                FocusOn(items[index]);
                ShowPage();
                Sfx.Play(Sfx.Click(), 0.4f, 1.6f);
            }
            panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, 1f, Time.unscaledDeltaTime / 0.25f);
        }

        void ShowPage()
        {
            var it = items[index];
            pTitle.text = it.Name;
            if (it.Kind != null)
            {
                var k = it.Kind;
                pValues.text = $"터짐  <color=#c08a1e>${game.BurstPay(k):N0}</color>\n떨어짐  ${game.LandPay(k):N0}     체력 {k.Hits}";
            }
            else pValues.text = $"잡은 현상금  {game.BountiesCaught}";
            pDesc.text = it.Desc;
            pIndex.text = $"{index + 1} / {items.Count}     ← →  넘기기     Tab  닫기";
        }
    }
}
