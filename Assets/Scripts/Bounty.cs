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
        public int EveryBursts = 5;
        int lastTrigger;
        Can can;
        Image poster; Text title, amount, hp, stamp;
        Image[] pips;
        Transform canvas;
        bool busy;

        public static Bounty Make(Transform canvasRoot)
        {
            var b = new GameObject("Bounty").AddComponent<Bounty>();
            b.canvas = canvasRoot;
            b.BuildPoster();
            return b;
        }

        void BuildPoster()
        {
            // 양피지 포스터: 누런 종이 + 어두운 테두리, 위에 WANTED, 가운데 깡통 그림 자리, 아래 현상금
            var paper = new Color(0.86f, 0.74f, 0.5f);
            var ink = new Color(0.25f, 0.15f, 0.08f);
            poster = Ui.Image("Poster", canvas, new Vector2(-720f, 180f), new Vector2(300f, 400f), ink);
            Ui.Image("Paper", poster.transform, Vector2.zero, new Vector2(288f, 388f), paper);
            Ui.Image("Line1", poster.transform, new Vector2(0f, 120f), new Vector2(250f, 4f), ink);
            Ui.Image("Line2", poster.transform, new Vector2(0f, -110f), new Vector2(250f, 4f), ink);
            title = Ui.Text("Title", poster.transform, new Vector2(0f, 155f), new Vector2(280f, 60f), 44, TextAnchor.MiddleCenter, ink, shadow: false);
            title.text = "WANTED";
            // 깡통 그림 자리: 마름모 틀 (사용자 그림이 들어갈 칸)
            Ui.Diamond("Mug", poster.transform, new Vector2(0f, 10f), 120f, ink, new Color(0.6f, 0.2f, 0.15f));
            hp = Ui.Text("Hp", poster.transform, new Vector2(0f, -64f), new Vector2(280f, 30f), 18, TextAnchor.MiddleCenter, ink, shadow: false);
            amount = Ui.Text("Amount", poster.transform, new Vector2(0f, -150f), new Vector2(280f, 60f), 40, TextAnchor.MiddleCenter, new Color(0.55f, 0.1f, 0.08f), shadow: false);
            pips = new Image[30];
            for (int i = 0; i < pips.Length; i++) pips[i] = Ui.Image($"Pip{i}", poster.transform, new Vector2(-116f + (i % 15) * 16.5f, -88f - (i / 15) * 14f), new Vector2(10f, 10f), ink);
            stamp = Ui.Text("Stamp", poster.transform, Vector2.zero, new Vector2(320f, 100f), 64, TextAnchor.MiddleCenter, new Color(0.7f, 0.08f, 0.05f, 0.9f), shadow: false);
            stamp.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            stamp.text = "";
            poster.gameObject.SetActive(false);
        }

        /// <summary>터뜨린 수가 조건을 넘으면 부른다</summary>
        public void Tick()
        {
            if (busy || Game.I == null || Game.I.State != GameState.Play) return;
            if (Game.I.Bursts > 0 && Game.I.Bursts / EveryBursts > lastTrigger) { lastTrigger = Game.I.Bursts / EveryBursts; Summon(); }
        }

        public void Summon()
        {
            if (busy) return;
            StartCoroutine(SummonCo());
        }

        IEnumerator SummonCo()
        {
            busy = true;
            var kind = CanKind.Bounty;
            long bounty = (long)Mathf.Round(kind.BurstBase * Game.I.ValueMult * Game.I.PayoutMult * Game.I.BurstMult * 0.5f);
            amount.text = $"${bounty:N0}";
            stamp.text = "";
            foreach (var p in pips) p.color = new Color(0.25f, 0.15f, 0.08f);
            // 포스터가 구석에서 돌며 들어온다
            poster.gameObject.SetActive(true);
            var rt = poster.rectTransform;
            Sfx.Play(Sfx.Twirl(), 0.8f, 0.8f);
            Fx.Shake(0.4f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 2.2f)
            {
                float e = 1f - Mathf.Pow(1f - t, 3f);
                rt.anchoredPosition = Vector2.Lerp(new Vector2(-1300f, 700f), new Vector2(-720f, 180f), e);
                rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-540f, -6f, e));
                rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, e);
                yield return null;
            }
            rt.localRotation = Quaternion.Euler(0f, 0f, -6f);
            Game.I.Hud.Float("현상수배!", new Vector2(0f, 2.5f), 10);
            Sfx.Play(Sfx.Bang(), 0.6f, 0.5f);
            yield return new WaitForSeconds(0.5f);
            // 깡통이 선반 가운데에 떨어진다
            can = Can.Spawn(kind, new Vector2(0f, Look.ShelfY + 0.11f + kind.Height * 0.5f));
            Game.I.Cans.Add(can);
            Fx.Shake(0.6f);
            Sfx.Play(Sfx.Drop(), 1f, 0.5f);
            // 살아 있는 동안 체력 표시
            while (can != null && !can.Done)
            {
                int left = can.HitsLeft;
                hp.text = $"남은 {left} / {kind.Hits}";
                for (int i = 0; i < pips.Length; i++) pips[i].color = i < kind.Hits ? (i < left ? new Color(0.25f, 0.15f, 0.08f) : new Color(0.8f, 0.3f, 0.2f, 0.4f)) : Color.clear;
                if (can.Airborne) rt.localRotation = Quaternion.Euler(0f, 0f, -6f + Mathf.Sin(Time.time * 30f) * 1.5f);
                yield return null;
            }
            yield return new WaitForSeconds(1.6f);
            // 포스터 퇴장
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 3f) { rt.anchoredPosition = Vector2.Lerp(new Vector2(-720f, 180f), new Vector2(-1300f, 700f), t * t); yield return null; }
            poster.gameObject.SetActive(false);
            can = null; busy = false;
        }

        /// <summary>현상수배 깡통이 터졌다</summary>
        public void Busted(Can c)
        {
            if (c != can) return;
            stamp.text = "BUSTED";
            StartCoroutine(StampCo());
            Fx.Shake(1.2f);
            Sfx.Play(Sfx.Bang(), 1f, 0.4f);
            Game.I.Hud.Float("현상금 획득!", (Vector2)c.transform.position + Vector2.up * 1.3f, 12);
        }

        /// <summary>떨어져서 놓쳤다</summary>
        public void Escaped(Can c)
        {
            if (c != can) return;
            stamp.text = "ESCAPED";
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
