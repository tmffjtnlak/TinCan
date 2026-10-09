using System.Collections;
using UnityEngine;

namespace TinCan
{
    /// <summary>깡통 종류: 값·크기·소환 비용. 비쌀수록 작고 가볍다</summary>
    [System.Serializable]
    public class CanKind
    {
        public string Name;
        public int Value;          // 공중에서 한 번 맞출 때 기본 돈
        public int Cost;           // 소환 비용
        public float Width, Height;
        public float Mass;
        public float Pitch;        // 탱 소리 높이
        public Color Body, Rim, Label;
        public long Price;         // 깡통류 구매 값 (0 = 처음부터)
        public string Desc;
        // 깡통마다 붙는 기능 (2주 안엔 하나만)
        public int LandBounces;    // 낙하지역에 닿으면 이 횟수만큼 혼자 다시 튀어오른다
        /// <summary>체력 = 몇 번 맞으면 터지나 (사용자 10-10: 무한 저글링을 막고, 다 맞춰 터뜨리면 최고치). 게임 한 문장: 튕겨서 터뜨린다</summary>
        public int Hits;

        public static readonly CanKind Tin = new CanKind { Id = "tin", Name = "깡통", Desc = "값 1. 6번 맞으면 터진다.", Value = 1, Cost = 2, Price = 0, Hits = 6, Width = 0.5f, Height = 0.7f, Mass = 1f, Pitch = 1f, Body = Look.Tin, Rim = Look.TinDark, Label = Look.Label };
        public static readonly CanKind Bottle = new CanKind { Id = "bottle", Name = "병", Desc = "값 3. 작고 가볍다. 4번이면 깨진다.", Value = 3, Cost = 8, Price = 60, Hits = 4, Width = 0.32f, Height = 0.9f, Mass = 0.8f, Pitch = 1.5f, Body = new Color(0.35f, 0.6f, 0.4f, 0.9f), Rim = new Color(0.25f, 0.4f, 0.28f), Label = new Color(0.9f, 0.85f, 0.7f) };
        public static readonly CanKind Rubber = new CanKind { Id = "rubber", Name = "고무 깡통", Desc = "값 2. 떨어지면 혼자 한 번 다시 튄다. 8번이면 터진다.", Value = 2, Cost = 6, Price = 200, Hits = 8, LandBounces = 1, Width = 0.5f, Height = 0.7f, Mass = 0.7f, Pitch = 0.85f, Body = new Color(0.9f, 0.45f, 0.5f), Rim = new Color(0.55f, 0.2f, 0.25f), Label = new Color(1f, 0.9f, 0.5f) };
        public static readonly CanKind Hat = new CanKind { Id = "hat", Name = "모자", Desc = "값 10. 납작하고 아주 가볍다. 10번이면 찢어진다.", Value = 10, Cost = 30, Price = 400, Hits = 10, Width = 0.7f, Height = 0.36f, Mass = 0.5f, Pitch = 0.7f, Body = new Color(0.35f, 0.22f, 0.14f), Rim = new Color(0.2f, 0.12f, 0.08f), Label = new Color(0.8f, 0.65f, 0.3f) };
        public string Id;

        /// <summary>깡통류 가게 목록 (순서대로)</summary>
        public static readonly System.Collections.Generic.List<CanKind> ForSale = new System.Collections.Generic.List<CanKind> { Tin, Bottle, Rubber, Hat };
    }

    /// <summary>
    /// 깡통. 선반 위에서 시작. 총에 맞으면 "탱" 하고 튀고(그때부터 공중), 공중에서 맞을 때마다 값 × 연속이 쌓인다.
    /// 낙하지역에 닿으면 쌓인 돈을 정산하고 사라진다. 돈은 떨어져야 들어온다.
    /// </summary>
    public class Can : MonoBehaviour
    {
        public CanKind Kind { get; private set; }
        public int Combo { get; private set; }       // 공중에서 맞은 횟수 (띄운 한 발 포함)
        public long Earned { get; private set; }     // 쌓인 돈 (정산 전)
        public bool Airborne { get; private set; }
        public bool Done { get; private set; }
        /// <summary>업그레이드 "가벼운 깡통": 맞을 때 위로 더 튄다</summary>
        public static float BounceBonus;

        public int HitsLeft { get; private set; }
        /// <summary>타이틀 메뉴 깡통: 떨어지면 이걸 부른다 (돈 정산 대신). 체력 점은 숨긴다</summary>
        public System.Action OnMenuLanded;
        public bool IsMenu => OnMenuLanded != null;
        public void MakeMenu(System.Action onLanded) { OnMenuLanded = onLanded; foreach (var p in pips) p.gameObject.SetActive(false); }
        Rigidbody2D rb;
        SpriteRenderer body, rim, label;
        SpriteRenderer[] pips;
        Transform visual;
        float flashLeft;

