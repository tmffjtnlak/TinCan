using System.Collections;
using UnityEngine;

namespace TinCan
{
    /// <summary>깡통 종류: 값과 크기만 다르다 (비쌀수록 작고 가볍다)</summary>
    [System.Serializable]
    public class CanKind
    {
        public string Name;
        public int Value;          // 공중에서 한 번 맞출 때 기본 돈
        public float Width, Height;
        public float Mass;
        public float Pitch;        // 탱 소리 높이
        public Color Body, Rim, Label;

        public static readonly CanKind Tin = new CanKind { Name = "깡통", Value = 1, Width = 0.5f, Height = 0.7f, Mass = 1f, Pitch = 1f, Body = Look.Tin, Rim = Look.TinDark, Label = Look.Label };
        public static readonly CanKind Bottle = new CanKind { Name = "병", Value = 3, Width = 0.32f, Height = 0.9f, Mass = 0.8f, Pitch = 1.5f, Body = new Color(0.35f, 0.6f, 0.4f, 0.9f), Rim = new Color(0.25f, 0.4f, 0.28f), Label = new Color(0.9f, 0.85f, 0.7f) };
    }

    /// <summary>
    /// 깡통. 총에 맞으면 "탱" 하고 튄다. 공중에서 연속으로 맞으면 콤보, 바닥에 닿으면 떨어진 것.
    /// 떨어진 깡통은 잠시 뒤 울타리 제자리로 돌아온다.
    /// </summary>
    public class Can : MonoBehaviour
    {
        public CanKind Kind { get; private set; }
        public int Combo { get; private set; }       // 이번 공중에서 맞은 횟수
        public bool Airborne { get; private set; }
        public Vector2 Home { get; private set; }
        /// <summary>업그레이드 "가벼운 깡통": 맞을 때 위로 더 튄다</summary>
        public static float BounceBonus;

        Rigidbody2D rb;
        SpriteRenderer body, rim, label;
        Transform visual;
        float flashLeft;
        bool dropped;

        public static Can Spawn(CanKind kind, Vector2 home)
        {
            var go = new GameObject($"Can_{kind.Name}");
            var can = go.AddComponent<Can>();
            can.Build(kind, home);
            return can;
        }

        void Build(CanKind kind, Vector2 home)
        {
            Kind = kind; Home = home;
            transform.position = home;
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.mass = kind.Mass; rb.gravityScale = 2.2f; rb.linearDamping = 0.1f; rb.angularDamping = 0.6f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var col = gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(kind.Width, kind.Height);
            col.sharedMaterial = Mat();
            visual = new GameObject("Visual").transform; visual.SetParent(transform, false);
            body = Look.Quad("Body", Vector2.zero, new Vector2(kind.Width, kind.Height), kind.Body, 2, visual);
            label = Look.Quad("Label", Vector2.zero, new Vector2(kind.Width + 0.02f, kind.Height * 0.4f), kind.Label, 3, visual);
            rim = Look.Quad("RimTop", new Vector2(0f, kind.Height * 0.5f - 0.04f), new Vector2(kind.Width + 0.04f, 0.08f), kind.Rim, 4, visual);
            Look.Quad("RimBot", new Vector2(0f, -kind.Height * 0.5f + 0.04f), new Vector2(kind.Width + 0.04f, 0.08f), kind.Rim, 4, visual);
            Look.Quad("Shine", new Vector2(-kind.Width * 0.3f, 0f), new Vector2(0.06f, kind.Height * 0.8f), new Color(1f, 1f, 1f, 0.35f), 5, visual);
            Rest();
        }

        static PhysicsMaterial2D mat;
        static PhysicsMaterial2D Mat() => mat != null ? mat : (mat = new PhysicsMaterial2D("can") { bounciness = 0.15f, friction = 0.5f });

        /// <summary>울타리 제자리에 선다</summary>
        void Rest()
        {
            rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f;
            transform.position = Home; transform.rotation = Quaternion.identity;
            Airborne = false; dropped = false; Combo = 0;
            visual.localScale = Vector3.one;
        }

        /// <summary>총에 맞았다. hitPoint = 맞은 자리(월드). 맞은 쪽 반대로, 위로 튄다</summary>
        public int Hit(Vector2 hitPoint)
        {
            int earned = 0;
            if (Airborne && !dropped) { Combo++; earned = Kind.Value * Combo; }
            else if (!dropped) { Airborne = true; Combo = 0; }   // 울타리에서 처음 맞음: 띄우기만, 돈은 공중에서부터
            Vector2 off = hitPoint - (Vector2)transform.position;
            float side = Mathf.Clamp(-off.x / Mathf.Max(0.01f, Kind.Width * 0.5f), -1f, 1f);
            var v = rb.linearVelocity;
            v.y = Mathf.Max(0f, v.y * 0.3f) + 9.5f + BounceBonus + Random.Range(-0.5f, 0.8f);
            v.x = v.x * 0.4f + side * 2.2f + Random.Range(-0.6f, 0.6f);
            rb.linearVelocity = v;
            rb.angularVelocity = -side * Random.Range(400f, 800f) + Random.Range(-200f, 200f);
            flashLeft = 0.06f;
            StopAllCoroutines(); StartCoroutine(Squash());
            Sfx.Play(Sfx.Tang(), 0.8f, Kind.Pitch * (1f + Mathf.Min(Combo, 12) * 0.03f));
            return earned;
        }

        IEnumerator Squash()
        {
            float t = 0f;
            while (t < 0.16f)
            {
                t += Time.deltaTime;
                float k = t / 0.16f;
                float s = Mathf.Sin(k * Mathf.PI);
                visual.localScale = new Vector3(1f + s * 0.35f, 1f - s * 0.3f, 1f);
                yield return null;
            }
            visual.localScale = Vector3.one;
        }

        void OnCollisionEnter2D(Collision2D c)
        {
            if (!Airborne || dropped) return;
            if (c.collider.GetComponent<Ground>() != null) Drop();
        }

        void Drop()
        {
            dropped = true;
            Sfx.Play(Sfx.Drop(), 0.6f, Kind.Pitch);
            Game.I?.CanDropped(this);
            StartCoroutine(ReturnHome());
        }

        IEnumerator ReturnHome()
        {
            yield return new WaitForSeconds(1.1f);
            // 스르륵 사라졌다 제자리에
            for (float t = 0f; t < 1f; t += Time.deltaTime * 6f) { visual.localScale = Vector3.one * (1f - t); yield return null; }
            Rest();
            for (float t = 0f; t < 1f; t += Time.deltaTime * 6f) { visual.localScale = Vector3.one * t; yield return null; }
            visual.localScale = Vector3.one;
        }

        void Update()
        {
            if (flashLeft > 0f)
            {
                flashLeft -= Time.deltaTime;
                bool on = flashLeft > 0f;
                body.color = on ? Color.white : Kind.Body;
                label.color = on ? Color.white : Kind.Label;
                rim.color = on ? Color.white : Kind.Rim;
            }
        }
    }
}
