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
        public void SetPlayVisible(bool v) { moneyBoard.gameObject.SetActive(v); hintStrip.gameObject.SetActive(false); cylBoard.gameObject.SetActive(false); slowBg.gameObject.SetActive(v); slowLabel.gameObject.SetActive(v); SetButtonsVisible(v); if (!v) airBadge.gameObject.SetActive(false); }   // 위 안내 띠는 뺐다 (사용자 10-12 UI 정리)

        void Awake()
        {
            var cg = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            var c = cg.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.pixelPerfect = true;   // 도트 규칙: UI도 픽셀에 붙인다
            var s = cg.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920f, 1080f);
            canvas = cg.transform;

            hurtVeil = Ui.Image("Hurt", canvas, Vector2.zero, new Vector2(4000f, 3000f), Color.clear);

            // 왼쪽 위: 돈 판자
            // 돈만 (사용자 10-12 UI 정리: 기록 글자는 뺐다)
            var board = moneyBoard = Ui.Panel("MoneyBoard", canvas, new Vector2(-760f, 470f), new Vector2(340f, 90f));
            money = Ui.Text("Money", board.transform, new Vector2(10f, 0f), new Vector2(300f, 60f), 48, TextAnchor.MiddleLeft, Ui.Gold);
            // 왼쪽 정렬 글자: 기준점을 글자 왼쪽 끝으로 (부풀고 기울어도 자리가 안 밀리게)
            money.rectTransform.pivot = new Vector2(0f, 0.5f); money.rectTransform.anchoredPosition = new Vector2(10f - 150f, 0f);
            stats = Ui.Text("Stats", board.transform, new Vector2(10f, -32f), new Vector2(380f, 34f), 19, TextAnchor.MiddleLeft, Ui.Dim);
            stats.gameObject.SetActive(false);

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
            BuildButtons();
            SetButtonsVisible(false);
        }

        // ---------- 오른쪽 아래 버튼 둘: 스킬트리 / 총 상점 (사용자 10-12) ----------
        Image[] btn = new Image[2]; Text[] btnLabel = new Text[2];
        readonly float[] btnS = { 1f, 1f }, btnSV = { 0f, 0f }, btnRot = { 0f, 0f }, btnRotV = { 0f, 0f };
        static readonly string[] btnNames = { "스킬트리", "총 상점" };
        void BuildButtons()
        {
            for (int i = 0; i < 2; i++)
            {
                btn[i] = Ui.Panel($"Btn{i}", canvas, new Vector2(760f, -470f + i * 84f), new Vector2(300f, 72f), nails: false);
                btnLabel[i] = Ui.Text("Label", btn[i].transform, Vector2.zero, new Vector2(280f, 50f), 24, TextAnchor.MiddleCenter, Ui.Gold);
                btnLabel[i].text = btnNames[i];
            }
        }
        public void SetButtonsVisible(bool v) { foreach (var b in btn) if (b != null) b.gameObject.SetActive(v); }

        /// <summary>첫 흐름 단계 (10-14): 소환 전엔 스킬트리 버튼만, 소환을 열면 돈·숨 게이지, 증표가 생기면 총 상점. 새로 나타나는 버튼은 톡 튀어나온다</summary>
        public void SetStage(bool summon, bool shop)
        {
            moneyBoard.gameObject.SetActive(summon);
            slowBg.gameObject.SetActive(summon); slowLabel.gameObject.SetActive(summon);
            ShowButton(0, true); ShowButton(1, shop);
        }
        void ShowButton(int i, bool v)
        {
            if (btn[i] == null) return;
            if (v && !btn[i].gameObject.activeSelf) { btnS[i] = 0.2f; btnSV[i] = 0f; Sfx.Play(Sfx.Tang(), 0.4f, 1.6f); }
            btn[i].gameObject.SetActive(v);
        }
        /// <summary>시작 노드를 아직 안 열었으면 스킬트리 버튼이 숨 쉬듯 뛴다</summary>
        public bool PulseTree { get; set; }

        Text prompt; float promptA;
        /// <summary>"Space를 눌러 깡통 소환" — 소환을 열고 한참 안 부르면 부드럽게 나타나고, 부르면 부드럽게 사라진다</summary>
        public void SpawnPrompt(bool on, float dt)
        {
            if (prompt == null)
            {
                prompt = Ui.Text("SpawnPrompt", canvas, new Vector2(0f, -330f), new Vector2(1000f, 60f), 36, TextAnchor.MiddleCenter, Ui.Gold);
                prompt.text = "Space를 눌러 깡통 소환";
            }
            promptA = Mathf.MoveTowards(promptA, on ? 1f : 0f, dt / (on ? 0.5f : 0.35f));
            float e = promptA * promptA * (3f - 2f * promptA);
            var c = prompt.color; c.a = e; prompt.color = c;
            prompt.rectTransform.anchoredPosition = new Vector2(0f, -330f + (1f - e) * -16f + Mathf.Round(Mathf.Sin(Time.unscaledTime * 3f)) * 4f * e);
            prompt.gameObject.SetActive(promptA > 0.001f);
        }

        /// <summary>버튼: 올리면 살짝 커지고 기울고, 누르면 납작했다 튕기며 연다. 매 프레임(플레이 중, 가게 닫힘) 부른다</summary>
        public void Buttons(float dtUnscaled, System.Action onTree, System.Action onShop)
        {
            var mouse = UnityEngine.InputSystem.Mouse.current; if (mouse == null) return;
            var mp = mouse.position.ReadValue();
            var pt = new Vector2(mp.x - Screen.width * 0.5f, mp.y - Screen.height * 0.5f) * (1080f / Screen.height);
            for (int i = 0; i < 2; i++)
            {
                if (btn[i] == null || !btn[i].gameObject.activeSelf) continue;
                var rt = btn[i].rectTransform;
                bool over = Mathf.Abs(pt.x - rt.anchoredPosition.x) < rt.sizeDelta.x * 0.5f && Mathf.Abs(pt.y - rt.anchoredPosition.y) < rt.sizeDelta.y * 0.5f;
                float target = over ? 1.08f : 1f;
                if (i == 0 && PulseTree && !over) target = 1f + 0.07f * Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 4f));   // 처음엔 여기를 눌러 보라고
                if (over && mouse.leftButton.wasPressedThisFrame)
                {
                    btnS[i] = 0.9f; btnSV[i] = 6f; btnRot[i] = Random.Range(-4f, 4f);
                    Sfx.Play(Sfx.Click(), 0.5f, 1.4f);
                    if (i == 0) onTree?.Invoke(); else onShop?.Invoke();
                }
                Ease.Spring(ref btnS[i], ref btnSV[i], target, 500f, 16f, dtUnscaled);
                Ease.Spring(ref btnRot[i], ref btnRotV[i], over ? -2f : 0f, 300f, 12f, dtUnscaled);
                rt.localScale = Vector3.one * btnS[i];
                rt.localRotation = Quaternion.Euler(0f, 0f, btnRot[i]);
                btn[i].color = over || (i == 0 && PulseTree && Mathf.Sin(Time.unscaledTime * 4f) > 0.6f) ? Ui.Gold : Ui.BoardEdge;
            }
        }

        /// <summary>돈 판자의 월드 좌표 (탄피가 날아갈 곳)</summary>
        public Vector2 MoneyWorld()
        {
            var a = ((RectTransform)moneyBoard.transform).anchoredPosition * (Screen.height / 1080f);
            var w = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f + a.x, Screen.height * 0.5f + a.y, 10f));
            return w;
        }
        /// <summary>탄피가 닿았다: 돈 글자가 한 번 튄다</summary>
        /// <summary>
        /// 탄피가 닿았다 (사용자 10-13 "쫀득하게"): 돈 글자에 용수철 힘을 툭 준다. 크기는 부풀었다 출렁이며 돌아오고,
        /// 기울기는 닿을 때마다 좌우를 번갈아 받아 연달아 들어오면 흔들흔들한다
        /// </summary>
        public void BumpMoney()
        {
            moneySV += 7f;
            moneyRotV += bumpSign * 120f; bumpSign = -bumpSign;
        }
        float moneyS = 1f, moneySV, moneyRot, moneyRotV, bumpSign = 1f;

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
        /// <param name="k">연출 크기 0..1: 작으면 빠르고 작게, 크면 천천히 크게 (사용자 10-12)</param>
        /// <param name="totalAt">합계 글자의 월드 위치를 돌려준다 (탄피가 거기서 쏟아진다)</param>
        public IEnumerator Tally(System.Collections.Generic.List<(string text, bool lit)> lines, string total, float k, System.Action<Vector2> totalAt)
        {
            var made = new System.Collections.Generic.List<Text>();
            // 왼쪽 아래, 선반 밑 기둥 사이 (사용자 10-12 UX: 세트가 끝날 때 눈이 있는 곳 = 낙하지역. 공중·선반을 안 가린다).
            // 줄은 위→아래, 합계는 모래 띠 바로 위에 가장 크게. 거기서 탄피가 모래로 쏟아지고 돈 판자로 올라간다
            float x = -690f, gap = Mathf.Lerp(34f, 44f, k);
            float sumY = -330f;                                   // 모래 띠(-410) 바로 위
            float top = sumY + Mathf.Lerp(40f, 60f, k) + (lines.Count - 1) * gap;
            int font = Mathf.RoundToInt(Mathf.Lerp(24f, 36f, k)), dimFont = Mathf.RoundToInt(Mathf.Lerp(20f, 28f, k));
            float lineGap = Mathf.Lerp(0.04f, 0.28f, k), popTime = Mathf.Lerp(0.06f, 0.14f, k);
            for (int i = 0; i < lines.Count; i++)
            {
                var (text, lit) = lines[i];
                var t = Ui.Text("Tally", canvas, new Vector2(x, top - i * gap), new Vector2(420f, 44f), lit ? font : dimFont, TextAnchor.MiddleLeft, lit ? Ui.Gold : Ui.Dim);
                t.text = text; made.Add(t);
                Sfx.Play(lit ? Sfx.Tang() : Sfx.Click(), lit ? 0.5f : 0.3f, lit ? 1.3f + i * 0.15f : 1.2f);
                if (lit && k > 0.4f) Fx.Shake(0.1f * k);
                StartCoroutine(Pop(t, lit ? 1.6f + k * 0.8f : 1.3f, popTime));   // 줄은 튀는 동안 다음 줄이 온다 (딱딱딱)
                yield return new WaitForSecondsRealtime(popTime + (lit ? lineGap : lineGap * 0.5f));
            }
            var sum = Ui.Text("TallyTotal", canvas, new Vector2(x, sumY), new Vector2(420f, 90f), Mathf.RoundToInt(Mathf.Lerp(48f, 72f, k)), TextAnchor.MiddleLeft, Ui.Gold);
            sum.text = total; made.Add(sum);
            Sfx.Play(Sfx.Bang(), 0.4f + k * 0.4f, 1.1f);
            Fx.Shake(0.2f + k * 0.5f);
            StartCoroutine(Pop(sum, 2f + k, popTime * 1.3f));
            yield return new WaitForSecondsRealtime(popTime * 1.3f);
            // 합계 글자의 왼쪽 끝 근처 (글자 가운데보다 숫자 쪽)
            var rt = (RectTransform)sum.transform;
            var a = (rt.anchoredPosition + new Vector2(-100f, -20f)) * (Screen.height / 1080f);
            totalAt?.Invoke(Camera.main.ScreenToWorldPoint(new Vector3(Screen.width * 0.5f + a.x, Screen.height * 0.5f + a.y, 10f)));
            yield return new WaitForSecondsRealtime(Mathf.Lerp(0.25f, 0.9f, k));
            StartCoroutine(FadeOut(made));
        }

        IEnumerator FadeOut(System.Collections.Generic.List<Text> made)
        {
            for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime * 3f)
            {
                foreach (var t in made) { if (t == null) continue; var c = t.color; c.a = 1f - k; t.color = c; }
                yield return null;
            }
            foreach (var t in made) if (t != null) Destroy(t.gameObject);
        }

        /// <summary>
        /// 글자 등장 (사용자 10-12, 용수철 문법): 작고 기울어진 채 1프레임에 나타나 넘쳤다 자리 잡는다 (크기·회전 용수철).
        /// from이 클수록(큰 세트) 더 세게 튄다. seconds 동안만 돌리고 손을 뗀다
        /// </summary>
        IEnumerator Pop(Text t, float from, float seconds)
        {
            float s = Mathf.Max(0.2f, 2f - from), sv = (from - 1f) * 18f + 8f;   // 작게 시작해 세게 튀어오른다
            float rot = Random.Range(8f, 14f) * (Random.value < 0.5f ? -1f : 1f), rv = 0f;
            float life = Mathf.Max(seconds * 2.5f, 0.25f);
            for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime / life)
            {
                if (t == null) yield break;
                float dt = Time.unscaledDeltaTime;
                Ease.Spring(ref s, ref sv, 1f, 520f, 15f, dt);
                Ease.Spring(ref rot, ref rv, 0f, 400f, 11f, dt);
                t.transform.localScale = Vector3.one * s;
                t.transform.localRotation = Quaternion.Euler(0f, 0f, rot);
                yield return null;
            }
            t.transform.localScale = Vector3.one; t.transform.localRotation = Quaternion.identity;
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
            // 등장: 작고 기울어진 채 튀어 오른다 (용수철), 그다음 떠오르며 사라진다
            float s = 0.3f, sv = 16f, rot = Random.Range(-12f, 12f), rv = 0f;
            while (time < life)
            {
                float dt = Time.unscaledDeltaTime;
                time += dt;
                float k = time / life;
                var sp = cam.WorldToScreenPoint(world + Vector2.up * (0.3f + k * 0.9f));
                rt.anchoredPosition = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f) * (1080f / Screen.height);
                Ease.Spring(ref s, ref sv, 1f, 560f, 15f, dt);
                Ease.Spring(ref rot, ref rv, 0f, 420f, 11f, dt);
                rt.localScale = Vector3.one * s;
                rt.localRotation = Quaternion.Euler(0f, 0f, rot);
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
            if (game.Money != shownTarget) { shownTarget = game.Money; }
            shown = Mathf.Abs(shownTarget - shown) < 2 ? shownTarget : (long)Mathf.Lerp(shown, shownTarget, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 10f)) + System.Math.Sign(shownTarget - shown);
            // 쫀득한 용수철: 덜 감쇠라 한두 번 출렁이며 선다. 가로로 더 부풀고 세로는 덜 (말랑하게 눌린 느낌)
            float udt = Time.unscaledDeltaTime;
            Ease.Spring(ref moneyS, ref moneySV, 1f, 600f, 10f, udt);
            Ease.Spring(ref moneyRot, ref moneyRotV, 0f, 520f, 9f, udt);
            moneyS = Mathf.Clamp(moneyS, 0.8f, 1.45f); moneyRot = Mathf.Clamp(moneyRot, -14f, 14f);
            float sq = moneyS - 1f;
            money.transform.localScale = new Vector3(1f + sq * 1.15f, 1f + sq * 0.7f, 1f);
            money.transform.localRotation = Quaternion.Euler(0f, 0f, moneyRot);
            money.text = game.DebugMoney ? "$∞  <size=18><color=#ff8888>디버그</color></size>" : $"${shown:N0}" + (game.Tokens > 0 ? $"  <size=24><color=#e8a060>증표 {game.Tokens}</color></size>" : "");
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