        public static Can Spawn(CanKind kind, Vector2 at)
        {
            var go = new GameObject($"Can_{kind.Name}");
            var can = go.AddComponent<Can>();
            can.Build(kind, at);
            return can;
        }

        void Build(CanKind kind, Vector2 at)
        {
            Kind = kind;
            transform.position = at + Vector2.up * 1.2f;   // 위에서 툭 떨어져 선반에 앉는다
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.mass = kind.Mass; rb.gravityScale = 2.2f; rb.linearDamping = 0.1f; rb.angularDamping = 0.6f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;   // 선반 위에선 안 넘어진다
            var col = gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(kind.Width, kind.Height);
            col.sharedMaterial = Mat();
            visual = new GameObject("Visual").transform; visual.SetParent(transform, false);
            body = Look.Quad("Body", Vector2.zero, new Vector2(kind.Width, kind.Height), kind.Body, 2, visual);
            label = Look.Quad("Label", Vector2.zero, new Vector2(kind.Width + 0.02f, kind.Height * 0.4f), kind.Label, 3, visual);
            rim = Look.Quad("RimTop", new Vector2(0f, kind.Height * 0.5f - 0.04f), new Vector2(kind.Width + 0.04f, 0.08f), kind.Rim, 4, visual);
            Look.Quad("RimBot", new Vector2(0f, -kind.Height * 0.5f + 0.04f), new Vector2(kind.Width + 0.04f, 0.08f), kind.Rim, 4, visual);
            Look.Quad("Shine", new Vector2(-kind.Width * 0.3f, 0f), new Vector2(0.06f, kind.Height * 0.8f), new Color(1f, 1f, 1f, 0.35f), 5, visual);
            // 체력 점: 깡통 위에 남은 횟수만큼 (맞을 때마다 하나씩 꺼진다)
            HitsLeft = kind.Hits;
            pips = new SpriteRenderer[kind.Hits];
            float pw = 0.08f, gap = 0.03f, total = kind.Hits * pw + (kind.Hits - 1) * gap;
            for (int i = 0; i < kind.Hits; i++)
                pips[i] = Look.Quad("Pip", new Vector2(-total * 0.5f + pw * 0.5f + i * (pw + gap), kind.Height * 0.5f + 0.14f), new Vector2(pw, pw), new Color(1f, 1f, 1f, 0.9f), 6, visual);
            visual.localScale = Vector3.zero;
            StartCoroutine(PopIn());
        }

