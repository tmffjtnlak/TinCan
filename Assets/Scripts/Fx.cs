using System.Collections;
using UnityEngine;

namespace TinCan
{
    /// <summary>타격감 효과 모음. 전부 도형이라 그림이 와도 그대로 쓴다. 불꽃·먼지·링·동전·흔들림.</summary>
    public class Fx : MonoBehaviour
    {
        static Fx inst;
        static Fx I => inst != null ? inst : (inst = new GameObject("Fx").AddComponent<Fx>());
        static Vector3 camHome; static float shake, shakeDecay = 6f;
        /// <summary>흔들림의 기준 위치. 연출(현상금 막타)이 카메라를 옮기는 동안 그 자리를 넣어 둔다. null이면 화면 가운데</summary>
        public static Vector3? CamFocus;

        // ---------- 소환 구름 (사용자 10-13): 흰 도트 원 여러 개가 튕기며 부풀었다 위로 떠오르며 줄어든다 ----------
        // 원은 반지름마다 따로 찍은 도트 원 그림을 갈아 끼운다 → 크기가 변해도 늘 도트 (늘려서 키우지 않는다)
        static Sprite[] discs;
        static Sprite Disc(int r)
        {
            if (discs == null) discs = new Sprite[16];
            r = Mathf.Clamp(r, 1, discs.Length - 1);
            if (discs[r] != null) return discs[r];
            int n = r * 2 + 1;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = x - r, dy = y - r;
                    tex.SetPixel(x, y, dx * dx + dy * dy <= r * r + r * 0.6f ? Color.white : Color.clear);
                }
            tex.Apply();
            return discs[r] = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 27f);
        }

        /// <summary>흰 구름 한 뭉치: 가운데 큰 원 + 둘레 작은 원 5개. 깡통(2)과 탄피(1)보다 뒤(0)</summary>
        public static void Cloud(Vector2 at, float size = 1f)
        {
            // 10-13 2차: 지속 시간 2배 (부풂 0.1 → 0.2초, 용수철 세기 ÷4·감쇠 ÷2 = 같은 출렁임을 2배 시간에, 떠오름 ÷2), 크기는 Cloud 호출에서 ×1.2
            I.StartCoroutine(I.PuffCo(at, 9f * size, 0f, Vector2.up * 0.25f));
            for (int i = 0; i < 5; i++)
            {
                float a = (i * 72f + Random.Range(-18f, 18f)) * Mathf.Deg2Rad;
                var off = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.75f) * Random.Range(0.28f, 0.4f) * size;
                I.StartCoroutine(I.PuffCo(at + off, Random.Range(4.5f, 7f) * size, i * 0.03f, off * 0.9f + Vector2.up * 0.2f));
            }
        }

        IEnumerator PuffCo(Vector2 at, float maxR, float delay, Vector2 drift)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            var sr = new GameObject("Puff").AddComponent<SpriteRenderer>(); sr.sortingOrder = 0; sr.color = Color.white;
            float r = 0f, v = 0f, t = 0f; Vector2 pos = at;
            while (true)
            {
                float dt = Time.deltaTime; t += dt;
                bool grow = t < 0.2f;
                // 부풀 땐 빠르고 살짝 넘치게, 줄 땐 느긋하게 (용수철)
                Ease.Spring(ref r, ref v, grow ? maxR : 0f, grow ? 362f : 62f, grow ? 10.5f : 9f, dt);
                if (!grow) pos += drift * dt * 1.3f;
                int ri = Mathf.RoundToInt(r);
                if (!grow && ri < 1) break;
                sr.sprite = ri >= 1 ? Disc(ri) : null;
                sr.transform.position = new Vector3(Mathf.Round(pos.x * 27f) / 27f, Mathf.Round(pos.y * 27f) / 27f, 0f);   // 도트 칸에 붙인다
                if (t > 1.8f) break;
                yield return null;
            }
            Destroy(sr.gameObject);
        }

        /// <summary>맞은 자리 불꽃: 노란 조각이 사방으로 짧게</summary>
        public static void Spark(Vector2 at, int count = 6, float power = 1f)
        {
            for (int i = 0; i < count; i++) I.StartCoroutine(I.SparkCo(at, power));
        }

        IEnumerator SparkCo(Vector2 at, float power)
        {
            var dir = Random.insideUnitCircle.normalized;
            var s = Look.Quad("Spark", at, new Vector2(0.18f * power, 0.04f), new Color(1f, 0.95f, 0.6f), 20);
            s.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            float speed = Random.Range(5f, 9f) * power;
            for (float t = 0f; t < 1f; t += Time.deltaTime * 7f)
            {
                s.transform.position += (Vector3)(dir * speed * Time.deltaTime * (1f - t));
                s.transform.localScale = new Vector3(0.18f * power * (1f - t * 0.5f), 0.04f, 1f);
                s.color = new Color(1f, 0.95f - t * 0.5f, 0.6f - t * 0.6f, 1f - t);
                yield return null;
            }
            Destroy(s.gameObject);
        }

        /// <summary>먼지: 갈색 동그라미 몇 개가 옆으로 퍼지며 커졌다 사라진다 (떨어질 때, 선반에 앉을 때)</summary>
        public static void Dust(Vector2 at, int count = 5, float power = 1f)
        {
            for (int i = 0; i < count; i++) I.StartCoroutine(I.DustCo(at, power));
        }

        IEnumerator DustCo(Vector2 at, float power)
        {
            var d = Look.Quad("Dust", at + new Vector2(Random.Range(-0.2f, 0.2f), 0f), Vector2.one * 0.1f, new Color(0.75f, 0.6f, 0.4f, 0.7f), 19);
            var vel = new Vector2(Random.Range(-1.5f, 1.5f) * power, Random.Range(0.3f, 1.2f) * power);
            for (float t = 0f; t < 1f; t += Time.deltaTime * 2.5f)
            {
                d.transform.position += (Vector3)(vel * Time.deltaTime); vel.y -= Time.deltaTime * 2f;
                d.transform.localScale = Vector3.one * (0.1f + t * 0.3f * power);
                d.color = new Color(0.75f, 0.6f, 0.4f, 0.7f * (1f - t));
                yield return null;
            }
            Destroy(d.gameObject);
        }

        /// <summary>링: 네모 테두리가 커지며 사라진다 (터질 때)</summary>
        public static void Ring(Vector2 at, Color color, float size = 2f) => I.StartCoroutine(I.RingCo(at, color, size));

        IEnumerator RingCo(Vector2 at, Color color, float size)
        {
            var root = new GameObject("Ring").transform; root.position = at; root.rotation = Quaternion.Euler(0f, 0f, 45f);
            var sides = new SpriteRenderer[4];
            for (int i = 0; i < 4; i++) sides[i] = Look.Quad("Side", Vector2.zero, Vector2.one, color, 21, root);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 4f)
            {
                float r = Mathf.Lerp(0.2f, size, Mathf.Sqrt(t)); float th = 0.08f * (1f - t) + 0.02f;
                sides[0].transform.localPosition = new Vector2(0f, r); sides[0].transform.localScale = new Vector3(r * 2f, th, 1f);
                sides[1].transform.localPosition = new Vector2(0f, -r); sides[1].transform.localScale = new Vector3(r * 2f, th, 1f);
                sides[2].transform.localPosition = new Vector2(r, 0f); sides[2].transform.localScale = new Vector3(th, r * 2f, 1f);
                sides[3].transform.localPosition = new Vector2(-r, 0f); sides[3].transform.localScale = new Vector3(th, r * 2f, 1f);
                var c = color; c.a = 1f - t; foreach (var s in sides) s.color = c;
                yield return null;
            }
            Destroy(root.gameObject);
        }

        /// <summary>동전: 노란 조각들이 튀어 올랐다 떨어지며 사라진다. 금액이 클수록 많이 (로그)</summary>
        public static void Coins(Vector2 at, long amount)
        {
            int n = Mathf.Clamp(3 + Mathf.RoundToInt(Mathf.Log10(Mathf.Max(1, amount)) * 5f), 3, 30);
            for (int i = 0; i < n; i++) I.StartCoroutine(I.CoinCo(at));
        }

        IEnumerator CoinCo(Vector2 at)
        {
            var c = Look.Quad("Coin", at, new Vector2(0.14f, 0.14f), new Color(1f, 0.85f, 0.3f), 22);
            var vel = new Vector2(Random.Range(-3f, 3f), Random.Range(4f, 8f));
            float spin = Random.Range(-720f, 720f), life = Random.Range(0.6f, 1f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / life)
            {
                c.transform.position += (Vector3)(vel * Time.deltaTime); vel.y -= Time.deltaTime * 18f;
                c.transform.rotation = Quaternion.Euler(0f, 0f, spin * t);
                c.transform.localScale = new Vector3(0.14f * Mathf.Abs(Mathf.Cos(t * 12f)), 0.14f, 1f);   // 뒤집히는 느낌
                c.color = new Color(1f, 0.85f, 0.3f, t < 0.7f ? 1f : (1f - t) / 0.3f);
                yield return null;
            }
            Destroy(c.gameObject);
        }

        /// <summary>조준점 반응: 흰 링이 조준점에서 퍼진다 (맞췄다는 표시)</summary>
        public static void HitMark(Vector2 at) => I.StartCoroutine(I.RingCo(at, new Color(1f, 1f, 1f, 0.8f), 0.5f));

        /// <summary>화면 흔들림 (세기 0~1). 겹치면 더 세게</summary>
        public static void Shake(float strength) { I.enabled = true; shake = Mathf.Min(1.5f, shake + strength); }

        void Awake() { camHome = new Vector3(0f, 0f, -10f); }

        void LateUpdate()
        {
            var cam = Camera.main; if (cam == null) return;
            if (shake <= 0f) return;
            shake = Mathf.Max(0f, shake - Time.unscaledDeltaTime * shakeDecay);
            var off = Random.insideUnitCircle * shake * 0.15f;
            var home = CamFocus ?? camHome;
            cam.transform.position = new Vector3(home.x + off.x, home.y + off.y, home.z);
            if (shake <= 0f) cam.transform.position = home;
        }
    }
}
