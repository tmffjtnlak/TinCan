using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 플레이 화면 글자. 왼쪽 위 나무판에 돈·기록, 위 가운데 안내 띠, 오른쪽 아래 실린더(남은 탄),
    /// 떠오르는 +돈 글자. 전부 코드로 만든 UGUI.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        Game game;
        Text money, stats, hint, gunName, shelf;
        Image[] chambers;
        RectTransform cylinder;
        Transform canvas;
        Image hurtVeil;
        float cylSpin, moneyBump, airBump; bool airShown; int lastMult;
        Text airBadge;
        Image slowBg, slowFill, slowVeil; Text slowLabel;
        long shown, shownTarget;

        public void Bind(Game g) => game = g;
        public Transform Canvas => canvas;
        Image moneyBoard, hintStrip, cylBoard;
        /// <summary>타이틀에선 돈·안내·실린더 판자를 숨긴다</summary>
        public void SetPlayVisible(bool v) { moneyBoard.gameObject.SetActive(v); hintStrip.gameObject.SetActive(v); cylBoard.gameObject.SetActive(false); slowBg.gameObject.SetActive(v); slowLabel.gameObject.SetActive(v); if (!v) airBadge.gameObject.SetActive(false); }

        void Awake()
        {
            var cg = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            var c = cg.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
            var s = cg.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920f, 1080f);
            canvas = cg.transform;

            hurtVeil = Ui.Image("Hurt", canvas, Vector2.zero, new Vector2(4000f, 3000f), Color.clear);

            // 왼쪽 위: 돈 판자
            var board = moneyBoard = Ui.Panel("MoneyBoard", canvas, new Vector2(-720f, 455f), new Vector2(420f, 130f));
            money = Ui.Text("Money", board.transform, new Vector2(10f, 18f), new Vector2(380f, 60f), 52, TextAnchor.MiddleLeft, Ui.Gold);
            stats = Ui.Text("Stats", board.transform, new Vector2(10f, -32f), new Vector2(380f, 34f), 19, TextAnchor.MiddleLeft, Ui.Dim);

            // 동시 공중 배지는 세트 점수판(Tally)으로 대체 (사용자 10-12). 만들어만 두고 안 보인다
            airBadge = Ui.Text("AirBadge", canvas, new Vector2(0f, -400f), new Vector2(600f, 120f), 30, TextAnchor.MiddleCenter, Ui.Gold);
            airBadge.gameObject.SetActive(false);
            // 위 가운데: 안내 띠
            var strip = hintStrip = Ui.Panel("HintStrip", canvas, new Vector2(60f, 490f), new Vector2(1180f, 50f), nails: false);
            hint = Ui.Text("Hint", strip.transform, Vector2.zero, new Vector2(1160f, 40f), 19, TextAnchor.MiddleCenter, Ui.Dim);

            // 오른쪽 아래 판자는 뺐다 (사용자 10-11). 총 이름·슬롯 글자는 만들어만 두고 안 보인다
            cylBoard = Ui.Panel("GunBoard", canvas, new Vector2(800f, -470f), new Vector2(260f, 100f));
            cylBoard.gameObject.SetActive(false);
            cylinder = Ui.Rect("Cylinder", cylBoard.transform, Vector2.zero, Vector2.zero);
            chambers = new Image[0];
            gunName = Ui.Text("GunName", cylBoard.transform, new Vector2(0f, 18f), new Vector2(240f, 36f), 24, TextAnchor.MiddleCenter, Ui.Ink);
            shelf = Ui.Text("Shelf", cylBoard.transform, new Vector2(0f, -22f), new Vector2(240f, 50f), 15, TextAnchor.MiddleCenter, Ui.Dim);
            // 슬로우 게이지: 총알 기둥 왼쪽에 세로 막대
            slowBg = Ui.Image("SlowBg", canvas, new Vector2(760f, 0f), new Vector2(12f, 300f), new Color(0f, 0f, 0f, 0.5f));
            slowFill = Ui.Image("SlowFill", slowBg.transform, Vector2.zero, new Vector2(8f, 294f), new Color(0.5f, 0.8f, 1f));
            slowLabel = Ui.Text("SlowLabel", canvas, new Vector2(760f, 170f), new Vector2(120f, 24f), 14, TextAnchor.MiddleCenter, Ui.Dim);
            slowLabel.text = "우클릭";
            slowVeil = Ui.Image("SlowVeil", canvas, Vector2.zero, new Vector2(4000f, 3000f), Color.clear);
            slowVeil.transform.SetAsFirstSibling();
        }

        /// <summary>돈 판자의 월드 좌표 (탄피가 날아갈 곳)</summary>
        public Vector2 MoneyWorld()
        {
            var a = ((RectTransform)moneyBoard.transform).anchoredPosition * (Screen.height / 1080f);
            var w = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f + a.x, Screen.height * 0.5f + a.y, 10f));
            return w;
        }
        /// <summary>탄피가 닿았다: 돈 글자가 한 번 튄다</summary>
        public void BumpMoney() => moneyBump = 1f;

        /// <summary>깡통 위에 붙는 작은 돈 글자 (깡통이 위치를 갱신한다)</summary>
        public Text CanLabel()
        {
            var t = Ui.Text("CanLabel", canvas, Vector2.zero, new Vector2(200f, 30f), 20, TextAnchor.MiddleCenter, new Color(1f, 0.92f, 0.6f));
            t.gameObject.SetActive(false);
            return t;
        }

        /// <summary>정산: 큰 글자로 "+$N (M연속)". 터뜨린 거면 더 크고 "터짐!"</summary>
        public void Payout(long amount, int combo, Vector2 world, bool burst)
        {
            if (burst) Float($"터짐!  +${amount:N0}", world + Vector2.up * 0.6f, 10);
            else Float(combo >= 3 ? $"+${amount:N0}  {combo}연속" : $"+${amount:N0}", world + Vector2.up * 0.8f, Mathf.Max(combo, 4));
        }

        /// <summary>
        /// 세트 점수판 (사용자 10-12: 낚시 게임/오버워치 점수 느낌). 화면 가운데에 한 줄씩 딱, 딱, 딱 찍히고
        /// 마지막에 합계가 크게 쾅. 잠깐 머물렀다가 사라진다. ×1인 줄은 흐리고 작게.
        /// </summary>
        public IEnumerator Tally(System.Collections.Generic.List<(string text, bool lit)> lines, string total)
        {
            var made = new System.Collections.Generic.List<Text>();
            float top = 140f, gap = 58f;
            for (int i = 0; i < lines.Count; i++)
            {
                var (text, lit) = lines[i];
                var t = Ui.Text("Tally", canvas, new Vector2(0f, top - i * gap), new Vector2(900f, 60f), lit ? 44 : 30, TextAnchor.MiddleCenter, lit ? Ui.Gold : Ui.Dim);
                t.text = text; made.Add(t);
                Sfx.Play(lit ? Sfx.Tang() : Sfx.Click(), lit ? 0.6f : 0.3f, lit ? 1.3f + i * 0.15f : 1.2f);
                if (lit) Fx.Shake(0.15f);
                yield return Pop(t, lit ? 2.2f : 1.4f, 0.12f);
                yield return new WaitForSecondsRealtime(lit ? 0.22f : 0.1f);
            }
            var sum = Ui.Text("TallyTotal", canvas, new Vector2(0f, top - lines.Count * gap - 30f), new Vector2(900f, 110f), 88, TextAnchor.MiddleCenter, Ui.Gold);
            sum.text = total; made.Add(sum);
            Sfx.Play(Sfx.Bang(), 0.7f, 1.1f);
            Fx.Shake(0.6f);
            yield return Pop(sum, 3f, 0.16f);
            yield return new WaitForSecondsRealtime(0.7f);
            for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime * 3f)
            {
                foreach (var t in made) { if (t == null) continue; var c = t.color; c.a = 1f - k; t.color = c; t.transform.localScale = Vector3.one * (1f - k * 0.2f); }
                yield return null;
            }
            foreach (var t in made) if (t != null) Destroy(t.gameObject);
        }

        IEnumerator Pop(Text t, float from, float seconds)
        {
            for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / seconds)
            {
                float e = 1f - (1f - k) * (1f - k);
                t.transform.localScale = Vector3.one * Mathf.Lerp(from, 1f, e);
                yield return null;
            }
            t.transform.localScale = Vector3.one;
        }

        /// <summary>저주 맞음: 화면이 보라색으로 확 어두워졌다 돌아온다</summary>
        public void Hurt() => StartCoroutine(HurtCo());
        IEnumerator HurtCo()
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 2.5f) { hurtVeil.color = new Color(0.4f, 0f, 0.5f, 0.45f * (1f - t)); yield return null; }
            hurtVeil.color = Color.clear;
        }

        /// <summary>떠오르는 글자 (월드 위치에서). 콤보가 높을수록 크고 노랗다. 튀어나왔다 줄어든다</summary>
        public void Float(string msg, Vector2 world, int combo)
        {
            var t = Ui.Text("Float", canvas, Vector2.zero, new Vector2(500f, 70f), 26 + Mathf.Min(combo, 10) * 3, TextAnchor.MiddleCenter,
                Color.Lerp(Ui.Ink, Ui.Gold, Mathf.Clamp01(combo / 8f)));
            t.text = msg;
            StartCoroutine(FloatCo(t, world));
        }

        IEnumerator FloatCo(Text t, Vector2 world)
        {
            var rt = (RectTransform)t.transform;
            float life = 0.75f, time = 0f;
            var cam = Camera.main;
            while (time < life)
            {
                time += Time.unscaledDeltaTime;
                float k = time / life;
                var sp = cam.WorldToScreenPoint(world + Vector2.up * (0.3f + k * 0.9f));
                rt.anchoredPosition = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f) * (1080f / Screen.height);
                float pop = k < 0.15f ? 1f + (0.15f - k) * 4f : 1f;
                rt.localScale = Vector3.one * pop;
                var c = t.color; c.a = 1f - k * k; t.color = c;
                yield return null;
            }
            Destroy(t.gameObject);
        }

        void Update()
        {
            if (game == null || game.State != GameState.Play) return;
            var gun = game.Gun;
            // 돈: 굴러 올라가고, 오를 때 한 번 튄다
            if (game.Money != shownTarget) { shownTarget = game.Money; if (shownTarget > shown) moneyBump = 1f; }
            shown = Mathf.Abs(shownTarget - shown) < 2 ? shownTarget : (long)Mathf.Lerp(shown, shownTarget, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f)) + System.Math.Sign(shownTarget - shown);
            moneyBump = Mathf.Max(0f, moneyBump - Time.unscaledDeltaTime * 5f);
            money.transform.localScale = Vector3.one * (1f + moneyBump * 0.18f);
            money.text = game.DebugMoney ? "$∞  <size=18><color=#ff8888>디버그</color></size>" : $"${shown:N0}";
            money.supportRichText = true;
            int air = game.Airborne;
            stats.supportRichText = true;
            stats.text = $"최고 세트 ${game.BestPayout:N0}  ·  터뜨림 {game.Bursts}  ·  최고 연속 {game.BestCombo}"
                + (game.SetActive ? $"   <color=#ffd966>이번 세트 ${game.SetPending:N0}</color>" : "")
                + (gun.ReloadStreak >= 1 ? $"   <color=#88ffaa>저스트 {gun.ReloadStreak}</color>" : "");
            hint.text = game.MenuOpen ? "" : $"Space = {game.SpawnKind.Name} 소환 (${game.SpawnCost(game.SpawnKind)}, 공중이 비어 있을 때)   ·   좌클릭 = 쏜다   ·   우클릭 = 느리게   ·   R = 장전   ·   1/2 = 무기   ·   Tab = 가게   ·   <color=#ffd966>둘 띄우면 ×3</color>";
            gunName.text = gun.Kind.Name;
            string p = $"{(game.ActiveSlot == 0 ? "<color=#ffd966>" : "<color=#999999>")}1 {game.Primary.Name} {gun.StoredAmmo(game.Primary)}</color>";
            string s = game.Secondary != null ? $"   {(game.ActiveSlot == 1 ? "<color=#ffd966>" : "<color=#999999>")}2 {game.Secondary.Name} {gun.StoredAmmo(game.Secondary)}</color>" : "   <color=#666666>2 —</color>";
            shelf.supportRichText = true;
            shelf.text = $"{p}{s}\n선반 {game.CansOnShelf}/{game.MaxCans}";
            // 슬로우 게이지(세로)와 푸른 화면
            float g = game.SlowGaugeMax > 0f ? game.SlowGauge / game.SlowGaugeMax : 0f;
            slowFill.rectTransform.sizeDelta = new Vector2(8f, 294f * g);
            slowFill.rectTransform.anchoredPosition = new Vector2(0f, -147f + 147f * g);
            slowFill.color = game.SlowActive ? new Color(0.7f, 0.95f, 1f) : (g >= 0.99f ? new Color(0.6f, 0.9f, 1f) : new Color(0.4f, 0.65f, 0.9f));
            slowVeil.color = Color.Lerp(slowVeil.color, game.SlowActive ? new Color(0.3f, 0.5f, 0.9f, 0.14f) : Color.clear, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));

        }
    }
}