        /// <summary>저주받았다 (저주 총에 맞아서): 보라색으로 변한다. 떨어지면 쌓인 돈을 뺏기고, 터뜨리면 평소대로 최고치</summary>
        public bool Cursed { get; private set; }
        static readonly Color CurseBody = new Color(0.16f, 0.08f, 0.2f), CurseRim = new Color(0.55f, 0.15f, 0.6f), CurseLabel = new Color(0.75f, 0.25f, 0.85f);
        public void Curse()
        {
            if (Cursed || Done) return;
            Cursed = true;
            Sfx.Play(Sfx.Tang(), 0.6f, 0.4f);
            StartCoroutine(CursePulse());
        }
        IEnumerator CursePulse()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime * 4f) { visual.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.4f); yield return null; }
            visual.localScale = Vector3.one;
        }
        Color BodyColor => Cursed ? CurseBody : Kind.Body;
        Color RimColor => Cursed ? CurseRim : Kind.Rim;
        Color LabelColor => Cursed ? CurseLabel : Kind.Label;

        static PhysicsMaterial2D mat;
        static PhysicsMaterial2D Mat() => mat != null ? mat : (mat = new PhysicsMaterial2D("can") { bounciness = 0.15f, friction = 0.5f });

        IEnumerator PopIn()
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime * 7f) { float s = Mathf.Sin(t * Mathf.PI * 0.5f); visual.localScale = new Vector3(s * 1.15f, s, 1f); yield return null; }
            visual.localScale = Vector3.one;
        }

        /// <summary>총에 맞았다. hitPoint = 맞은 자리(월드). 맞은 쪽 반대로, 위로 튄다. 돌려주는 값 = 이번 한 발로 쌓인 돈</summary>
        public int Hit(Vector2 hitPoint, float scale = 1f, float bounceScale = 1f)
        {
            if (Done) return 0;
            if (!Airborne)
            {
                Airborne = true;
                rb.constraints = RigidbodyConstraints2D.None;
                var shelf = Object.FindFirstObjectByType<Shelf>();
                if (shelf != null) Physics2D.IgnoreCollision(GetComponent<Collider2D>(), shelf.GetComponent<Collider2D>(), true);
            }
            Combo++;
            int earned = Game.I != null ? Game.I.EarnFor(Kind, Combo, scale) : Kind.Value * Combo;
            Earned += earned;
            if (!IsMenu)
            {
                HitsLeft--;
                for (int i = 0; i < pips.Length; i++) pips[i].color = i < HitsLeft ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 0.4f, 0.3f, 0.35f);
                if (HitsLeft <= 0) { Burst(hitPoint); return earned; }
            }
            Vector2 off = hitPoint - (Vector2)transform.position;
            float side = Mathf.Clamp(-off.x / Mathf.Max(0.01f, Kind.Width * 0.5f), -1f, 1f);
            var v = rb.linearVelocity;
            v.y = (Mathf.Max(0f, v.y * 0.3f) + 9.5f + BounceBonus + Random.Range(-0.5f, 0.8f)) * bounceScale;
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

        int bouncesLeft = -1;

        void OnTriggerEnter2D(Collider2D c)
        {
            if (Done || !Airborne) return;
            if (c.GetComponent<DropZone>() == null) return;
            if (bouncesLeft < 0) bouncesLeft = Kind.LandBounces;
            if (bouncesLeft > 0)
            {
                // 깡통 기능 "고무": 혼자 다시 튀어오른다 (연속은 이어진다)
                bouncesLeft--;
                var v = rb.linearVelocity; v.y = 11f + BounceBonus; v.x *= 0.5f; rb.linearVelocity = v;
                transform.position = new Vector3(transform.position.x, Look.DropY + 0.5f, 0f);
                flashLeft = 0.06f;
                StartCoroutine(Squash());
                Sfx.Play(Sfx.Tang(), 0.5f, Kind.Pitch * 0.6f);
                return;
            }
            Land();
        }

        /// <summary>체력이 다했다: 터진다. 최고치 정산. 파편이 사방으로</summary>
        void Burst(Vector2 at)
        {
            Done = true;
            Sfx.Play(Sfx.Bang(), 0.7f, Kind.Pitch * 1.6f);
            Sfx.Play(Sfx.Tang(), 0.9f, Kind.Pitch * 0.5f);
            for (int i = 0; i < 10; i++)
            {
                var chip = Look.Quad("Chip", (Vector2)transform.position + Random.insideUnitCircle * 0.2f, new Vector2(Random.Range(0.08f, 0.18f), Random.Range(0.06f, 0.14f)), i % 3 == 0 ? LabelColor : BodyColor, 7);
                chip.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                var crb = chip.gameObject.AddComponent<Rigidbody2D>();
                crb.gravityScale = 2f;
                crb.linearVelocity = Random.insideUnitCircle.normalized * Random.Range(4f, 9f) + Vector2.up * 3f;
                crb.angularVelocity = Random.Range(-900f, 900f);
                Destroy(chip.gameObject, Random.Range(0.6f, 1.1f));
            }
            Game.I?.CanBurst(this);
            StopAllCoroutines();
            rb.simulated = false;
            visual.gameObject.SetActive(false);
            Destroy(gameObject, 0.1f);
        }

        /// <summary>낙하지역에 떨어졌다: 정산하고 사라진다</summary>
        void Land()
        {
            Done = true;
            Sfx.Play(Sfx.Drop(), 0.6f, Kind.Pitch);
            if (IsMenu) OnMenuLanded(); else Game.I?.CanLanded(this);
            StopAllCoroutines();
            StartCoroutine(Vanish());
        }

        IEnumerator Vanish()
        {
            rb.simulated = false;
            yield return new WaitForSeconds(0.15f);
            for (float t = 0f; t < 1f; t += Time.deltaTime * 8f) { visual.localScale = Vector3.one * (1f - t); yield return null; }
            Destroy(gameObject);
        }

        void Update()
        {
            if (flashLeft > 0f)
            {
                flashLeft -= Time.deltaTime;
                bool on = flashLeft > 0f;
                body.color = on ? Color.white : BodyColor;
                label.color = on ? Color.white : LabelColor;
                rim.color = on ? Color.white : RimColor;
            }
            else if (Cursed && body.color != CurseBody) { body.color = BodyColor; label.color = LabelColor; rim.color = RimColor; }
        }
    }
}
