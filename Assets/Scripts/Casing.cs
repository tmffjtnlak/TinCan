using System.Collections;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 빈 탄피 (사용자 10-12). 깡통이 떨어지거나 터지면 맞춘 발 수만큼 바닥에 후두둑 쏟아진다.
    /// 한 세트가 끝나면 돈 판자로 호로록 날아가고, 닿을 때 돈이 오른다. 세트 안에서는 아직 내 돈이 아니다.
    /// 오른쪽 기둥의 산 탄(노랑·빨강)과 달리 놋쇠색 빈 것. 터뜨린 깡통 것은 금색.
    /// 물리 없이 손으로 떨어뜨린다 (바닥에서 두어 번 튀고 눕는다).
    /// </summary>
    public class Casing : MonoBehaviour
    {
        public long Value;
        public bool Gold;
        SpriteRenderer sr;
        Vector2 vel; float spin, floorY; bool resting, flying;
        static readonly Color Brass = new Color(0.72f, 0.56f, 0.28f), GoldC = new Color(1f, 0.85f, 0.35f);

        public static Casing Drop(Vector2 at, long value, bool gold)
        {
            var go = new GameObject("Casing");
            var c = go.AddComponent<Casing>();
            c.Value = value; c.Gold = gold;
            // 사용자 그림 casing.png (27×14, 10-12). 없으면 도형
            var art = GunView.LoadArt("casing");
            if (art != null)
            {
                c.sr = go.AddComponent<SpriteRenderer>();
                c.sr.sprite = art; c.sr.sortingOrder = 1;
                c.sr.color = gold ? new Color(1f, 1f, 0.8f) : Color.white;
                go.transform.localScale = Vector3.one * (gold ? 0.55f : 0.45f);
            }
            else c.sr = Look.Quad("Brass", Vector2.zero, new Vector2(gold ? 0.3f : 0.24f, 0.1f), gold ? GoldC : Brass, 1, go.transform);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            c.vel = new Vector2(Random.Range(-2.5f, 2.5f), Random.Range(2f, 5f));
            c.spin = Random.Range(-900f, 900f);
            c.floorY = Random.Range(Look.DropY - 0.55f, Look.DropY + 0.25f);   // 모래밭 안에 흩어진다
            return c;
        }

        void Update()
        {
            if (resting || flying) return;
            float dt = Time.deltaTime;
            vel.y -= 22f * dt;
            transform.position += (Vector3)(vel * dt);
            transform.Rotate(0f, 0f, spin * dt);
            if (transform.position.y <= floorY)
            {
                transform.position = new Vector3(transform.position.x, floorY, 0f);
                if (Mathf.Abs(vel.y) < 2f)
                {
                    resting = true; vel = Vector2.zero;
                    transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-12f, 12f) + (Random.value < 0.5f ? 0f : 180f));
                }
                else
                {
                    vel.y = -vel.y * 0.35f; vel.x *= 0.6f; spin *= 0.5f;
                    Sfx.Play(Sfx.Click(), 0.08f, Random.Range(2.2f, 3f));
                }
            }
        }

        /// <summary>돈 판자로 날아간다. 닿으면 onArrive(값) 하고 사라진다</summary>
        public IEnumerator FlyTo(Vector2 target, float delay, System.Action<long> onArrive)
        {
            flying = true;
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            Vector2 from = transform.position;
            float baseScale = transform.localScale.x;
            Vector2 lift = from + Vector2.up * Random.Range(0.6f, 1.4f) + Vector2.right * Random.Range(-0.4f, 0.4f);
            float life = 0.42f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / life)
            {
                float k = t * t;   // 천천히 떴다가 빨라진다
                Vector2 a = Vector2.Lerp(from, lift, t), b = Vector2.Lerp(lift, target, k);
                transform.position = Vector2.Lerp(a, b, t);
                transform.Rotate(0f, 0f, 720f * Time.unscaledDeltaTime);
                transform.localScale = Vector3.one * baseScale * (1f - k * 0.5f);
                yield return null;
            }
            onArrive?.Invoke(Value);
            Destroy(gameObject);
        }
    }
}
