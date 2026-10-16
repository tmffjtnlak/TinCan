using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 현상수배 깡통 (사용자 10-11): 보스가 아니라 체력 높고 무겁고 돈 많고 등장이 요란한 깡통. 규칙은 같다.
    /// 흐름: WANTED 포스터가 구석에서 돌며 들어온다 → 현상금 → 깡통이 선반에 쿵 → 살아 있는 동안 포스터가 구석에 남아 체력 표시
    ///       → 터뜨리면 BUSTED 도장 + 큰 정산, 떨어지면 ESCAPED.
    /// 등장 조건: N번 터뜨릴 때마다 (지금 5). F2 = 바로 부르기(디버그).
    /// </summary>
    public class Bounty : MonoBehaviour
    {
        /// <summary>지난 현상금이 끝난 뒤 터뜨린 수가 이만큼 차면 부른다 (10-14: 5 → 10, 끝난 때부터 다시 센다. 전엔 싸움 중 박치기도 세져서 끝나자마자 또 떴다)</summary>
        public int EveryBursts = 25;   // 10-14 2차: 10 → 25. 10-16: 이제 다 차면 포스터를 쏴서 부른다 (충전 2칸·5초 기준 2분 남짓)
        /// <summary>첫 현상금은 터뜨림 이만큼에서 (10-14: 0:34에 나와 너무 일렀다 → 튜토리얼 흐름의 3분쯤)</summary>
        public int FirstAt = 45;
        /// <summary>도주 시간 (초, 선반에 내려앉은 때부터). 떨어져도 안 끝나고 이 시간이 다 되면 도망간다 (사용자 10-14, 첫 시험값)</summary>
        public float EscapeSeconds = 35f;   // 10-14: 부하를 잡아야만 깎이니 25 → 35
        int nextAt;
        Can can;
        Image poster; Text title, amount, hp, stamp, timer;
        Image[] pips;
        Transform canvas;
        bool busy;

        CanvasGroup group, hudGroup;
        float alpha = 1f, alphaV;

        /// <summary>
        /// 포스터는 HUD(맨 위 오버레이)가 아니라 자기 캔버스에 (사용자 10-14): 카메라 캔버스, 정렬 -1 = 배경(-2) 위·탄피(1)·깡통(2) 아래 → 깡통을 가리지 않는다.
        /// 가게·스킬트리·설정이 열리면, 그리고 우클릭 연출로 HUD가 사라질 때 같이 사라진다
        /// </summary>
        public static Bounty Make(Transform hudCanvas)
        {
            Gun.ShotDamage = 1;
            var b = new GameObject("Bounty").AddComponent<Bounty>();
            var go = new GameObject("BountyCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = Camera.main; c.planeDistance = 5f;
            c.sortingOrder = -1; c.pixelPerfect = true;
            var s = go.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920f, 1080f);
            b.group = go.GetComponent<CanvasGroup>(); b.group.blocksRaycasts = false; b.group.interactable = false;
            b.hudGroup = hudCanvas.GetComponent<CanvasGroup>();
            b.canvas = go.transform;
            b.BuildPoster();
            return b;
        }

        void LateUpdate()
        {
            // 플레이 중이고 메뉴가 닫혀 있을 때만 보인다. 빠르게(0.1초 남짓) 사라지고 나타난다
            var g = Game.I;
            bool show = g != null && g.State == GameState.Play && !g.MenuOpen;
            Ease.Spring(ref alpha, ref alphaV, show ? 1f : 0f, 900f, 60f, Time.unscaledDeltaTime);
            float hud = hudGroup != null ? hudGroup.alpha : 1f;   // 우클릭 연출(HUD 사라짐)도 따른다
            group.alpha = Mathf.Clamp01(alpha) * hud;
        }

        void BuildPoster()
        {
            // 양피지 포스터: 누런 종이 + 어두운 테두리, 위에 WANTED, 가운데 깡통 그림 자리, 아래 현상금
            var paper = new Color(0.86f, 0.74f, 0.5f);
            var ink = new Color(0.25f, 0.15f, 0.08f);
            poster = Ui.Image("Poster", canvas, new Vector2(-720f, 180f), new Vector2(300f, 400f), ink);
            posterGroup = poster.gameObject.AddComponent<CanvasGroup>(); posterGroup.blocksRaycasts = false;
            Ui.Image("Paper", poster.transform, Vector2.zero, new Vector2(288f, 388f), paper);
            Ui.Image("Line1", poster.transform, new Vector2(0f, 120f), new Vector2(250f, 4f), ink);
            Ui.Image("Line2", poster.transform, new Vector2(0f, -110f), new Vector2(250f, 4f), ink);
            title = Ui.Text("Title", poster.transform, new Vector2(0f, 155f), new Vector2(280f, 60f), 44, TextAnchor.MiddleCenter, ink, shadow: false);
            title.text = "WANTED";
            // 깡통 그림 자리: 마름모 틀 (사용자 그림이 들어갈 칸)
            Ui.Diamond("Mug", poster.transform, new Vector2(0f, 10f), 120f, ink, new Color(0.6f, 0.2f, 0.15f));
            hp = Ui.Text("Hp", poster.transform, new Vector2(0f, -62f), new Vector2(280f, 30f), 18, TextAnchor.MiddleCenter, ink, shadow: false);
            // 도주 시간: 포스터 아래에 따로 크게 (10-14: 포스터 안에선 작아서 안 읽혔다)
            timer = Ui.Text("Escape", poster.transform, new Vector2(0f, -232f), new Vector2(320f, 50f), 36, TextAnchor.MiddleCenter, Ui.Gold);
            amount = Ui.Text("Amount", poster.transform, new Vector2(0f, -150f), new Vector2(280f, 60f), 40, TextAnchor.MiddleCenter, new Color(0.55f, 0.1f, 0.08f), shadow: false);
            pips = new Image[30];
            for (int i = 0; i < pips.Length; i++) pips[i] = Ui.Image($"Pip{i}", poster.transform, new Vector2(-116f + (i % 15) * 16.5f, -88f - (i / 15) * 14f), new Vector2(10f, 10f), ink);
            stamp = Ui.Text("Stamp", poster.transform, Vector2.zero, new Vector2(320f, 100f), 64, TextAnchor.MiddleCenter, new Color(0.7f, 0.08f, 0.05f, 0.9f), shadow: false);
            stamp.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            stamp.text = "";
            poster.gameObject.SetActive(false);
        }

        // ---------- 벽의 수배 포스터 (10-16 사용자): 현상금은 자동으로 오지 않는다 ----------
        // 터뜨릴 때마다 포스터가 선명해지고, 다 차면 흔들린다 → 플레이어가 고른 때에 포스터를 쏘면 온다.
        // 놓치면 다시 흐려진다 (잃는 건 시간). 첫 현상금만 우클릭 배우기 끝에 Game이 바로 부른다
        CanvasGroup posterGroup;
        ShootButton target;
        float posterAlpha, posterAlphaV;
        bool told;
        /// <summary>다음 현상금까지 0~1</summary>
        public float Progress => nextAt == 0 ? 0f : Mathf.Clamp01(1f - (nextAt - Game.I.Bursts) / (float)EveryBursts);
        public bool Ready => !busy && Game.I != null && Game.I.FirstBountyDone && nextAt > 0 && Game.I.Bursts >= nextAt;

        public void Tick()
        {
            var g = Game.I;
            if (busy || g == null || g.State != GameState.Play) { if (target != null) target.gameObject.SetActive(false); return; }
            if (!g.FirstBountyDone) return;   // 10-15: 첫 현상금은 우클릭 배우기 끝에 Game이 부른다
            if (nextAt == 0) nextAt = g.Bursts + EveryBursts;   // 이어 하기는 지금부터 한 간격 뒤
            if (target == null) target = ShootButton.Bare("Poster", () => { if (Ready && Calm) Summon(); });
            // 포스터: 흐릿하게(0.25) 있다가 터뜨릴수록 선명해지고(0.75), 다 차면 또렷하게 흔들린다
            if (!poster.gameObject.activeSelf)
            {
                poster.gameObject.SetActive(true);
                var r = poster.rectTransform; r.anchoredPosition = PosterHome; r.localScale = Vector3.one; r.localRotation = Quaternion.Euler(0f, 0f, -6f);
                stamp.text = ""; posterAlpha = 0f;
                foreach (var p in pips) p.color = Color.clear;
            }
            bool ready = Ready;
            float p01 = Progress;
            if (ready && Calm && !told && !g.MenuOpen) { told = true; Bartender.I?.SayNow("수배지가 다 찼군. 준비되면 저걸 쏴."); }   // 처음 한 번만 알려 준다
            Ease.Spring(ref posterAlpha, ref posterAlphaV, ready ? 1f : Mathf.Lerp(0.25f, 0.75f, p01), 120f, 22f, Time.unscaledDeltaTime);
            posterGroup.alpha = Mathf.Clamp01(posterAlpha);
            amount.text = "$ ? ? ?";
            hp.text = ready ? "" : $"터뜨림 {Mathf.Max(0, EveryBursts - (nextAt - g.Bursts))} / {EveryBursts}";
            timer.text = ready ? (Calm ? "쏘면 온다" : "세트가 끝나면") : "";
            timer.color = Ui.Gold;
            var rt = poster.rectTransform;
            rt.localRotation = Quaternion.Euler(0f, 0f, -6f + (ready && Calm ? Mathf.Sin(Time.unscaledTime * 9f) * 3f : 0f));
            rt.localScale = Vector3.one * (ready && Calm ? 1f + Mathf.Max(0f, Mathf.Sin(Time.unscaledTime * 4.5f)) * 0.04f : 1f);
            // 맞는 자리: 포스터 그 자리 (세트 중엔 꺼 둔다 — 저글링하다 실수로 부르지 않게)
            bool live = ready && Calm && !g.MenuOpen;
            target.gameObject.SetActive(live);
            if (live)
            {
                var c = new Vector3[4]; rt.GetWorldCorners(c);
                target.transform.position = (c[0] + c[2]) * 0.5f;
                target.GetComponent<BoxCollider2D>().size = new Vector2(Mathf.Abs(c[2].x - c[0].x), Mathf.Abs(c[2].y - c[0].y));
            }
        }
        /// <summary>세트 중이 아니다 (공중 깡통·정산 없음)</summary>
        static bool Calm => !Game.I.SetActive && !Game.I.Settling && Game.I.Airborne == 0;
        static readonly Vector2 PosterHome = new Vector2(-720f, 180f);

        /// <summary>현상금 등장: 보스 종류 중 하나 (10-14: 보통 / 구르는 무법자 반반)</summary>
        public void Summon() => Summon(!Game.I.FirstBountyDone ? CanKind.BountyFirst : Random.value < 0.5f ? CanKind.Bounty : CanKind.BountyRoller);   // 10-15: 첫 현상금은 체력 8 보통형

        /// <summary>종류를 골라 부른다 (F4 = 체력 3 시험용)</summary>
        public void Summon(CanKind kind)
        {
            if (busy) return;
            StartCoroutine(SummonCo(kind));
        }

        IEnumerator SummonCo(CanKind kind)
        {
            busy = true;
            PlayLog.Event("bounty", kind.Id, 0);
            long bounty = Game.I.BurstPay(kind);   // 10-14: 터뜨림 보상 (맞힐 때 돈은 없다)
            amount.text = $"${bounty:N0}";
            stamp.text = "";
            foreach (var p in pips) p.color = new Color(0.25f, 0.15f, 0.08f);
            var rt = poster.rectTransform;
            if (target != null) target.gameObject.SetActive(false);
            if (poster.gameObject.activeSelf && posterAlpha > 0.5f)
            {
                // 벽의 포스터를 쐈다 (10-16): 그 자리에서 쾅 — 또렷해지며 부풀었다 선다
                posterGroup.alpha = 1f;
                Sfx.Play(Sfx.Bang(), 0.7f, 0.7f);
                Fx.Shake(0.5f);
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 3f)
                {
                    rt.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.18f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, -6f + Mathf.Sin(t * Mathf.PI * 3f) * 10f * (1f - t));
                    yield return null;
                }
                rt.localScale = Vector3.one;
            }
            else
            {
                // 포스터가 구석에서 돌며 들어온다 (첫 현상금·시험 키)
                poster.gameObject.SetActive(true);
                posterGroup.alpha = 1f;
                Sfx.Play(Sfx.Twirl(), 0.8f, 0.8f);
                Fx.Shake(0.4f);
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 2.2f)
                {
                    float e = 1f - Mathf.Pow(1f - t, 3f);
                    rt.anchoredPosition = Vector2.Lerp(new Vector2(-1300f, 700f), PosterHome, e);
                    rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-540f, -6f, e));
                    rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, e);
                    yield return null;
                }
            }
            rt.localRotation = Quaternion.Euler(0f, 0f, -6f);
            Game.I.Hud.Float("현상수배!", new Vector2(0f, 2.5f), 10);
            Sfx.Play(Sfx.Bang(), 0.6f, 0.5f);
            yield return new WaitForSeconds(0.5f);
            // 깡통이 선반 가운데에 떨어진다
            // 선반 가운데가 비어 있으면 가운데, 아니면 빈자리 (10-14: 다른 깡통과 겹치지 않게)
            float bx = 0f;
            foreach (var c in Game.I.Cans) if (c != null && !c.Airborne && !c.Done && Mathf.Abs(c.transform.position.x) < (kind.Width + c.Kind.Width) * 0.5f * (1f - Game.MaxOverlap)) { bx = Game.I.FreeX(kind); break; }
            can = Can.Spawn(kind, new Vector2(bx, Look.ShelfY + 0.11f + kind.Height * 0.5f));
            Game.I.Cans.Add(can);
            // 부하 하나가 같이 내려온다 (사용자 10-14: 현상금전에도 최소 둘은 돌게). 선반 빈자리에
            // 패거리 두목은 셋 (10-16). 공격력도 그동안만 3
            for (int m = 0; m < kind.Minions; m++)
            {
                float mx = Game.I.FreeX(CanKind.Minion);
                Game.I.Cans.Add(Can.Spawn(CanKind.Minion, new Vector2(mx, Look.ShelfY + 0.11f + CanKind.Minion.Height * 0.5f)));
            }
            if (kind == CanKind.BountyGang) Gun.ShotDamage = 3;
            Fx.Shake(0.6f);
            Sfx.Play(Sfx.Drop(), 1f, 0.5f);
            // 살아 있는 동안 체력·도주 시간 표시. 시간이 다 되면 도망간다 (사용자 10-14). 가게가 열렸거나 막타 연출 중엔 멈춘다
            float escape = EscapeSeconds;
            while (can != null && !can.Done)
            {
                int left = can.HitsLeft;
                if (!Game.I.MenuOpen && !Gun.Cinematic) escape -= Time.deltaTime;
                if (escape <= 0f) { can.RunAway(); break; }
                bool hurry = escape < 5f;
                hp.text = $"남은 {left} / {kind.Hits}   ·   다시 튐 {can.ReboundsLeft}";
                timer.text = $"도주까지 {Mathf.CeilToInt(escape)}초";
                timer.color = hurry ? new Color(1f, 0.35f, 0.25f) : Ui.Gold;
                if (hurry) rt.localRotation = Quaternion.Euler(0f, 0f, -6f + Mathf.Sin(Time.time * 40f) * 2.5f);   // 마지막 5초: 포스터가 떤다
                for (int i = 0; i < pips.Length; i++) pips[i].color = i < kind.Hits ? (i < left ? new Color(0.25f, 0.15f, 0.08f) : new Color(0.8f, 0.3f, 0.2f, 0.4f)) : Color.clear;
                if (can.Airborne && !hurry) rt.localRotation = Quaternion.Euler(0f, 0f, -6f + Mathf.Sin(Time.time * 30f) * 1.5f);
                yield return null;
            }
            timer.text = "";
            Gun.ShotDamage = 1;
            yield return new WaitForSeconds(1.6f);
            // 포스터 퇴장
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 3f) { rt.anchoredPosition = Vector2.Lerp(new Vector2(-720f, 180f), new Vector2(-1300f, 700f), t * t); yield return null; }
            poster.gameObject.SetActive(false);
            can = null; busy = false;
            nextAt = Game.I.Bursts + EveryBursts;   // 끝난 때부터 다시 센다
        }

        /// <summary>부하가 터졌다: delay초 뒤 현상금 옆 공중에 새 부하를 불러낸다 (현상금이 살아 있을 때만)</summary>
        public void CallMinion(float delay) => StartCoroutine(CallMinionCo(delay));
        IEnumerator CallMinionCo(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (can == null || can.Done) yield break;
            var at = (Vector2)can.transform.position + new Vector2(Random.value < 0.5f ? -0.8f : 0.8f, 0.3f);
            Can.SpawnAirborne(CanKind.Minion, at, new Vector2(Mathf.Sign(at.x - can.transform.position.x) * 1.5f, 4f), 0f, 1f);
            Fx.Cloud(at, 0.7f);
            Sfx.Play(Sfx.Twirl(), 0.5f, 1.4f);
        }
        /// <summary>현상수배 깡통이 터졌다</summary>
        public void Busted(Can c)
        {
            if (c != can) return;
            stamp.text = "BUSTED";
            StartCoroutine(StampCo());
            Fx.Shake(1.2f);
            Sfx.Play(Sfx.Bang(), 1f, 0.4f);
            Game.I.Tokens += 1;
            PlayLog.Event("busted", c.Kind.Id, 0);
            bool first = !Game.I.FirstBountyDone; Game.I.FirstBountyDone = true; Game.I.BountiesCaught++;
            Bartender.I?.BountyBusted(c.transform.position, first);   // 영혼을 거둬 간다 (10-15). 첫 현상금이면 이어서 총 가게 안내   // 현상금 증표: 잡을 때마다 1개 (도주하면 없음)
            Game.I.Hud.Float("현상금 획득!  증표 +1", (Vector2)c.transform.position + Vector2.up * 1.3f, 12);
        }

        /// <summary>떨어져서 놓쳤다</summary>
        public void Escaped(Can c)
        {
            if (c != can) return;
            stamp.text = "ESCAPED";
            PlayLog.Event("escaped", c.Kind.Id, 0);
            if (!Game.I.FirstBountyDone) { Game.I.FirstBountyDone = true; Bartender.I?.ShopIntro(false); }   // 10-15: 첫 현상금을 놓치면 주인이 "그 총으론 무리" + 증표 하나 + 총 가게
            stamp.color = new Color(0.3f, 0.3f, 0.35f, 0.9f);
            StartCoroutine(StampCo());
        }

        IEnumerator StampCo()
        {
            var t = stamp.transform;
            for (float k = 0f; k < 1f; k += Time.unscaledDeltaTime * 6f) { t.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, k * k); yield return null; }
            t.localScale = Vector3.one;
            stamp.color = stamp.text == "BUSTED" ? new Color(0.7f, 0.08f, 0.05f, 0.9f) : stamp.color;
        }
    }
}
