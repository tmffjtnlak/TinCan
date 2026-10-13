using System.Collections;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 현상금 깡통 부착물 (사용자 10-13): 모자·콧수염·권총. 깡통 그림(visual)의 자식이라 납작·회전을 같이 탄다.
    /// 소환 땐 깡통이 뜬 뒤 하나씩 각자 용수철로 붙고(모자는 위에서 툭 씌워짐), 맞을 때마다 일정 확률로 떨어져 나간다.
    /// 떨어진 건 따로 날아가 바닥에 닿으면 납작 → 잠깐 → 작아지며 사라진다 (깡통 바닥 착지와 같은 문법).
    /// 코루틴은 부착물 자신이 돌린다 (깡통 Hit의 StopAllCoroutines에 끊기지 않게).
    /// </summary>
    public class CanProp : MonoBehaviour
    {
        const float Dot = 1f / 27f;
        SpriteRenderer sr;
        Vector3 home;          // 깡통 그림 안 자리
        bool dropFromAbove;    // 모자: 위에서 떨어져 씌워진다
        public bool Detached { get; private set; }

        /// <summary>부착물 하나 만들기. offsetDots = 깡통 가운데에서 도트 단위 (위 +)</summary>
        public static CanProp Make(Transform visual, string art, Vector2 offsetDots, int order, bool dropFromAbove, float delay)
        {
            var sp = GunView.LoadArt(art);
            if (sp == null) return null;
            var go = new GameObject(art);
            go.transform.SetParent(visual, false);
            var p = go.AddComponent<CanProp>();
            p.sr = go.AddComponent<SpriteRenderer>(); p.sr.sprite = sp; p.sr.sortingOrder = order;
            p.home = new Vector3(offsetDots.x * Dot, offsetDots.y * Dot, 0f);
            p.dropFromAbove = dropFromAbove;
            go.transform.localPosition = p.home; go.transform.localScale = Vector3.zero;
            p.StartCoroutine(p.Appear(delay));
            return p;
        }

        /// <summary>윗끝 높이 (깡통 가운데 기준, 월드)</summary>
        public float Top => home.y + sr.sprite.bounds.extents.y;

        IEnumerator Appear(float delay)
        {
            yield return new WaitForSeconds(delay);
            // 크기: 0 → 용수철(살짝 넘침). 모자는 8도트 위에서 떨어져 내려앉으며 납작했다 돌아온다. 기울기 ±20°에서 용수철로 0
            float s = 0f, sv = 0f, y = dropFromAbove ? 8f * Dot : 0f, yv = 0f, rot = (Random.value < 0.5f ? -1f : 1f) * 20f, rv = 0f;
            float sx = 1f, sxv = 0f, sy = 1f, syv = 0f; bool landed = !dropFromAbove;
            for (float t = 0f; t < 0.8f && !Detached; t += Time.deltaTime)
            {
                float dt = Time.deltaTime;
                Ease.Spring(ref s, ref sv, 1f, 900f, 16f, dt);
                Ease.Spring(ref rot, ref rv, 0f, 700f, 14f, dt);
                if (!landed)
                {
                    yv -= 40f * dt; y += yv * dt;
                    if (y <= 0f) { y = 0f; landed = true; sx = 1.3f; sy = 0.7f; sxv = syv = 0f; }   // 씌워지는 순간 납작
                }
                Ease.Spring(ref sx, ref sxv, 1f, 1150f, 18f, dt);
                Ease.Spring(ref sy, ref syv, 1f, 1150f, 23f, dt);
                transform.localScale = new Vector3(s * sx, s * sy, 1f);
                transform.localPosition = home + Vector3.up * y;
                transform.localRotation = Quaternion.Euler(0f, 0f, rot);
                yield return null;
            }
            if (Detached) yield break;
            transform.localScale = Vector3.one; transform.localPosition = home; transform.localRotation = Quaternion.identity;
        }

        /// <summary>떨어져 나간다: hitFrom 반대쪽 + 위로 튀어 돌며 날아가 바닥에서 쉬었다 사라진다</summary>
        public void Detach(Vector2 hitFrom, float power = 1f)
        {
            if (Detached) return;
            Detached = true;
            StopAllCoroutines();
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;
            float dir = Mathf.Sign((transform.position.x - hitFrom.x) + Random.Range(-0.05f, 0.05f));
            var vel = new Vector2(dir * Random.Range(1.2f, 2.6f), Random.Range(3f, 5f)) * power;
            StartCoroutine(Fly(vel, Random.Range(-540f, 540f)));
        }

        IEnumerator Fly(Vector2 vel, float spin)
        {
            float rot = transform.eulerAngles.z;
            var pos = (Vector2)transform.position;
            var size = sr.sprite.bounds.size;
            for (float life = 0f; life < 6f; life += Time.deltaTime)
            {
                float dt = Time.deltaTime * Can.TimeScale;
                vel.y -= 18f * dt; pos += vel * dt; rot += spin * dt;
                float r = rot * Mathf.Deg2Rad;
                float half = Mathf.Abs(Mathf.Cos(r)) * size.y * 0.5f + Mathf.Abs(Mathf.Sin(r)) * size.x * 0.5f;
                if (vel.y < 0f && pos.y - half <= Look.DropY) break;
                transform.position = pos; transform.rotation = Quaternion.Euler(0f, 0f, rot);
                yield return null;
            }
            // 바닥: 가까운 바로 선 자리(0°/180°)로 용수철, 밑면 기준 납작 → 잠깐 → 작아지며 사라짐 (효과 없음)
            float target = Mathf.Round(rot / 180f) * 180f, rv = 0f;
            float sx = 1.3f, sy = 0.7f, sxv = 0f, syv = 0f;
            for (float t = 0f; t < 0.6f; t += Time.deltaTime)
            {
                float dt = Time.deltaTime;
                Ease.Spring(ref sx, ref sxv, 1f, 1150f, 18f, dt);
                Ease.Spring(ref sy, ref syv, 1f, 1150f, 23f, dt);
                Ease.Spring(ref rot, ref rv, target, 900f, 16f, dt);
                float r = rot * Mathf.Deg2Rad;
                float half = Mathf.Abs(Mathf.Cos(r)) * size.y * 0.5f + Mathf.Abs(Mathf.Sin(r)) * size.x * 0.5f;
                transform.localScale = new Vector3(sx, sy, 1f);
                transform.position = new Vector3(pos.x, Look.DropY + half * sy, 0f);
                transform.rotation = Quaternion.Euler(0f, 0f, rot);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            var s0 = transform.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f) { transform.localScale = s0 * (1f - Ease.InCubic(t)); yield return null; }
            Destroy(gameObject);
        }
    }
}
