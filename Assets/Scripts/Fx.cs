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
            cam.transform.position = new Vector3(camHome.x + off.x, camHome.y + off.y, camHome.z);
            if (shake <= 0f) cam.transform.position = camHome;
        }
    }
}
