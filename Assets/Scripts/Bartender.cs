using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 술집 주인 (사용자 10-15): 하스스톤 밥처럼 선반(바 카운터) 뒤에서 스르륵 올라와 짧게 말하고 다시 내려간다 (평소엔 없음, 흐름만 잡아 줌).
    /// 평소엔 친절한 바텐더. 사실은 악마 — 플레이어가 주인을 쏘거나(엔터 더 건전 상점 주인), 현상금을 잡으면(영혼을 거둬 감)
    /// 잠깐 본색(빨간 눈·그림자 뿔)이 비쳤다가 바로 돌아온다 (언더테일 플라위처럼, 큰 반전 아님).
    /// 그림은 사용자 손: Assets/Art/bartender.png가 있으면 그걸 쓰고, 없으면 도형 임시 모습.
    /// 선반 윗면 아래는 SpriteMask로 가려서 "카운터 뒤에서 올라오는" 것처럼 보인다.
    /// </summary>
    public class Bartender : MonoBehaviour
    {
        public static Bartender I { get; private set; }
        const float D = 1f / 27f;                    // 1도트
        const float IdleX = 3.9f;                    // 평소 자리: 카운터 오른쪽 (가운데 깡통 자리를 비운다)
        const int Order = -1;                        // 배경(-2) 위, 탄피(1)·깡통(2) 아래 = 깡통이 주인 앞에 보인다
        static float ShelfTop => Look.ShelfY + 0.11f;
        static float UpY => ShelfTop - 8f * D;       // 가슴께까지 보임
        static float DownY => ShelfTop - 48f * D;    // 머리 끝까지 카운터 아래

        Transform body;
        SpriteRenderer[] eyes, horns; SpriteRenderer smile, art, veil;
        BoxCollider2D hitBox;
        float y, yTarget, yV;
        public bool Up => yTarget == UpY && y > ShelfTop - 30f * D;
        bool busy;                                   // 반응 중 (총알을 잡는 중엔 또 안 맞는다)

        // 말풍선 (자기 캔버스: 띠 연출로 HUD가 투명해져도 보인다)
        RectTransform bubble; Text bubbleText; CanvasGroup bubbleGroup; float bubbleA;

        public static Bartender Make()
        {
            var b = new GameObject("Bartender").AddComponent<Bartender>();
            I = b;
            b.Build();
            return b;
        }

        void Build()
        {
            transform.position = new Vector3(IdleX, DownY, 0f);
            y = yTarget = DownY;
            body = new GameObject("Body").transform; body.SetParent(transform, false);
            // 카운터 윗면 위만 보이게
            var maskGo = new GameObject("CounterMask");
            var mask = maskGo.AddComponent<SpriteMask>(); mask.sprite = Look.Square();
            maskGo.transform.position = new Vector3(IdleX, ShelfTop + 3f, 0f);
            maskGo.transform.localScale = new Vector3(6f, 6f, 1f);

            var drawn = GunView.LoadArt("bartender");
            if (drawn != null)
            {
                art = Look.Quad("Art", Vector2.zero, Vector2.one, Color.white, Order, body);
                art.sprite = drawn; art.transform.localScale = Vector3.one;
                art.transform.localPosition = new Vector2(0f, drawn.bounds.extents.y);
                art.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
            else BuildPlaceholder();
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>()) sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

            hitBox = gameObject.AddComponent<BoxCollider2D>(); hitBox.isTrigger = true;
            hitBox.size = new Vector2(24f * D, 30f * D); hitBox.offset = new Vector2(0f, 26f * D);
            hitBox.enabled = false;

            // 화면 어둡게 (두 번째로 쐈을 때)
            veil = Look.Quad("Veil", Vector2.zero, new Vector2(60f, 40f), new Color(0f, 0f, 0f, 0f), 40);
            BuildBubble();
        }

        /// <summary>도형 임시 모습 (바텐더: 올백 머리, 콧수염, 조끼, 나비넥타이). 그림이 오면 대체</summary>
        void BuildPlaceholder()
        {
            var skin = new Color(0.93f, 0.76f, 0.6f); var ink = new Color(0.15f, 0.13f, 0.25f);
            // 같은 정렬 순서 안에서 나중 것이 앞에 오도록 z를 조금씩 카메라 쪽으로 (안 그러면 얼굴이 머리에 묻힌다)
            int k = 0;
            SpriteRenderer Q(string n, float x, float yy, float w, float h, Color c)
            {
                var q = Look.Quad(n, new Vector2(x * D, yy * D), new Vector2(w * D, h * D), c, Order, body);
                q.transform.localPosition += Vector3.back * 0.001f * ++k;
                return q;
            }
            Q("Shirt", 0f, 11f, 26f, 22f, new Color(0.9f, 0.88f, 0.82f));
            Q("VestL", -8f, 11f, 10f, 22f, new Color(0.45f, 0.13f, 0.11f));
            Q("VestR", 8f, 11f, 10f, 22f, new Color(0.45f, 0.13f, 0.11f));
            Q("Tie", 0f, 21f, 7f, 3f, ink);
            Q("Neck", 0f, 24f, 8f, 3f, skin);
            Q("Head", 0f, 32f, 16f, 15f, skin);
            Q("Hair", 0f, 39.5f, 18f, 4f, ink);
            Q("Part", -3f, 41f, 10f, 1f, new Color(0.3f, 0.27f, 0.4f));
            eyes = new[] { Q("EyeL", -4f, 33f, 2f, 2f, ink), Q("EyeR", 4f, 33f, 2f, 2f, ink) };
            Q("Stache", 0f, 28.5f, 12f, 2f, new Color(0.25f, 0.15f, 0.08f));
            smile = Q("Smile", 0f, 26.5f, 6f, 1f, new Color(0.55f, 0.25f, 0.2f));
            // 본색: 그림자 뿔 (평소 투명)
            horns = new[] { Q("HornL", -7f, 44f, 3f, 6f, new Color(0.1f, 0.05f, 0.08f, 0f)), Q("HornR", 7f, 44f, 3f, 6f, new Color(0.1f, 0.05f, 0.08f, 0f)) };
            horns[0].transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            horns[1].transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        }

        void BuildBubble()
        {
            var cg = new GameObject("BartenderCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var c = cg.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 60; c.pixelPerfect = true;
            var sc = cg.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920f, 1080f);
            bubbleGroup = cg.GetComponent<CanvasGroup>(); bubbleGroup.blocksRaycasts = false; bubbleGroup.interactable = false; bubbleGroup.alpha = 0f;
            var panel = Ui.Panel("Bubble", cg.transform, Vector2.zero, new Vector2(620f, 84f), nails: false);
            bubble = panel.rectTransform;
            bubbleText = Ui.Text("Line", panel.transform, Vector2.zero, new Vector2(590f, 70f), 24, TextAnchor.MiddleCenter, Ui.Gold);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Ease.Spring(ref y, ref yV, yTarget, 160f, 14f, dt);
            float bob = Up && !busy ? Mathf.Round(Mathf.Sin(Time.unscaledTime * 2.2f)) * D : 0f;   // 숨쉬기: 1도트
            transform.position = new Vector3(IdleX, y + bob, 0f);
            hitBox.enabled = Up && Game.I != null && Game.I.State == GameState.Play;
            // 말풍선은 머리 위 (화면 좌표로)
            var cam = Camera.main;
            if (cam != null)
            {
                var sp = cam.WorldToScreenPoint(new Vector3(IdleX - 0.6f, y + 62f * D, 0f));
                bubble.anchoredPosition = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f) * (1080f / Screen.height);
            }
            bubbleGroup.alpha = bubbleA;
            if (veil.color.a > 0f) veil.transform.position = cam != null ? (Vector2)cam.transform.position : Vector2.zero;
        }

        // ---------- 움직임 ----------
        public void Rise() { yTarget = UpY; }
        public void Sink() { yTarget = DownY; }
        public IEnumerator RiseCo() { Rise(); Sfx.Play(Sfx.Whoosh(), 0.35f, 0.7f); yield return new WaitForSecondsRealtime(0.5f); }

        /// <summary>한 줄 말한다: 글자가 한 자씩 (딸깍), 다 나오면 잠깐 머물렀다 사라진다</summary>
        public IEnumerator Say(string line, float hold = 1.6f)
        {
            bubbleText.text = "";
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.15f) { bubbleA = t; yield return null; }
            bubbleA = 1f;
            for (int i = 1; i <= line.Length; i++)
            {
                bubbleText.text = line.Substring(0, i);
                if (i % 2 == 0 && line[i - 1] != ' ') Sfx.Play(Sfx.Click(), 0.25f, 1.6f + Random.Range(-0.1f, 0.1f));
                yield return new WaitForSecondsRealtime(line[i - 1] == '.' || line[i - 1] == '…' ? 0.12f : 0.035f);
            }
            yield return new WaitForSecondsRealtime(hold + line.Length * 0.02f);
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.2f) { bubbleA = t; yield return null; }
            bubbleA = 0f;
        }
        /// <summary>올라와서 한마디 하고 다시 내려간다 (10-15 사용자: 평소엔 없고, 흐름만 잡아 주는 역할)</summary>
        public void SayNow(string line) { if (!appearing) StartCoroutine(Appear(line)); }
        bool appearing;
        IEnumerator Appear(string line)
        {
            appearing = true;
            yield return RiseCo();
            yield return Say(line);
            while (busy) yield return null;   // 그새 총에 맞아 반응 중이면 끝날 때까지
            Sink();
            appearing = false;
        }

        // ---------- 본색 ----------
        void Reveal(float k, bool stare)
        {
            var red = new Color(0.95f, 0.12f, 0.1f); var ink = new Color(0.15f, 0.13f, 0.25f);
            if (eyes != null) foreach (var e in eyes) e.color = Color.Lerp(ink, red, k);
            if (horns != null) foreach (var h in horns) { var c = h.color; c.a = k * 0.9f; h.color = c; }
            if (smile != null) smile.enabled = !stare;
            if (art != null) art.color = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.6f), k);
        }

        /// <summary>
        /// 총에 맞았다 (Gun.Fire에서, 깡통을 하나도 안 맞혔을 때만): 총알을 잡아 멈추고 잠깐 본색.
        /// 1번째 = 빨간 눈 한순간, 2번째 = 화면이 잠깐 어두워지고 더 오래, 3번째부터 = 웃지 않고 빤히 본다
        /// </summary>
        public void Shot(Vector2 at)
        {
            if (busy || !Up || Game.I == null) return;
            Game.I.BartenderShots++;
            StartCoroutine(ShotCo(at, Game.I.BartenderShots));
        }
        IEnumerator ShotCo(Vector2 at, int n)
        {
            busy = true;
            // 총알이 코앞에서 멈춘다 (잡혔다)
            var bullet = Look.Quad("CaughtBullet", at, new Vector2(3f * D, 2f * D), new Color(1f, 0.85f, 0.4f), 3);
            Sfx.Play(Sfx.Click(), 0.7f, 0.5f);
            float hold = n == 1 ? 0.18f : n == 2 ? 0.6f : 1.2f;
            Reveal(1f, n >= 3);
            if (n >= 2) StartCoroutine(VeilCo(n == 2 ? 0.35f : 0.5f, hold + 0.4f));
            Fx.Shake(0.2f);
            yield return new WaitForSecondsRealtime(hold);
            if (n < 3) Reveal(0f, false);
            // 총알을 툭 떨어뜨린다
            var rb = bullet.gameObject.AddComponent<Rigidbody2D>(); rb.gravityScale = 2f; rb.angularVelocity = 400f;
            Destroy(bullet.gameObject, 1.2f);
            string line = n == 1 ? "…여기선 깡통만 쏘는 거야." : n == 2 ? "두 번은 말 안 해." : "……";
            yield return Say(line, n >= 3 ? 1.2f : 1.6f);
            Reveal(0f, false);
            busy = false;
        }
        IEnumerator VeilCo(float a, float seconds)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.1f) { veil.color = new Color(0f, 0f, 0f, a * t); yield return null; }
            yield return new WaitForSecondsRealtime(seconds);
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.4f) { veil.color = new Color(0f, 0f, 0f, a * t); yield return null; }
            veil.color = new Color(0f, 0f, 0f, 0f);
        }

        /// <summary>현상금을 잡았다: 영혼 하나가 주인에게 빨려 들어가고, 눈이 잠깐 빨갛게, "잘 보관하지"</summary>
        public void BountyBusted(Vector2 from, bool first = false) => StartCoroutine(SoulCo(from, first));
        IEnumerator SoulCo(Vector2 from, bool first)
        {
            appearing = true;
            yield return new WaitForSecondsRealtime(1.6f);   // 막타 연출이 끝나고
            while (Gun.Cinematic) yield return null;
            if (!Up) { yield return RiseCo(); }
            var soul = Look.Quad("Soul", from, new Vector2(4f * D, 4f * D), new Color(0.75f, 0.9f, 1f, 0.9f), 3);
            Vector2 to = new Vector2(IdleX, y + 32f * D), mid = (from + to) * 0.5f + Vector2.up * 1.5f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.9f)
            {
                float e = t * t * (3f - 2f * t);
                Vector2 p = Vector2.Lerp(Vector2.Lerp(from, mid, e), Vector2.Lerp(mid, to, e), e);
                soul.transform.position = p + Random.insideUnitCircle * D * (1f - t);
                soul.color = new Color(0.75f, 0.9f, 1f, 0.9f * (1f - t * 0.4f));
                yield return null;
            }
            Destroy(soul.gameObject);
            Sfx.Play(Sfx.Whoosh(), 0.4f, 0.5f);
            busy = true;
            Reveal(1f, false);
            yield return new WaitForSecondsRealtime(0.25f);
            Reveal(0f, false);
            busy = false;
            yield return Say("잘했어. 그 영혼은 내가 잘 보관하지.");
            if (first) { yield return ShopIntroCo(true); yield break; }
            Sink();
            appearing = false;
        }

        // ---------- 총 가게 안내 (10-15 사용자: 첫 현상금이 끝나면) ----------
        /// <summary>
        /// 첫 현상금이 끝났다. 놓쳤으면 "그 총으론 무리지" 하며 증표를 하나 주고, 잡았으면 "그 증표로 총을 바꿀 수 있지".
        /// 화면이 조용해지면(공중 깡통·정산 끝) 총 가게를 열어 준다
        /// </summary>
        public void ShopIntro(bool caught) => StartCoroutine(ShopIntroCo(caught));
        IEnumerator ShopIntroCo(bool caught)
        {
            appearing = true;
            var g = Game.I;
            while (g != null && (g.Airborne > 0 || g.SetActive || g.Settling || Gun.Cinematic || g.MenuOpen)) yield return null;
            yield return new WaitForSecondsRealtime(0.4f);
            if (!Up) yield return RiseCo();
            if (!caught)
            {
                yield return Say("그 녹슨 총으론 무리였지. 이거 받아.", 1.0f);
                g.Tokens += 1;
                g.Hud.Float("증표 +1", new Vector2(IdleX, ShelfTop + 2.2f), 10);
                Sfx.Play(Sfx.Tang(), 0.7f, 1.3f);
                yield return new WaitForSecondsRealtime(0.5f);
            }
            yield return Say("증표로 총을 바꿀 수 있어. 한번 골라 봐.", 1.0f);
            Sink();
            appearing = false;
            yield return new WaitForSecondsRealtime(0.4f);
            if (g != null && !g.MenuOpen && g.State == GameState.Play) g.OpenGunShop(GunKind.Revolver);   // 총 가게 (책자), 살 수 있는 리볼버를 펼쳐서
        }
    }
}
