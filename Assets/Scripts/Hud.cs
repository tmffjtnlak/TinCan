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
        float cylSpin, moneyBump;
        Image slowBg, slowFill, slowVeil; Text slowLabel;
        long shown, shownTarget;

        public void Bind(Game g) => game = g;
        public Transform Canvas => canvas;
        Image moneyBoard, hintStrip, cylBoard;
        /// <summary>타이틀에선 돈·안내·실린더 판자를 숨긴다</summary>
        public void SetPlayVisible(bool v) { moneyBoard.gameObject.SetActive(v); hintStrip.gameObject.SetActive(v); cylBoard.gameObject.SetActive(v); }

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

            // 위 가운데: 안내 띠
            var strip = hintStrip = Ui.Panel("HintStrip", canvas, new Vector2(60f, 490f), new Vector2(1000f, 50f), nails: false);
            hint = Ui.Text("Hint", strip.transform, Vector2.zero, new Vector2(980f, 40f), 20, TextAnchor.MiddleCenter, Ui.Dim);

            // 오른쪽 아래: 실린더 (6~12칸이 원으로)
            cylBoard = Ui.Panel("CylBoard", canvas, new Vector2(800f, -440f), new Vector2(260f, 170f));
            cylinder = Ui.Rect("Cylinder", cylBoard.transform, new Vector2(-60f, 0f), new Vector2(120f, 120f));
            chambers = new Image[12];
            for (int i = 0; i < chambers.Length; i++) chambers[i] = Ui.Image($"Ch{i}", cylinder, Vector2.zero, new Vector2(18f, 18f), Ui.Ink);
            gunName = Ui.Text("GunName", cylBoard.transform, new Vector2(55f, 30f), new Vector2(130f, 36f), 24, TextAnchor.MiddleCenter, Ui.Ink);
            shelf = Ui.Text("Shelf", cylBoard.transform, new Vector2(55f, -24f), new Vector2(150f, 64f), 15, TextAnchor.MiddleCenter, Ui.Dim);
            // 슬로우 게이지: 실린더 판자 위에 가로 막대
            slowBg = Ui.Image("SlowBg", cylBoard.transform, new Vector2(0f, 72f), new Vector2(230f, 14f), new Color(0f, 0f, 0f, 0.5f));
            slowFill = Ui.Image("SlowFill", slowBg.transform, Vector2.zero, new Vector2(224f, 8f), new Color(0.5f, 0.8f, 1f));
            slowLabel = Ui.Text("SlowLabel", cylBoard.transform, new Vector2(0f, 94f), new Vector2(230f, 24f), 16, TextAnchor.MiddleCenter, Ui.Dim);
            slowLabel.text = "우클릭 = 느리게";
            slowVeil = Ui.Image("SlowVeil", canvas, Vector2.zero, new Vector2(4000f, 3000f), Color.clear);
            slowVeil.transform.SetAsFirstSibling();
        }

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
            stats.text = $"터뜨림 {game.Bursts}  ·  최고 연속 {game.BestCombo}  ·  최고 정산 ${game.BestPayout:N0}"
                + (air >= 2 ? $"   <color=#ffd966>공중 {air}  ×{air}</color>" : "")
                + (gun.ReloadStreak >= 1 ? $"   <color=#88ffaa>저스트 {gun.ReloadStreak}</color>" : "");
            hint.text = game.MenuOpen ? "" : $"Space = {game.SpawnKind.Name} 소환 (${game.SpawnCost(game.SpawnKind)})   ·   좌클릭 = 쏜다   ·   우클릭 = 느리게   ·   R = 장전   ·   1/2 = 무기   ·   Tab = 가게";
            gunName.text = gun.Kind.Name;
            string p = $"{(game.ActiveSlot == 0 ? "<color=#ffd966>" : "<color=#999999>")}1 {game.Primary.Name} {gun.StoredAmmo(game.Primary)}</color>";
            string s = game.Secondary != null ? $"   {(game.ActiveSlot == 1 ? "<color=#ffd966>" : "<color=#999999>")}2 {game.Secondary.Name} {gun.StoredAmmo(game.Secondary)}</color>" : "   <color=#666666>2 —</color>";
            shelf.supportRichText = true;
            shelf.text = $"{p}{s}\n선반 {game.CansOnShelf}/{game.MaxCans}";
            // 슬로우 게이지와 푸른 화면
            float g = game.SlowGaugeMax > 0f ? game.SlowGauge / game.SlowGaugeMax : 0f;
            slowFill.rectTransform.sizeDelta = new Vector2(224f * g, 8f);
            slowFill.rectTransform.anchoredPosition = new Vector2(-112f + 112f * g, 0f);
            slowFill.color = game.SlowActive ? new Color(0.7f, 0.95f, 1f) : (g >= 0.99f ? new Color(0.6f, 0.9f, 1f) : new Color(0.4f, 0.65f, 0.9f));
            slowVeil.color = Color.Lerp(slowVeil.color, game.SlowActive ? new Color(0.3f, 0.5f, 0.9f, 0.14f) : Color.clear, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 12f));

            // 실린더: 탄창 수만큼 칸을 원으로. 장전 중이면 돈다
            int cap = Mathf.Min(gun.Capacity, chambers.Length);
            float radius = cap <= 6 ? 36f : 46f;
            if (gun.Reloading) cylSpin += Time.unscaledDeltaTime * 540f;
            cylinder.localRotation = Quaternion.Euler(0f, 0f, cylSpin);
            for (int i = 0; i < chambers.Length; i++)
            {
                bool on = i < cap;
                chambers[i].gameObject.SetActive(on);
                if (!on) continue;
                float a = (i / (float)cap) * Mathf.PI * 2f + Mathf.PI * 0.5f;
                chambers[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                chambers[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -cylSpin + 45f);
                bool loaded = i < gun.Ammo;
                chambers[i].color = gun.Reloading ? new Color(1f, 0.8f, 0.3f, 0.5f + 0.5f * Mathf.PingPong(Time.unscaledTime * 6f, 1f))
                    : (loaded ? Ui.Ink : new Color(1f, 1f, 1f, 0.15f));
                chambers[i].rectTransform.sizeDelta = Vector2.one * (cap <= 6 ? 18f : 13f);
            }
        }
    }
}
