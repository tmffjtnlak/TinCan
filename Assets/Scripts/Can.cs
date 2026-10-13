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
        /// <summary>체력 = 몇 번 맞으면 터지나 (사용자 10-10: 무한 저글링을 막고, 다 맞춰 터뜨리면 최고치). 게임 한 문장: 튕겨서 터뜨린다</summary>
        public int Hits;
        /// <summary>무게 = 떨어지는 속도 (중력 배수). 깡통은 특능 없이 물성만: 무게·크기·체력·값 (사용자 10-10)</summary>
        public float Weight = 1f;
        /// <summary>맞을 때 튀는 힘 배수 / 회전 배수 / 찌그러짐 배수 (사용자 10-14: 현상금은 "느리고 묵직하게" — 낮게 뜨고 천천히 돌고 크게 찌그러진다)</summary>
        public float Bounce = 1f, Spin = 1f, Squish = 1f;

        public static readonly CanKind Tin = new CanKind { Id = "tin", Name = "깡통", Desc = "값 1. 6번 맞으면 터진다.", Value = 1, Cost = 2, Price = 0, Hits = 6, Weight = 1f, Width = 16f / 27f, Height = 20f / 27f, Mass = 1f,   /* 크기 = 사용자 임시 그림 can_tin 16×20 (10-13) */ Pitch = 1f, Body = Look.Tin, Rim = Look.TinDark, Label = Look.Label };
        public static readonly CanKind Bottle = new CanKind { Id = "bottle", Name = "병", Desc = "값 3. 작고 무겁다. 5번이면 깨진다.", Value = 3, Cost = 8, Price = 60, Hits = 5, Weight = 1.15f, Width = 15f / 27f, Height = 34f / 27f,   /* 크기 = 사용자 그림 can_bottle 15×34, 체력 5 (10-13) */ Mass = 0.8f, Pitch = 1.5f, Body = new Color(0.35f, 0.6f, 0.4f, 0.9f), Rim = new Color(0.25f, 0.4f, 0.28f), Label = new Color(0.9f, 0.85f, 0.7f) };
        public static readonly CanKind Rubber = new CanKind { Id = "rubber", Name = "고무 깡통", Desc = "값 2. 가벼워서 천천히 떨어진다. 8번이면 터진다.", Value = 2, Cost = 6, Price = 200, Hits = 8, Weight = 0.6f, Width = 0.5f, Height = 0.7f, Mass = 0.7f, Pitch = 0.85f, Body = new Color(0.9f, 0.45f, 0.5f), Rim = new Color(0.55f, 0.2f, 0.25f), Label = new Color(1f, 0.9f, 0.5f) };
        public static readonly CanKind Hat = new CanKind { Id = "hat", Name = "모자", Desc = "값 10. 납작하고 아주 가볍다. 10번이면 찢어진다.", Value = 10, Cost = 30, Price = 400, Hits = 10, Weight = 0.45f, Width = 0.7f, Height = 0.36f, Mass = 0.5f, Pitch = 0.7f, Body = new Color(0.35f, 0.22f, 0.14f), Rim = new Color(0.2f, 0.12f, 0.08f), Label = new Color(0.8f, 0.65f, 0.3f) };
        public string Id;

        // ---------- 특성 깡통 (사용자 10-14: "쏘는 행위는 같고 깡통이 일으키는 일이 다양해야") ----------
        // 10-10의 "깡통은 물성만" 규칙을 사용자가 바꿨다. 특성 = 터질 때/맞을 때 일어나는 일 하나
        public enum Trait { None, Dynamite, Bundle, Blue, Magnet }
        /// <summary>보조 성향 깡통: 터지거나 떨어져도 탄피를 흘리지 않는다 (사용자 10-14, 다이너마이트부터). 돈·연속은 그대로</summary>
        public bool NoCasings;
        /// <summary>계산 밖 물체 (사용자 10-14: "다이너마이트는 공격 수단이지 돈 수급처가 아니다"). 깡통처럼 맞고 튀고 터지지만
        /// 돈·터뜨림 수·연속 기록·공중 수(배율·세트·소환 잠금)에 하나도 안 들어간다</summary>
        public bool NotScored;
        public Trait Special;
        /// <summary>다이너마이트: 3발에 터지며 주변 깡통에 한 발씩 (밀어내며 띄움)</summary>
        public static readonly CanKind Dynamite = new CanKind { Id = "dynamite", Name = "다이너마이트", Desc = "3발에 터지며 주변 깡통을 친다.", Value = 2, Cost = 2, Hits = 3, Weight = 1.1f, Width = 18f / 27f, Height = 28f / 27f, Mass = 1.1f, Pitch = 0.9f, Body = new Color(0.85f, 0.15f, 0.15f), Rim = new Color(0.5f, 0.1f, 0.1f), Label = Color.white, Special = Trait.Dynamite, NoCasings = true, NotScored = true };
        /// <summary>깡통 묶음: 3발에 끈이 끊어지며 깡통 셋이 튀어나와 3초 동안 천천히 떨어진다</summary>
        public static readonly CanKind Bundle = new CanKind { Id = "bundle", Name = "깡통 묶음", Desc = "3발에 풀리며 깡통 셋이 튀어나온다.", Value = 1, Cost = 2, Hits = 3, Weight = 1.3f, Width = 28f / 27f, Height = 37f / 27f, Mass = 1.5f, Pitch = 0.8f, Body = Look.Tin, Rim = Look.TinDark, Label = Look.Label, Special = Trait.Bundle };
        /// <summary>파란 깡통: 터지면 반투명 깡통이 잠시 떠 있다</summary>
        public static readonly CanKind Blue = new CanKind { Id = "blue", Name = "파란 깡통", Desc = "터지면 반투명 깡통이 잠시 떠오른다.", Value = 1, Cost = 2, Hits = 4, Weight = 1f, Width = 16f / 27f, Height = 20f / 27f, Mass = 1f, Pitch = 1.1f, Body = new Color(0.4f, 0.6f, 1f), Rim = new Color(0.25f, 0.35f, 0.7f), Label = Color.white, Special = Trait.Blue };
        /// <summary>자석 깡통: 맞을 때마다 옆의 옆까지 깡통을 끌어당긴다 (그림은 자석까지 32칸, 몸 간격은 24칸)</summary>
        public static readonly CanKind Magnet = new CanKind { Id = "magnet", Name = "자석 깡통", Desc = "맞을 때마다 주변 깡통을 끌어당긴다.", Value = 1, Cost = 2, Hits = 5, Weight = 1f, Width = 24f / 27f, Height = 20f / 27f, Mass = 1f, Pitch = 1.2f, Body = Look.Tin, Rim = Look.TinDark, Label = Look.Label, Special = Trait.Magnet };
        /// <summary>묶음에서 튀어나온 깡통 (깡통 그림, 체력 2)</summary>
        public static readonly CanKind Loose = new CanKind { Id = "tin", Name = "풀린 깡통", Desc = "", Value = 1, Cost = 0, Hits = 2, Weight = 1f, Width = 16f / 27f, Height = 20f / 27f, Mass = 1f, Pitch = 1.05f, Body = Look.Tin, Rim = Look.TinDark, Label = Look.Label };
        /// <summary>파란 깡통이 남기는 반투명 깡통 (체력 2)</summary>
        public static readonly CanKind Ghost = new CanKind { Id = "blue", Name = "반투명 깡통", Desc = "", Value = 1, Cost = 0, Hits = 2, Weight = 1f, Width = 16f / 27f, Height = 20f / 27f, Mass = 1f, Pitch = 1.4f, Body = new Color(0.4f, 0.6f, 1f), Rim = new Color(0.25f, 0.35f, 0.7f), Label = Color.white };

        /// <summary>깡통류 가게 목록 (순서대로)</summary>
        /// <summary>현상수배 깡통: 체력 15 (10-14: 30 → 15), 무겁고, 값 5. 터뜨리면 값×15×16 = 1,200 기본. 가게엔 없다</summary>
        public static readonly CanKind Bounty = new CanKind { Id = "bounty", Name = "현상수배 깡통", Desc = "체력 15. 무겁다. 터뜨리면 현상금.", Value = 5, Cost = 0, Price = 0, Hits = 15, Weight = 0.6f, Bounce = 0.7f, Spin = 0.4f, Squish = 1.5f,  Width = 19f / 27f, Height = 24f / 27f, Mass = 2f, Pitch = 0.6f, Body = new Color(0.55f, 0.18f, 0.14f), Rim = new Color(0.3f, 0.1f, 0.08f), Label = new Color(0.9f, 0.75f, 0.3f), IsBounty = true };
        public bool IsBounty;
        /// <summary>현상금의 부하 (사용자 10-14): 콧수염 단 깡통. 현상금이 살아 있는 동안 바닥에서 무한히 다시 뛰어오르고, 터지면 현상금이 새로 불러낸다</summary>
        public bool IsMinion;
        public static readonly CanKind Minion = new CanKind { Id = "tin", Name = "부하 깡통", Desc = "현상금의 부하.", Value = 1, Cost = 0, Price = 0, Hits = 3, Weight = 1f, Width = 16f / 27f, Height = 20f / 27f, Mass = 1f, Pitch = 1.15f, Body = Look.Tin, Rim = Look.TinDark, Label = Look.Label, IsMinion = true };
        /// <summary>시험용 현상수배 깡통 (사용자 10-13): 체력 3. F4로 부른다 (막타 연출 확인용)</summary>
        public static readonly CanKind BountyTest = new CanKind { Id = "bounty", Name = "현상수배 깡통 (시험)", Desc = "체력 3. 시험용.", Value = 5, Cost = 0, Price = 0, Hits = 3, Weight = 0.6f, Bounce = 0.7f, Spin = 0.4f, Squish = 1.5f,  Width = 19f / 27f, Height = 24f / 27f, Mass = 2f, Pitch = 0.6f, Body = new Color(0.55f, 0.18f, 0.14f), Rim = new Color(0.3f, 0.1f, 0.08f), Label = new Color(0.9f, 0.75f, 0.3f), IsBounty = true };
        public static readonly System.Collections.Generic.List<CanKind> ForSale = new System.Collections.Generic.List<CanKind> { Tin, Bottle, Rubber, Hat };
        /// <summary>터뜨리면 기본 얼마: 값 × H(H+1). (n번 맞추면 값 × n(n+1)/2 쌓이고, 터뜨리면 ×2) — 예측 가능한 점수</summary>
        public long BurstBase => (long)Value * Hits * (Hits + 1);
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
        /// <summary>슬로우(우클릭): 깡통만 시간이 느려진다. 1 = 보통. 속도는 비례, 중력은 제곱으로 줄여야 진짜 시간 느려짐과 같다</summary>
        public static float TimeScale = 1f;
        float appliedScale = 1f;
        // 기본 중력 (10-14 사용자: 기본 총으로 저글링이 어렵다) 2.2 → 1.6: 한 발 체공(0.94초)이 장전(0.9초)과 거의 같아서
        // 장전하는 동안 깡통이 다 떨어졌다. 튀는 속도는 그대로 → 조금 더 높이, 더 오래 뜬다
        const float BaseGravity = 1.6f;
        /// <summary>이 깡통만의 시간 배수 (연쇄 대기 중 0.08). 실제로 쓰는 배수 = 전체(TimeScale) × 이것</summary>
        float localSlow = 1f, localSlowTarget = 1f;
        float Scale => TimeScale * localSlow;
        /// <summary>현상금 연쇄 차례를 기다리는 중: 거의 멈춰 매달려 있고, 바닥에 닿아도 사라지지 않는다</summary>
        public bool ChainHeld { get; private set; }
        bool parked;   // 연쇄 대기 중 바닥에 닿아 멈춰 있다

        /// <summary>연쇄 대상이 됐다 (사용자 10-13): 0.15초에 걸쳐 이 깡통만 0.08배로 느려져 공중에 매달린 채 차례를 기다린다</summary>
        public void HoldForChain()
        {
            if (Done || !Airborne) return;
            ChainHeld = true; localSlowTarget = 0.08f;
        }

        float Gravity => BaseGravity * Kind.Weight * Scale * Scale;
        bool floorSlowed;

        void FixedUpdate()
        {
            if (rb == null || Done) return;
            if (!Mathf.Approximately(localSlow, localSlowTarget)) localSlow = Mathf.MoveTowards(localSlow, localSlowTarget, Time.unscaledDeltaTime / 0.15f);
            SyncScale();
            if (flyLeft > 0f && Airborne && !held && !parked) FlyStep();
            // 떠 있기 (다이너마이트 투척 등): 남은 동안 중력에 배수, 정지면 속도도 서서히 죽인다
            else if (floatLeft > 0f && Airborne && !held && !parked)
            {
                floatLeft -= Time.fixedDeltaTime * Scale;
                bool on = floatLeft > 0f;
                rb.gravityScale = Gravity * (on ? floatMul : 1f);
                if (on && floatMul <= 0.01f) rb.linearVelocity *= 1f - Mathf.Min(1f, 4f * Time.fixedDeltaTime);
            }
            // 저글링 낙하 완화: 떨어지는 동안만, 공중 수에 따라 중력을 줄인다 (다이너마이트는 계산 밖이라 제외)
            else if (Airborne && !held && !parked && !Kind.NotScored)
                rb.gravityScale = Gravity * (rb.linearVelocity.y < 0f ? JuggleFallMul() : 1f);
            // 스킬 "바닥 근처 느려짐": 떨어지다 바닥 가까이 오면 속도가 반으로 (한 번만, 다시 맞으면 초기화)
            if (Airborne && !floorSlowed && Game.I != null && Game.I.FloorSlow > 0f && rb.linearVelocity.y < 0f && transform.position.y < Look.DropY + 1.6f)
            {
                floorSlowed = true;
                var v = rb.linearVelocity; v.y *= 1f - Game.I.FloorSlow; rb.linearVelocity = v;
                Fx.Dust(new Vector2(transform.position.x, transform.position.y - 0.3f), 2, 0.4f);
            }
        }

        /// <summary>
        /// 리지드바디 속도를 지금 TimeScale 단위로 맞춘다. 속도를 읽거나 새로 넣기 전에 반드시 먼저 부른다.
        /// (10-13 버그: 느려지기가 켜지거나 꺼진 바로 그 프레임에 맞으면, Hit은 새 배수로 속도를 넣고
        ///  다음 FixedUpdate가 옛 배수 기준으로 한 번 더 곱해서 4배로 튀어 천장에 박히거나, 덜 튀어 떨어졌다)
        /// </summary>
        void SyncScale()
        {
            if (rb == null || Mathf.Approximately(appliedScale, Scale)) return;
            float k = Scale / appliedScale;
            rb.linearVelocity *= k; rb.angularVelocity *= k;
            rb.gravityScale = held || parked ? 0f : Gravity;
            appliedScale = Scale;
        }

        public int HitsLeft { get; private set; }
        /// <summary>타이틀 메뉴 깡통: 떨어지면 이걸 부른다 (돈 정산 대신). 체력 점은 숨긴다</summary>
        public System.Action OnMenuLanded;
        public bool IsMenu => OnMenuLanded != null;
        /// <summary>타이틀 깡통: 체력 3, 점은 안 보인다. 터뜨리면 즉시, 떨어져도 된다 (사용자 10-11)</summary>
        public void MakeMenu(System.Action onLanded) { OnMenuLanded = onLanded; HitsLeft = 3; foreach (var p in pips) p.gameObject.SetActive(false); }
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

        float floatLeft, floatMul = 1f;

        /// <summary>
        /// 공중에 바로 생기는 깡통 (특성 깡통이 터지며 만든 것). 선반 착지 연출 없이 작게 톡 커지며 바로 공중,
        /// floatSeconds 동안 중력 × floatMul (0이면 그 자리에 둥실), ghost면 반투명. 게임의 깡통 목록에도 넣는다
        /// </summary>
        bool slowUntilHit;
        float alpha = 1f;

        /// <summary>
        /// 디버그 표시 (F9, 사용자 10-14): 속도·중력 배율·무게·질량·체력·상태.
        /// 중력 배율 = 지금 리지드바디 중력 ÷ (기본 2.2 × 무게 × 시간배수²) → 저글링 낙하 완화·둥실·비행이 걸리면 1보다 작다
        /// </summary>
        public string DebugText()
        {
            if (rb == null) return Kind.Name;
            var v = rb.linearVelocity;
            float gBase = BaseGravity * Kind.Weight * Scale * Scale;
            float mul = gBase > 0.0001f ? rb.gravityScale / gBase : 0f;
            string st = Done ? "끝" : !Airborne ? (held ? "소환 중" : "선반") : flyLeft > 0f ? $"비행 {flyLeft:0.0}s" : floatLeft > 0f ? $"둥실 {floatLeft:0.0}s" : slowUntilHit ? "슬로우(맞기 전)" : ChainHeld ? "연쇄 대기" : v.y < 0f ? "낙하" : "상승";
            if (Kind.NotScored) st += " · 계산 밖";
            return $"{Kind.Name}  체력 {HitsLeft}/{Kind.Hits}  연속 {Combo}  ${Earned}\n" +
                   $"속도 ({v.x:0.0}, {v.y:0.0})  |{v.magnitude:0.0}|\n" +
                   $"중력 ×{mul:0.00}  (2.2 × 무게 {Kind.Weight:0.##} × 시간² {Scale * Scale:0.##} = {gBase:0.00} → 실제 {rb.gravityScale:0.00})\n" +
                   $"질량 {rb.mass:0.##}  시간 ×{Scale:0.00}  {st}";
        }

        // ---------- 비행 (파란 깡통이 남긴 반투명 깡통, 사용자 10-14): 화면 가운데 줄로 올라가 좌우로 잠시 날다가 떨어진다 ----------
        float flyLeft, flyDir = 1f;
        const float FlyY = 0.4f, FlySpeed = 2.6f;
        public void StartFlight(float seconds) { flyLeft = seconds; flyDir = Random.value < 0.5f ? -1f : 1f; }

        void FlyStep()
        {
            flyLeft -= Time.fixedDeltaTime * Scale;
            if (flyLeft <= 0f) { rb.gravityScale = Gravity; return; }
            var p = (Vector2)transform.position;
            float edge = Look.PlayHalfW - 1.2f;
            if (p.x > edge) flyDir = -1f; else if (p.x < -edge) flyDir = 1f;   // 화면 끝에서 돌아선다
            var v = new Vector2(flyDir * FlySpeed, Mathf.Clamp((FlyY - p.y) * 3f, -4f, 6f) + Mathf.Sin(Time.time * 6f) * 0.4f);
            rb.gravityScale = 0f;
            rb.linearVelocity = v * Scale;
            rb.angularVelocity = Mathf.Lerp(rb.angularVelocity, -flyDir * 120f * Scale, 0.1f);
        }

        /// <summary>selfSlow &lt; 1이면 처음 맞기 전까지 이 깡통만 그 배수로 느리다 (묶음에서 풀린 깡통)</summary>
        public static Can SpawnAirborne(CanKind kind, Vector2 at, Vector2 vel, float floatSeconds, float floatMul, bool ghost = false, float selfSlow = 1f)
        {
            var c = Spawn(kind, at);
            c.StopAllCoroutines();
            c.transform.position = at;
            c.held = false; c.Airborne = true;
            if (selfSlow < 1f) { c.localSlow = c.localSlowTarget = selfSlow; c.slowUntilHit = true; c.appliedScale = c.Scale; }
            c.rb.constraints = RigidbodyConstraints2D.None;
            var shelf = Object.FindFirstObjectByType<Shelf>();
            if (shelf != null) Physics2D.IgnoreCollision(c.GetComponent<Collider2D>(), shelf.GetComponent<Collider2D>(), true);
            c.floatLeft = floatSeconds; c.floatMul = floatMul;
            c.rb.gravityScale = c.Gravity * (floatSeconds > 0f ? floatMul : 1f);
            c.rb.linearVelocity = vel * c.Scale;
            c.rb.angularVelocity = Random.Range(-300f, 300f) * c.Scale;
            if (ghost) { c.alpha = 0.5f; foreach (var sr in c.visual.GetComponentsInChildren<SpriteRenderer>()) if (sr != c.white) { var col = sr.color; col.a = 0.5f; sr.color = col; } }   // 사용자: "반투명 캔"
            if (c.white != null) c.white.color = Color.clear;
            c.StartCoroutine(c.PopSmall());
            Game.I?.Cans.Add(c);
            return c;
        }

        IEnumerator PopSmall()
        {
            float s = 0.3f, v = 0f;
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) { Ease.Spring(ref s, ref v, 1f, 900f, 16f, Time.deltaTime); visual.localScale = Vector3.one * s; yield return null; }
            visual.localScale = Vector3.one;
        }

        /// <summary>자석에 끌려온다: 그쪽으로 속도를 더한다 (선반 위면 선반 따라 미끄러짐)</summary>
        public void Pull(Vector2 toward, float strength)
        {
            if (Done || rb == null) return;
            SyncScale();
            var dir = toward - (Vector2)transform.position;
            if (dir.sqrMagnitude < 0.0001f) return;
            var v = rb.linearVelocity / Scale;
            if (Airborne) v += dir.normalized * strength;
            else v.x += Mathf.Sign(dir.x) * strength * 0.8f;
            rb.linearVelocity = v * Scale;
        }

        void Build(CanKind kind, Vector2 at)
        {
            Kind = kind;
            transform.position = at + Vector2.up * 0.8f;   // 위에서 툭 떨어져 선반에 앉는다 (10-13: 1.2 → 0.8, 소환 전체 40% 단축)
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.mass = kind.Mass; rb.linearDamping = 0.1f; rb.angularDamping = 0.6f; appliedScale = Scale;
            held = true; rb.gravityScale = 0f;   // 소환 트윈이 거의 끝날 때까지 공중에 멈춰 있다 (사용자 10-13). PopIn이 풀어 준다
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;   // 선반 위에선 안 넘어진다
            var col = gameObject.AddComponent<BoxCollider2D>();
            col.size = new Vector2(kind.Width, kind.Height);
            col.sharedMaterial = Mat();
            // 깡통끼리는 안 부딪힌다 (사용자 10-11): 예측이 깨지고, 플레이어가 통제하는 건 자기 손뿐이어야 한다
            foreach (var other in All) if (other != null && other != this) Physics2D.IgnoreCollision(col, other.GetComponent<Collider2D>(), true);
            All.Add(this);
            visual = new GameObject("Visual").transform; visual.SetParent(transform, false);
            var art = GunView.LoadArt("can_" + kind.Id);   // 사용자 그림이 있으면 도형 대신 (Assets/Art/can_{id}.png)
            if (art != null)
            {
                body = new GameObject("Body").AddComponent<SpriteRenderer>(); body.transform.SetParent(visual, false);
                body.sprite = art; body.sortingOrder = 2;
                var w = WhiteOf(art);
                if (w != null) { white = new GameObject("White").AddComponent<SpriteRenderer>(); white.transform.SetParent(visual, false); white.sprite = w; white.sortingOrder = 3; white.color = Color.white; }
            }
            else
            {
                body = Look.Quad("Body", Vector2.zero, new Vector2(kind.Width, kind.Height), kind.Body, 2, visual);
                label = Look.Quad("Label", Vector2.zero, new Vector2(kind.Width + 0.02f, kind.Height * 0.4f), kind.Label, 3, visual);
                rim = Look.Quad("RimTop", new Vector2(0f, kind.Height * 0.5f - 0.04f), new Vector2(kind.Width + 0.04f, 0.08f), kind.Rim, 4, visual);
                Look.Quad("RimBot", new Vector2(0f, -kind.Height * 0.5f + 0.04f), new Vector2(kind.Width + 0.04f, 0.08f), kind.Rim, 4, visual);
                Look.Quad("Shine", new Vector2(-kind.Width * 0.3f, 0f), new Vector2(0.06f, kind.Height * 0.8f), new Color(1f, 1f, 1f, 0.35f), 5, visual);
            }
            // 체력 점: 깡통 위에 남은 횟수만큼 (맞을 때마다 하나씩 꺼진다)
            HitsLeft = kind.Hits;
            if (Game.I != null && Game.I.Hud != null) earnedLabel = Game.I.Hud.CanLabel();
            pips = new SpriteRenderer[kind.Hits];
            int perRow = Mathf.Min(kind.Hits, 10);
            float pw = kind.Hits > 10 ? 0.06f : 0.08f, gap = 0.03f, total = perRow * pw + (perRow - 1) * gap;
            for (int i = 0; i < kind.Hits; i++)
                pips[i] = Look.Quad("Pip", new Vector2(-total * 0.5f + pw * 0.5f + (i % perRow) * (pw + gap), kind.Height * 0.5f + 0.14f + (i / perRow) * (pw + gap)), new Vector2(pw, pw), new Color(1f, 1f, 1f, 0.9f), 6, visual);
            if (kind.IsBounty) AddProps();
            if (kind.IsMinion) AddProp(Random.value < 0.5f ? "prop_stache_wavy" : "prop_stache_flat", new Vector2(0f, -1f), 4, false, 0.15f);   // 부하: 콧수염만
            visual.localScale = Vector3.zero;
            StartCoroutine(PopIn());
        }

        // ---------- 현상금 깡통 부착물 (사용자 10-13) ----------
        // 자리는 사용자 합성 그림(19×24 깡통 기준)에서 잰 값: 깡통 가운데에서 도트 (위 +).
        // 모자 하나(갈색/흰색) + 콧수염 하나(물결/일자) + 권총 60%. 권총은 깡통 뒤(총열이 가려짐), 콧수염은 라벨 위, 모자는 맨 위
        readonly System.Collections.Generic.List<CanProp> props = new System.Collections.Generic.List<CanProp>();
        const float PropDropChance = 0.05f;  // 맞을 때마다 이 확률로 하나가 떨어져 나간다 (10-14: 20%는 체력 30에서 중반에 다 벗겨져서 5%)
        void AddProps()
        {
            bool brown = Random.value < 0.5f;
            AddProp(brown ? "prop_hat_brown" : "prop_hat_white", brown ? new Vector2(0f, 13f) : new Vector2(0.5f, 19.5f), 5, true, 0.32f);
            AddProp(Random.value < 0.5f ? "prop_stache_wavy" : "prop_stache_flat", new Vector2(0f, -1.5f), 4, false, 0.18f);
            if (Random.value < 0.6f) AddProp("prop_gun", new Vector2(-3.5f, -7.5f), 1, false, 0.25f);
            // 체력 점은 모자 위로 올린다
            float top = Kind.Height * 0.5f;
            foreach (var p in props) top = Mathf.Max(top, p.Top);
            float lift = top - Kind.Height * 0.5f;
            if (lift > 0f) foreach (var pip in pips) pip.transform.localPosition += Vector3.up * lift;
        }
        void AddProp(string art, Vector2 offsetDots, int order, bool fromAbove, float delay)
        {
            var p = CanProp.Make(visual, art, offsetDots, order, fromAbove, delay);
            if (p != null) props.Add(p);
        }
        void DropProp(Vector2 from, float power = 1f)
        {
            if (props.Count == 0) return;
            int i = Random.Range(0, props.Count);
            if (props[i] != null) props[i].Detach(from, power);
            props.RemoveAt(i);
        }
        void DropAllProps(Vector2 from, float power)
        {
            foreach (var p in props) if (p != null) p.Detach(from, power);
            props.Clear();
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

        /// <summary>
        /// 소환 (사용자 10-13): 뒤에 흰 도트 구름이 피어오르고, 깡통은 흰 실루엣으로 작게 나타나 용수철로 튕기며 커지고 제 색이 된다.
        /// 선반에 닿는 순간 납작(1.3×0.7)해졌다 용수철로 돌아오고 먼지.
        /// </summary>
        IEnumerator PopIn()
        {
            // 사용자 10-13 2판: 뒤에 구름 + 45° 기울어진 채 작게 나타나 → 커지는 용수철 + 회전 용수철(0°로 흔들리며 돌아옴)
            // → 트윈이 거의 끝나면(0.3초 또는 크기·각도가 자리 잡으면) 그때 중력 → 선반에 쿵(납작 → 용수철)
            // 10-13 3판: 전체 40% 단축 (용수철 세기 ×2.8·감쇠 ×1.65 = 같은 출렁임을 0.6배 시간에), 처음 각도 70~85°,
            // 착지 = 선반과 부딪힌 그 프레임(OnCollisionEnter2D), 납작은 밑면 기준(가운데 기준이면 밑이 떠 보였다), 떨어지는 동안 살짝 길쭉
            if (!IsMenu) Fx.Cloud(transform.position, 1.08f);   // 10-13: 구름 1.2배 → 너무 커서 10% 줄임 (1.2 × 0.9)
            float sx = 0.15f, sy = 0.15f, vx = 0f, vy = 0f, whiteA = 1f;
            // 회전 (10-13 5판): ±180°에서 시작해 '느렸다 빨랐다 느려지는' 곡선(InOutCubic)으로 0.29초에 0°까지 돈다.
            // 체공 시간은 4판보다 30% 길게 (회전 0.22 → 0.29초, 중력 풀기 0.24~0.3 → 0.31~0.39초)
            // 6판 (10-13): 0°가 아니라 비스듬히(±8~14°) 멈춘다 → 선반에 닿는 순간 용수철로 똑바로.
            // 회전 관성: 도는 빠르기만큼 길쭉해지고(세로 최대 +28%, 가로 그 55%만큼 홀쭉) 느려지면 용수철로 돌아온다
            // 7판 (10-13): 한 바퀴(360°)
            const float SpinDeg = 360f;
            float rotStart = (Random.value < 0.5f ? -1f : 1f) * SpinDeg, rot = rotStart, rv = 0f, prevRot = rotStart;
            float endTilt = (Random.value < 0.5f ? -1f : 1f) * Random.Range(8f, 14f);
            const float RotDur = 0.29f, OmegaPeak = 1.5f * SpinDeg / RotDur;   // InOutCubic 최고 빠르기 = 1.5 × 각도 / 시간 (360°면 ≈ 1860°/초)
            bool spun = false;
            bool landed = false; float t = 0f, releasedAt = -1f;
            shelfHit = false;
            while (t < 1.5f && !Airborne)
            {
                float dt = Time.deltaTime; t += dt;
                bool falling = releasedAt >= 0f && !landed && rb != null && rb.linearVelocity.y < -1.5f;
                if (!spun)
                {
                    float k = Mathf.Clamp01(t / RotDur);
                    rot = endTilt + (rotStart - endTilt) * (1f - Ease.InOutCubic(k));
                    if (k >= 1f) { spun = true; rot = endTilt; rv = 0f; }
                }
                else Ease.Spring(ref rot, ref rv, landed ? 0f : endTilt, 720f, 13f, dt);   // 공중에선 비스듬히, 선반에 닿으면 똑바로 (덜 감쇠: 한두 번 흔들리며)
                // 회전 관성 늘어남: 이번 프레임 도는 빠르기 → 목표 크기. 용수철이 따라가서 늦게 늘고 튕기며 돌아온다
                float omega = dt > 0f ? Mathf.Abs(rot - prevRot) / dt : 0f; prevRot = rot;
                float stretch = Mathf.Clamp01(omega / OmegaPeak) * 0.28f;
                // 크기: 가로가 조금 덜 감쇠 → 옆으로 먼저 퍼졌다 자리 (말랑). 떨어지는 동안은 살짝 길쭉 (0.92 × 1.1)
                Ease.Spring(ref sx, ref vx, (falling ? 0.92f : 1f) - stretch * 0.55f, 1150f, 18f, dt);
                Ease.Spring(ref sy, ref vy, (falling ? 1.1f : 1f) + stretch, 1150f, 23f, dt);
                if (t > 0.03f) whiteA = Mathf.Max(0f, whiteA - dt / 0.07f);   // 2프레임 흰색 → 0.07초에 제 색
                if (white != null) white.color = new Color(1f, 1f, 1f, Mathf.Max(whiteA, flashLeft > 0f ? 1f : 0f));
                // 중력 풀기: 트윈이 어느 정도 끝났을 때 (늦어도 0.39초). 아래로 살짝 밀어 바로 떨어지기 시작
                if (releasedAt < 0f && (t > 0.39f || (spun && t > 0.31f && Mathf.Abs(sx - 1f) < 0.08f && Mathf.Abs(rot - endTilt) < 4f)))
                {
                    releasedAt = t; ReleaseHold(); SyncScale();
                    if (rb != null) rb.linearVelocity = new Vector2(0f, -2.5f * Scale);
                }
                // 선반에 닿은 그 프레임
                if (!landed && shelfHit)
                {
                    landed = true;
                    if (!IsMenu) { Fx.Dust(new Vector2(transform.position.x, Look.ShelfY + 0.15f), 3, 0.5f); Sfx.Play(Sfx.Drop(), 0.3f, Kind.Pitch * 1.2f); }
                    sx = 1.35f; sy = 0.65f; vx = vy = 0f;
                    if (rb != null) rb.linearVelocity = Vector2.zero;   // 물리 튕김은 없앤다: 튕기는 느낌은 납작 용수철이 낸다 (튕겨 뜨면 납작이 공중에서 보였다)
                }
                visual.localScale = new Vector3(sx, sy, 1f);
                // 밑면 기준: 세로가 줄면 그만큼 내려서 밑이 선반에 붙어 있게 (착지 후에만. 공중에선 가운데 기준)
                visual.localPosition = landed ? new Vector3(0f, -(1f - sy) * Kind.Height * 0.5f, 0f) : Vector3.zero;
                visual.localRotation = Quaternion.Euler(0f, 0f, rot);
                if (landed && whiteA <= 0f && Mathf.Abs(sx - 1f) < 0.01f && Mathf.Abs(vx) < 0.05f && Mathf.Abs(rot) < 0.5f) break;
                yield return null;
            }
            ReleaseHold();
            visual.localScale = Vector3.one;
            visual.localPosition = Vector3.zero;
            visual.localRotation = Quaternion.identity;
            if (white != null && flashLeft <= 0f) white.color = new Color(1f, 1f, 1f, 0f);
        }

        bool shelfHit;
        void OnCollisionEnter2D(Collision2D c)
        {
            if (!Airborne && c.collider != null && c.collider.GetComponent<Shelf>() != null) shelfHit = true;
        }

        bool held;   // 소환 트윈 중: 중력 없이 공중에 멈춰 있다
        void ReleaseHold()
        {
            if (!held) return;
            held = false;
            if (rb != null) rb.gravityScale = Gravity;
        }

        SpriteRenderer white;   // 그림의 흰 실루엣 (소환·맞을 때 번쩍임)
        static readonly System.Collections.Generic.Dictionary<Sprite, Sprite> whites = new System.Collections.Generic.Dictionary<Sprite, Sprite>();
        static Sprite WhiteOf(Sprite s)
        {
            if (s == null) return null;
            if (whites.TryGetValue(s, out var w)) return w;
            try
            {
                var r = s.textureRect;
                var tex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = s.texture.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
                for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, px[i].a);
                tex.SetPixels(px); tex.Apply();
                w = Sprite.Create(tex, new Rect(0, 0, r.width, r.height), new Vector2(0.5f, 0.5f), s.pixelsPerUnit);
            }
            catch { w = null; }
            whites[s] = w;
            return w;
        }

        /// <summary>총에 맞았다. hitPoint = 맞은 자리(월드). 맞은 쪽 반대로, 위로 튄다. 돌려주는 값 = 이번 한 발로 쌓인 돈</summary>
        public int Hit(Vector2 hitPoint, float scale = 1f, float bounceScale = 1f, int damage = 1)
        {
            if (Done) return 0;
            // 묶음에서 풀린 깡통: 처음 맞기 전까지 자기만 느려져 있다 → 맞는 순간 제 속도 (사용자 10-14)
            if (slowUntilHit) { slowUntilHit = false; localSlowTarget = 1f; localSlow = 1f; }
            if (flyLeft > 0f) flyLeft = 0f;   // 날던 깡통은 맞으면 평범하게 튄다 (그다음은 중력)
            if (crouching) { crouching = false; held = false; if (rb != null) rb.gravityScale = Gravity; }   // 바닥에 웅크린 현상금을 맞히면 그 한 발로 뜬다
            SyncScale();   // 속도 계산 전에 배수부터 맞춘다 (느려지기 켜고 끄는 프레임 버그)
            if (!Airborne)
            {
                Airborne = true;
                ReleaseHold();   // 소환 트윈 중(공중에 멈춰 있을 때) 맞아도 바로 중력
                rb.constraints = RigidbodyConstraints2D.None;
                var shelf = Object.FindFirstObjectByType<Shelf>();
                if (shelf != null) Physics2D.IgnoreCollision(GetComponent<Collider2D>(), shelf.GetComponent<Collider2D>(), true);
            }
            Combo++;
            floorSlowed = false;
            int earned = Kind.NotScored ? 0 : Game.I != null ? Game.I.EarnFor(Kind, Combo, scale) : Kind.Value * Combo;   // 계산 밖 물체는 돈 0
            Earned += earned;
            // 현상금은 직접 맞아도 체력이 안 깎인다 (사용자 10-14: 부하를 터뜨린 파편으로만). 체력 1(막타 차례)일 때만 총이 마무리한다
            bool armored = Kind.IsBounty && HitsLeft > 1;
            if (!armored) HitsLeft -= Mathf.Max(1, damage);   // 다이너마이트는 한 번에 2 (스킬로 강화 예정)
            else Sfx.Play(Sfx.Click(), 0.6f, 0.55f);         // 쇠에 튕기는 소리: 안 먹혔다
            // (10-14 버티기 규칙은 뺐다: "터져야 할 깡통이 안 터지면 이상하다". 대신 현상금전엔 터진 깡통의 파편이 현상금을 깎는다 — Game.CanBurst)
            for (int i = 0; i < pips.Length; i++) pips[i].color = i < HitsLeft ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 0.4f, 0.3f, 0.35f);
            if (HitsLeft <= 0) { Burst(hitPoint); return earned; }
            Vector2 off = hitPoint - (Vector2)transform.position;
            float side = Mathf.Clamp(-off.x / Mathf.Max(0.01f, Kind.Width * 0.5f), -1f, 1f);
            var v = rb.linearVelocity / Scale;   // 느려진 시간 안에서 계산하고 다시 줄인다
            // 기본 튐은 보수적으로 (리볼버 한 발 = 선반에서 화면 중간쯤). 총 배수·스킬로 위로 갈라진다. 천장은 안전망
            v.y = (Mathf.Max(0f, v.y * 0.3f) + 7.2f + BounceBonus + Random.Range(-0.4f, 0.6f)) * bounceScale * Kind.Bounce;   // 현상금은 낮게 (Bounce 0.7)
            v.y *= AltitudeFactor(transform.position.y);   // 높이 있을수록 한 발이 덜 띄운다 (천장에서 노는 것 방지, 중간 높이 저글링은 그대로)
            v.x = v.x * 0.4f + side * 2.2f + Random.Range(-0.6f, 0.6f);
            // 천장 한계 (사용자 10-11): 화면 밖으로 나가면 연속이 끊기니, 꼭대기가 화면 위 끝 바로 아래가 되도록 자른다
            float top = Look.CeilingY - Kind.Height * 0.5f - transform.position.y;
            float g = -Physics2D.gravity.y * BaseGravity * Kind.Weight;
            float vmax = top > 0f ? Mathf.Sqrt(2f * g * top) : 0f;
            if (v.y > vmax) v.y = vmax;
            rb.linearVelocity = v * Scale;
            rb.angularVelocity = (-side * Random.Range(400f, 800f) + Random.Range(-200f, 200f)) * Scale * Kind.Spin;   // 현상금은 천천히 돈다
            flashLeft = 0.06f;
            StopAllCoroutines(); StartCoroutine(Squash());
            // 저글링 감각 (사용자 10-14): 공중에 동시에 뜬 깡통 수(1·2·3·4+)로 소리·구름·불꽃·흔들림이 한 단계씩 커진다.
            // 숫자 글자는 띄우지 않는다 (10-12: 실시간 배율 글자 대신 세트 끝 점수판으로 정함)
            int air = Game.I != null ? Mathf.Max(1, Game.I.Airborne) : 1;
            int tier = Kind.NotScored ? 1 : Mathf.Clamp(air, 1, 4);
            Sfx.Play(Sfx.Tang(), 0.8f, Kind.Pitch * (1f + Mathf.Min(Combo, 12) * 0.03f) * TierPitch[tier - 1]);
            if (tier >= 3) Sfx.Play(Sfx.Tang(), 0.35f, Kind.Pitch * TierPitch[tier - 1] * 1.5f);   // 3개부터 5도 위 소리가 겹쳐 화음
            Fx.Spark(hitPoint, 5 + Mathf.Min(Combo, 6) + (tier - 1) * 3, 0.8f + Mathf.Min(Combo, 8) * 0.08f + (tier - 1) * 0.15f);
            if (!Done) Fx.Cloud(hitPoint, 0.45f * TierCloud[tier - 1]);   // 맞은 자리에 작은 흰 구름 (저글링일수록 크게)
            if (!Done && Kind.Special == CanKind.Trait.Magnet) MagnetPull();
            if (!Done && props.Count > 0 && Random.value < PropDropChance) DropProp(hitPoint);   // 부착물이 일정 확률로 떨어져 나간다
            Fx.Shake(0.15f + Mathf.Min(Combo, 8) * 0.03f + (tier - 1) * 0.05f + (Kind.Squish > 1f ? 0.25f : 0f));
            if (Kind.Squish > 1f) Sfx.Play(Sfx.Drop(), 0.55f, 0.55f);   // 묵직한 깡통: 낮은 쿵이 겹친다
            return earned;
        }

        /// <summary>
        /// 고도 감쇠 (사용자 10-14): 한 발의 튀는 힘이 맞은 높이에 따라 줄어든다. AltitudeLow(화면 가운데쯤) 아래는 1 그대로 →
        /// 위로 갈수록 부드럽게(smoothstep) 줄어 AltitudeHigh(천장 바로 아래)에서 AltitudeMin. 고정값이라 쉽게 천장에 닿던 것을 막는다
        /// </summary>
        // 시험 (꼭대기마다 계속 친 깡통의 꼭대기): 끔 0.9→4.4(천장) / 0.5·0.45 → 4.0 / 0·0.3 → 3.5에서 멈춤 (천장보다 1.2 아래). 중간(y≤1) 저글링은 90% 이상 그대로
        public static float AltitudeLow = 0f, AltitudeMin = 0.3f;
        static float AltitudeHigh => Look.CeilingY - 0.5f;
        public static float AltitudeFactor(float y)
        {
            float t = Mathf.Clamp01((y - AltitudeLow) / Mathf.Max(0.01f, AltitudeHigh - AltitudeLow));
            return Mathf.Lerp(1f, AltitudeMin, t * t * (3f - 2f * t));
        }

        // 공중 깡통 수 단계별: 탱 소리 높이 (도·레·미·솔), 맞은 자리 구름 크기
        static readonly float[] TierPitch = { 1f, 1.122f, 1.26f, 1.5f };
        static readonly float[] TierCloud = { 1f, 1.3f, 1.6f, 2f };
        /// <summary>
        /// 저글링 낙하 완화 (사용자 10-14: 공명 튐 대신 "저글링 중엔 기본적으로 낙하 속도 감소", 난이도 완화).
        /// 공중에 동시에 2·3·4+개면 떨어지는 동안만 중력 ×0.8·0.7·0.6. 오를 때는 그대로 → 꼭대기 높이·천장 한계는 안 바뀌고 내려오는 시간만 길어진다.
        /// 하나만 쏠 땐 혜택 없음. 스킬트리로 강화할 자리
        /// </summary>
        // 시간은 중력의 제곱근으로 늘어서 0.8/0.7/0.6은 거의 안 느껴졌다 (셋: 체공 0.96 → 1.04초) → 더 내림
        public static readonly float[] NoJuggleFall = { 1f, 1f, 1f, 1f };   // (먼저 선언: 정적 초기화는 적힌 순서대로)
        public static float[] JuggleFall = NoJuggleFall;   // 10-14: 기본은 완화 없음. 스킬 "공중 곡예"가 { 1, 0.7, 0.55, 0.45 }로
        static float JuggleFallMul()
        {
            int air = Game.I != null ? Game.I.Airborne : 1;
            return JuggleFall[Mathf.Clamp(air, 1, JuggleFall.Length) - 1];
        }

        IEnumerator Squash()
        {
            float t = 0f;
            while (t < 0.16f)
            {
                t += Time.deltaTime;
                float k = t / 0.16f;
                float s = Mathf.Sin(k * Mathf.PI);
                visual.localScale = new Vector3(1f + s * 0.35f * Kind.Squish, 1f - s * 0.3f * Kind.Squish, 1f);   // 묵직한 깡통은 크게 찌그러짐
                yield return null;
            }
            visual.localScale = Vector3.one;
        }

        int bouncesLeft = -1;

        // ---------- 현상금: 바닥에서 다시 튀어 오르기 + 도주 (사용자 10-14) ----------
        bool crouching;
        /// <summary>현상금이 바닥에서 스스로 튀어 오를 수 있는 남은 횟수 (사용자 10-14: 2회)</summary>
        public int ReboundsLeft = 2;
        /// <summary>
        /// 현상금이 바닥에 닿았다: 쿵 납작 → 0.5초 웅크림(그사이에도 쏠 수 있다) → 선반 높이쯤(y≈0.4)까지 스스로 튀어 오른다.
        /// 잃는 건 시간뿐. 끝은 도주 시간이 다 됐을 때 (Bounty.cs)
        /// </summary>
        IEnumerator BountyRebound()
        {
            crouching = true; held = true;   // held: 중력 0, 저글링 중력 갱신도 건너뜀
            SyncScale();
            rb.linearVelocity = Vector2.zero; rb.angularVelocity = 0f; rb.gravityScale = 0f;
            transform.position = new Vector3(transform.position.x, Look.DropY + Kind.Height * 0.5f, 0f);
            transform.rotation = Quaternion.identity;
            Sfx.Play(Sfx.Drop(), 0.8f, 0.5f); Fx.Dust(new Vector2(transform.position.x, Look.DropY + 0.1f), 6, 1f); Fx.Shake(0.4f);
            float sx = 1.4f, sy = 0.6f, vx = 0f, vy = 0f;
            for (float t = 0f; t < 0.5f && crouching; t += Time.deltaTime)
            {
                Ease.Spring(ref sx, ref vx, 1.08f, 600f, 18f, Time.deltaTime);   // 웅크린 채 (살짝 납작)
                Ease.Spring(ref sy, ref vy, 0.92f, 600f, 22f, Time.deltaTime);
                visual.localScale = new Vector3(sx, sy, 1f);
                visual.localPosition = new Vector3(0f, -(1f - sy) * Kind.Height * 0.5f, 0f);
                yield return null;
            }
            visual.localScale = Vector3.one; visual.localPosition = Vector3.zero;
            if (!crouching || Done) yield break;   // 웅크린 사이에 맞았으면 그 한 발로 이미 떴다
            crouching = false; held = false;
            float apex = 0.4f;
            float g = -Physics2D.gravity.y * BaseGravity * Kind.Weight;
            float v0 = Mathf.Sqrt(2f * g * Mathf.Max(0.5f, apex - transform.position.y));
            rb.gravityScale = Gravity;
            rb.linearVelocity = new Vector2(Random.Range(-0.6f, 0.6f), v0) * Scale;
            StartCoroutine(Squash());
            Sfx.Play(Sfx.Twirl(), 0.5f, 0.7f);
        }

        /// <summary>
        /// 파편에 맞았다 (현상금전, 사용자 10-14): 다른 깡통이 터진 파편이 날아와 체력을 깎고 현상금이 조금 오른다.
        /// 튀지는 않고 번쩍 + 찌그러짐만. 체력은 1까지만 — 마지막 한 발은 총으로 (막타 연출)
        /// </summary>
        public void TakeShrapnel(int damage, long bonus)
        {
            if (Done) return;
            HitsLeft = Mathf.Max(1, HitsLeft - damage);
            Earned += bonus;
            for (int i = 0; i < pips.Length; i++) pips[i].color = i < HitsLeft ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 0.4f, 0.3f, 0.35f);
            flashLeft = 0.08f;
            StartCoroutine(Squash());
            Sfx.Play(Sfx.Tang(), 0.6f, Kind.Pitch * 0.8f);
            Fx.Cloud(transform.position, 0.5f);
        }
        /// <summary>도주: 쌓인 돈만 정산하고 위로 휙 달아나 화면 밖으로 (현상금은 놓친다)</summary>
        public void RunAway()
        {
            if (Done) return;
            Done = true; crouching = false;
            rb.simulated = false;
            DropAllProps(transform.position, 1f);
            Game.I?.CanLanded(this);   // 쌓인 돈 정산 + ESCAPED 도장
            StopAllCoroutines();
            StartCoroutine(RunAwayCo());
        }
        IEnumerator RunAwayCo()
        {
            Sfx.Play(Sfx.Whoosh(), 0.7f, 0.8f); Fx.Cloud(transform.position, 1.2f);
            var p = transform.position; float v = 2f;
            for (float t = 0f; t < 1.2f && p.y < Look.CeilingY + 3f; t += Time.deltaTime) { v += 30f * Time.deltaTime; p.y += v * Time.deltaTime; transform.position = p; yield return null; }
            Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D c)
        {
            if (Done || !Airborne) return;
            if (c.GetComponent<DropZone>() == null) return;
            if (rb != null && rb.linearVelocity.y > 0f) return;   // 올라가는 중엔 바닥 판정 없음 (화면 아래에서 던져진 다이너마이트가 지나간다)
            if (ChainHeld) { SyncScale(); parked = true; rb.linearVelocity = Vector2.zero; rb.gravityScale = 0f; return; }   // 연쇄 차례를 기다리는 중: 바닥에서 멈춰 기다린다
            // 현상금은 떨어져도 바로 끝이 아니다 (사용자 10-14): 웅크렸다 스스로 튀어 오른다. 2번까지 — 그다음 바닥에선 도망간다
            if (Kind.IsMinion && Game.I != null && Game.I.BountyAlive) { StartCoroutine(BountyRebound()); return; }   // 부하: 현상금이 있는 동안 무한히 다시 뛴다
            if (Kind.IsBounty) { if (ReboundsLeft > 0) { ReboundsLeft--; StartCoroutine(BountyRebound()); } else RunAway(); return; }
            if (bouncesLeft < 0) bouncesLeft = IsMenu ? 0 : (Game.I != null ? Game.I.LandBounces : 0);
            if (bouncesLeft > 0)
            {
                // 스킬 "다시 튐": 떨어진 깡통이 혼자 한 번 다시 튀어오른다 (연속은 이어진다)
                bouncesLeft--;
                floorSlowed = false;
                SyncScale();
                var v = rb.linearVelocity; v.y = (11f + BounceBonus) * Scale; v.x *= 0.5f; rb.linearVelocity = v;
                transform.position = new Vector3(transform.position.x, Look.DropY + 0.5f, 0f);
                flashLeft = 0.06f;
                StartCoroutine(Squash());
                Sfx.Play(Sfx.Tang(), 0.5f, Kind.Pitch * 0.6f);
                return;
            }
            Land();
        }

        /// <summary>체력이 다했다: 터진다. 최고치 정산. 파편이 사방으로</summary>
        /// <summary>연쇄로 터진다 (현상금 깡통이 터질 때 공중의 다른 깡통들, 사용자 10-13). 보통 터뜨린 것과 똑같이 정산</summary>
        public void ChainBurst()
        {
            if (Done || !Airborne) return;
            SyncScale();
            Burst(transform.position);
        }

        void Burst(Vector2 at)
        {
            Done = true;
            Sfx.Play(Sfx.Bang(), 0.7f, Kind.Pitch * 1.6f);
            Sfx.Play(Sfx.Tang(), 0.9f, Kind.Pitch * 0.5f);
            Fx.Spark(transform.position, 14, 1.4f);
            Fx.Cloud(transform.position, 1.08f);   // 터진 자리에 소환 때와 같은 구름이 남았다 흩어진다 (사용자 10-13 시험)
            Fx.Shake(0.7f);
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
            if (IsMenu) OnMenuLanded(); else Game.I?.CanBurst(this);   // 타이틀 깡통은 터지면 바로 시작
            if (!IsMenu) TraitBurst(transform.position);
            StopAllCoroutines();
            rb.simulated = false;
            DropAllProps(at, 1.4f);   // 남은 부착물은 전부 튀어 나간다 (모자만 바닥에 남았다 사라짐)
            if (Kind.IsBounty)
            {
                // 큰 깡통은 그냥 꺼지지 않는다 (사용자 10-13): 그림을 그 자리에 떼어 두고 흰색으로 번쩍 → 살짝 부풀었다 작아지며 사라짐
                foreach (var p in pips) if (p != null) p.gameObject.SetActive(false);
                visual.SetParent(null, true);
                Fx.PopOut(visual, white);
            }
            else visual.gameObject.SetActive(false);
            Destroy(gameObject, 0.1f);
        }

        // ---------- 특성 (사용자 10-14) ----------
        const float MagnetRange = 2.0f;      // 옆의 옆까지 (깡통 간격 ≈ 0.85 × 2 + 여유)
        // 폭발 반경 = 화면 가로의 33% (사용자 10-14 "많이 큰 범위") ≈ 5.9 유닛 → 화면 2/3를 덮는다
        static float DynamiteRange => Look.PlayHalfW * 2f * 0.33f;
        const float BlastSpeed = 22f;        // 충격파가 퍼지는 빠르기 (유닛/초): 가장자리까지 ≈ 0.27초

        /// <summary>자석: 맞을 때마다 반경 안의 다른 깡통을 이쪽으로 끌어당긴다 (공중은 그대로 당김, 선반 위는 옆으로 미끄러짐)</summary>
        void MagnetPull()
        {
            if (Game.I == null) return;
            var at = (Vector2)transform.position;
            foreach (var c in Game.I.Cans)
            {
                if (c == null || c == this || c.Done) continue;
                float d = Vector2.Distance(c.transform.position, at);
                if (d > MagnetRange || d < 0.3f) continue;
                c.Pull(at, 2.2f + (MagnetRange - d) * 1.2f);
                Fx.Cloud(c.transform.position, 0.3f);
            }
            Sfx.Play(Sfx.Click(), 0.5f, 1.6f);
        }

        /// <summary>터질 때 특성: 다이너마이트 폭발 / 묶음이 풀려 셋 / 파란 깡통이 반투명 깡통 하나</summary>
        void TraitBurst(Vector2 at)
        {
            if (Game.I == null) return;
            switch (Kind.Special)
            {
                case CanKind.Trait.Dynamite:
                    Game.I.StartCoroutine(Explode(at, this));
                    break;
                case CanKind.Trait.Bundle:
                    // 끈이 끊어지며 셋이 튀어 오른다. 셋을 각자 때려야 하니, 처음 맞기 전까지 자기만 느리다(0.4배) (사용자 10-14)
                    float[] vx = { -2.4f, 0f, 2.4f };
                    for (int i = 0; i < 3; i++)
                        SpawnAirborne(CanKind.Loose, at + new Vector2(vx[i] * 0.12f, 0.1f * i), new Vector2(vx[i], 6.5f + (i == 1 ? 1f : 0f)), 0f, 1f, false, 0.4f);
                    Sfx.Play(Sfx.Drop(), 0.6f, 1.4f);
                    break;
                case CanKind.Trait.Blue:
                    // 반투명 깡통이 날아오른다 (사용자 10-14 2판: 특색 = 비행). 화면 가운데 줄로 올라가 3초 동안 좌우로 날다가 떨어진다
                    SpawnAirborne(CanKind.Ghost, at, new Vector2(0f, 6f), 0f, 1f, ghost: true).StartFlight(3f);
                    Sfx.Play(Sfx.Twirl(), 0.4f, 1.8f);
                    break;
            }
        }

        /// <summary>
        /// 다이너마이트 폭발: 큰 구름·불꽃·흔들림, 0.05초 뒤 반경 안의 깡통마다 한 발 (폭발 반대쪽으로 밀리며 뜬다).
        /// 선반 위 깡통도 맞아서 공중으로 → 연쇄. 맞은 깡통이 다이너마이트면 또 터진다
        /// </summary>
        static IEnumerator Explode(Vector2 at, Can source)
        {
            float range = DynamiteRange;
            Fx.Cloud(at, 2.2f);
            Fx.Spark(at, 40, 2.6f);
            Fx.Shake(1.4f);
            Sfx.Play(Sfx.Bang(), 1f, 0.5f);
            // 충격파: 소환 구름과 같은 흰 도트 구름이 고리 두 겹으로 퍼져 나간다 (반경 절반 → 끝)
            for (int ring = 0; ring < 2; ring++)
            {
                int n = ring == 0 ? 9 : 15; float r = range * (ring == 0 ? 0.45f : 0.9f);
                for (int i = 0; i < n; i++)
                {
                    float a = (i + ring * 0.5f) / n * Mathf.PI * 2f;
                    var p = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    Fx.CloudLater(p, ring == 0 ? 0.9f : 0.6f, r / BlastSpeed);
                }
            }
            var g = Game.I; if (g == null) yield break;
            // 가까운 깡통부터, 충격파가 닿는 때에 한 발씩 (선반 위 깡통도 떠오른다 → 연쇄)
            var targets = new System.Collections.Generic.List<(Can c, float d)>();
            foreach (var c in g.Cans.ToArray())
            {
                if (c == null || c == source || c.Done) continue;
                float d = Vector2.Distance(c.transform.position, at);
                if (d <= range) targets.Add((c, d));
            }
            targets.Sort((x, y) => x.d.CompareTo(y.d));
            float t = 0f;
            foreach (var (c, d) in targets)
            {
                float when = 0.05f + d / BlastSpeed;
                if (when > t) { yield return new WaitForSeconds(when - t); t = when; }
                if (c == null || c.Done) continue;
                Vector2 p = c.transform.position;
                var dir = (p - at).sqrMagnitude > 0.0001f ? (p - at).normalized : Vector2.up;
                var hitPoint = p - dir * 0.2f;   // 폭발 쪽 면을 맞힌다 → 반대쪽으로 튄다
                int earned = c.Hit(hitPoint, 1f, 1.8f, DynamiteDamage);   // 높게 뜬다 (천장 한계까지)
                g.CanHit(c, earned, hitPoint);
            }
        }

        /// <summary>다이너마이트 피해 (한 번에 깎는 체력). 스킬트리 "다이너마이트 강화"가 올린다 (사용자 10-14: 2~3)</summary>
        public static int DynamiteDamage = 2;
        /// <summary>쏠 때마다 다이너마이트를 던질 확률 (사용자 10-14: 1%). 스킬로 올릴 수 있게</summary>
        public static float DynamiteChance = 0.01f;

        /// <summary>
        /// 다이너마이트 투척 (사용자 10-14): 선반에 소환되는 게 아니라, 쏠 때 가끔 총 쪽에서 공중으로 던져진다.
        /// 선반 위 아무 자리 쪽으로 높이 떠서 2.5초 동안 중력 35%로 천천히 → 그사이 3발 맞히면 터진다. 못 맞히면 바닥에 떨어져 불발
        /// </summary>
        public static Can ThrowDynamite(Vector2 from) => ThrowDynamite();

        /// <summary>
        /// 다이너마이트 투척 2판 (사용자 10-14): 총이 아니라 화면 바닥 밖에서 던져져 화면 가운데 높이(≈y 0)까지 올라온다.
        /// 꼭대기 근처에서 천천히(중력 35%, 2.5초) → 그사이 3발
        /// </summary>
        public static Can ThrowDynamite()
        {
            float startY = -Look.PlayHalfW * 9f / 16f - 0.8f;               // 화면 아래 끝 바로 밖
            // 10-14 2차: 꼭대기 = 화면 높이의 2/3 (아래 끝 -5 → 위 끝 5에서 ≈ 1.67), 좌우는 화면 가운데 부근에서만
            float halfH = Look.PlayHalfW * 9f / 16f;
            float apexY = -halfH + halfH * 2f * (2f / 3f) + Random.Range(-0.25f, 0.25f);
            float x0 = Random.Range(-2.5f, 2.5f);
            float x1 = Mathf.Clamp(x0 + Random.Range(-1.5f, 1.5f), -2.5f, 2.5f);
            float g = -Physics2D.gravity.y * BaseGravity * CanKind.Dynamite.Weight * 0.35f;
            float vy = Mathf.Sqrt(2f * g * (apexY - startY)) * 1.06f;   // 공기 저항(감쇠 0.1)만큼 보탬: 없으면 꼭대기가 0.5쯤 낮았다
            float tUp = vy / g;
            var c = SpawnAirborne(CanKind.Dynamite, new Vector2(x0, startY), new Vector2((x1 - x0) / tUp, vy), 2.5f, 0.35f);
            Sfx.Play(Sfx.Twirl(), 0.6f, 1.3f);
            return c;
        }

        /// <summary>낙하지역에 떨어졌다: 정산하고 사라진다</summary>
        void Land()
        {
            Done = true;
            Sfx.Play(Sfx.Drop(), 0.6f, Kind.Pitch);
            Fx.Dust(new Vector2(transform.position.x, Look.DropY + 0.3f), 6, 1f);
            Fx.Shake(0.2f);
            if (IsMenu) OnMenuLanded(); else Game.I?.CanLanded(this);
            DropAllProps((Vector2)transform.position + Vector2.down, 0.6f);   // 바닥에 떨어지면 부착물도 벗겨져 나뒹군다
            StopAllCoroutines();
            StartCoroutine(Vanish());
        }

        /// <summary>
        /// 바닥 착지 (사용자 10-13 2판): 떨어지던 각도로 부딪혀 옆으로 90° 꺾여 눕는다 (기울어진 쪽, 똑바로면 날아가던 쪽으로).
        /// 닿는 순간 밑면 기준 납작(1.35×0.65) → 용수철 (소환 착지와 같은 값). 누운 뒤 잠깐 있다가 효과 없이 작아지며 사라진다
        /// (터질 때 구름·불꽃과 대비)
        /// </summary>
        IEnumerator Vanish()
        {
            float flyX = rb.linearVelocity.x;
            rb.simulated = false;
            if (white != null && flashLeft <= 0f) white.color = new Color(1f, 1f, 1f, 0f);
            foreach (var p in pips) if (p != null) p.gameObject.SetActive(false);   // 체력 점도 같이 끈다 (심심하게)
            // 떨어지던 각도 → 눕는 쪽(±90°). 거의 똑바로 떨어졌으면 날아가던 방향으로 넘어진다 (오른쪽으로 가면 시계 방향 = -90°)
            float rot = Mathf.DeltaAngle(0f, transform.eulerAngles.z), rv = 0f;
            float target = Mathf.Abs(rot) > 5f ? (rot > 0f ? 90f : -90f) : (flyX > 0f ? -90f : 90f);
            transform.rotation = Quaternion.identity;   // 회전은 그림(visual)이, 납작은 몸통(transform)이 화면 축으로
            float x = transform.position.x, hw = Kind.Width * 0.5f, hh = Kind.Height * 0.5f;
            float sx = 1.35f, sy = 0.65f, vx = 0f, vy = 0f;
            void Pose()
            {
                // 지금 각도에서 화면 세로 반높이 → 밑면이 바닥선에 붙어 있게 (넘어가는 동안 모서리로 짚는 느낌)
                float r = rot * Mathf.Deg2Rad;
                float halfNow = Mathf.Abs(Mathf.Cos(r)) * hh + Mathf.Abs(Mathf.Sin(r)) * hw;
                transform.localScale = new Vector3(sx, sy, 1f);
                transform.position = new Vector3(x, Look.DropY + halfNow * sy, 0f);
                visual.localPosition = Vector3.zero; visual.localScale = Vector3.one;
                visual.localRotation = Quaternion.Euler(0f, 0f, rot);
            }
            for (float t = 0f; t < 0.8f; t += Time.deltaTime)
            {
                float dt = Time.deltaTime;
                Ease.Spring(ref sx, ref vx, 1f, 1150f, 18f, dt);
                Ease.Spring(ref sy, ref vy, 1f, 1150f, 23f, dt);
                Ease.Spring(ref rot, ref rv, target, 900f, 16f, dt);   // 탁 꺾여 눕고 한 번 살짝 되튐
                Pose();
                if (t > 0.3f && Mathf.Abs(sx - 1f) < 0.01f && Mathf.Abs(vx) < 0.05f && Mathf.Abs(rot - target) < 0.5f) break;
                yield return null;
            }
            sx = sy = 1f; rot = target; Pose();
            yield return new WaitForSeconds(0.2f);
            // 심심하게: 소리·먼지 없이 제자리에서 작아지며 사라진다
            var s0 = transform.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.3f)
            {
                transform.localScale = s0 * (1f - Ease.InCubic(t));
                yield return null;
            }
            Destroy(gameObject);
        }

        static readonly System.Collections.Generic.List<Can> All = new System.Collections.Generic.List<Can>();
        UnityEngine.UI.Text earnedLabel;
        void OnDestroy() { All.Remove(this); if (earnedLabel != null) Destroy(earnedLabel.gameObject); }

        void Update()
        {
            // 지금 쌓인 돈이 깡통 위에 따라다닌다 (예측 가능한 점수)
            if (earnedLabel != null)
            {
                bool show = Airborne && !Done && Earned > 0;
                earnedLabel.gameObject.SetActive(show);
                if (show)
                {
                    earnedLabel.text = Cursed ? $"-${Earned:N0}" : $"${Earned:N0}";
                    earnedLabel.color = Cursed ? new Color(0.85f, 0.5f, 1f) : (HitsLeft <= 1 ? new Color(1f, 0.6f, 0.3f) : new Color(1f, 0.92f, 0.6f));
                    var sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * (Kind.Height * 0.5f + 0.42f));
                    ((RectTransform)earnedLabel.transform).anchoredPosition = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f) * (1080f / Screen.height);
                }
            }
            if (label == null)
            {
                // 그림 깡통: 번쩍임 = 흰 실루엣을 겹친다, 저주 = 보라로 물들인다
                if (flashLeft > 0f) { flashLeft -= Time.deltaTime; if (white != null) white.color = new Color(1f, 1f, 1f, flashLeft > 0f ? 1f : 0f); }
                body.color = Cursed ? new Color(0.75f, 0.45f, 0.9f, alpha) : new Color(1f, 1f, 1f, alpha);   // alpha: 반투명 깡통 (매 프레임 덮어써서 불투명해지던 버그, 10-14)
            }
            else if (flashLeft > 0f)
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
