using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 코르크 (사용자 10-15): 병이 터지면 코르크 마개가 옆으로 포물선을 그리며 날아간다.
    /// 맞히면 세트와 상관없는 보너스 돈이 바로 들어온다 (배율·세트 정산 밖, 완전히 별개). 못 맞히면 바닥 아래로 사라진다.
    /// 공중 깡통 수·세트 끝 판정에 안 들어간다 (Can이 아니다).
    /// </summary>
    public class Cork : MonoBehaviour
    {
        const float D = 1f / 27f;
        static readonly Color Body = new Color(0.93f, 0.78f, 0.52f), Top = new Color(0.8f, 0.6f, 0.36f), Ink = new Color(0.13f, 0.12f, 0.2f);   // 벽(갈색)과 구분되게 밝게 + 외곽선
        Rigidbody2D rb;
        bool hit;

        /// <summary>보너스 = 병을 터뜨린 값의 절반 (지금 배수 반영)</summary>
        static long Bonus => Game.I != null ? System.Math.Max(1, Game.I.BurstPay(CanKind.Bottle) / 2) : 1;

        public static void Launch(Vector2 from)
        {
            var go = new GameObject("Cork");
            go.transform.position = from + Vector2.up * 0.4f;
            Look.Quad("Outline", new Vector2(0f, 0.5f * D), new Vector2(9f * D, 11f * D), Ink, 3, go.transform);
            var body = Look.Quad("Body", Vector2.zero, new Vector2(7f * D, 8f * D), Body, 3, go.transform);
            body.transform.localPosition += Vector3.back * 0.001f;
            var cap = Look.Quad("Top", new Vector2(0f, 4f * D), new Vector2(7f * D, 2f * D), Top, 3, go.transform);
            cap.transform.localPosition += Vector3.back * 0.002f;
            var c = go.AddComponent<Cork>();
            c.rb = go.AddComponent<Rigidbody2D>();
            c.rb.gravityScale = 1.6f;
            // 옆으로 포물선: 가운데 반대쪽은 화면 밖으로 나가니, 가운데 쪽으로 기울여 날린다
            float dir = from.x > 0.5f ? -1f : from.x < -0.5f ? 1f : (Random.value < 0.5f ? -1f : 1f);
            c.rb.linearVelocity = new Vector2(dir * Random.Range(2.6f, 3.4f), Random.Range(8.5f, 9.5f));
            c.rb.angularVelocity = -dir * Random.Range(500f, 800f);
            var col = go.AddComponent<CircleCollider2D>(); col.isTrigger = true; col.radius = 0.18f;   // 총 판정용 (부딪히지 않는다)
            Sfx.Play(Sfx.Click(), 0.6f, 2.2f);   // 퐁
        }

        void Update()
        {
            if (hit) return;
            if (transform.position.y < Look.DropY - 0.5f || Mathf.Abs(transform.position.x) > Look.PlayHalfW + 1f) Destroy(gameObject);
        }

        /// <summary>총에 맞았다 (Gun.Fire): 보너스 돈이 바로, 반짝 튀며 사라진다</summary>
        public void Shot()
        {
            if (hit || Game.I == null) return;
            hit = true;
            long bonus = Bonus;
            Game.I.AddBonus(bonus);
            Game.I.Hud.Float($"코르크!  +${bonus:N0}", (Vector2)transform.position + Vector2.up * 0.5f, 8);
            Fx.Spark(transform.position, 10, 1.2f);
            Sfx.Play(Sfx.Tang(), 0.7f, 2.4f);
            PlayLog.Event("cork", "", bonus);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.3f, 6f); rb.angularVelocity *= 3f;
            Destroy(gameObject, 0.5f);
        }
    }
}
