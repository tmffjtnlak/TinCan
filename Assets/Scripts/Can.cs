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

        public static readonly CanKind Tin = new CanKind { Id = "tin", Name = "깡통", Desc = "값 1. 6번 맞으면 터진다.", Value = 1, Cost = 2, Price = 0, Hits = 6, Weight = 1f, Width = 16f / 27f, Height = 20f / 27f, Mass = 1f,   /* 크기 = 사용자 임시 그림 can_tin 16×20 (10-13) */ Pitch = 1f, Body = Look.Tin, Rim = Look.TinDark, Label = Look.Label };
        public static readonly CanKind Bottle = new CanKind { Id = "bottle", Name = "병", Desc = "값 3. 작고 무겁다. 4번이면 깨진다.", Value = 3, Cost = 8, Price = 60, Hits = 4, Weight = 1.15f, Width = 0.32f, Height = 0.9f, Mass = 0.8f, Pitch = 1.5f, Body = new Color(0.35f, 0.6f, 0.4f, 0.9f), Rim = new Color(0.25f, 0.4f, 0.28f), Label = new Color(0.9f, 0.85f, 0.7f) };
        public static readonly CanKind Rubber = new CanKind { Id = "rubber", Name = "고무 깡통", Desc = "값 2. 가벼워서 천천히 떨어진다. 8번이면 터진다.", Value = 2, Cost = 6, Price = 200, Hits = 8, Weight = 0.6f, Width = 0.5f, Height = 0.7f, Mass = 0.7f, Pitch = 0.85f, Body = new Color(0.9f, 0.45f, 0.5f), Rim = new Color(0.55f, 0.2f, 0.25f), Label = new Color(1f, 0.9f, 0.5f) };
        public static readonly CanKind Hat = new CanKind { Id = "hat", Name = "모자", Desc = "값 10. 납작하고 아주 가볍다. 10번이면 찢어진다.", Value = 10, Cost = 30, Price = 400, Hits = 10, Weight = 0.45f, Width = 0.7f, Height = 0.36f, Mass = 0.5f, Pitch = 0.7f, Body = new Color(0.35f, 0.22f, 0.14f), Rim = new Color(0.2f, 0.12f, 0.08f), Label = new Color(0.8f, 0.65f, 0.3f) };
        public string Id;

        /// <summary>깡통류 가게 목록 (순서대로)</summary>
        /// <summary>현상수배 깡통: 체력 30, 무겁고, 값 5. 터뜨리면 값×30×31 = 4,650 기본. 가게엔 없다</summary>
        public static readonly CanKind Bounty = new CanKind { Id = "bounty", Name = "현상수배 깡통", Desc = "체력 30. 무겁다. 터뜨리면 현상금.", Value = 5, Cost = 0, Price = 0, Hits = 30, Weight = 1.35f, Width = 0.8f, Height = 1.1f, Mass = 2f, Pitch = 0.6f, Body = new Color(0.55f, 0.18f, 0.14f), Rim = new Color(0.3f, 0.1f, 0.08f), Label = new Color(0.9f, 0.75f, 0.3f), IsBounty = true };
        public bool IsBounty;
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
        const float BaseGravity = 2.2f;

        float Gravity => BaseGravity * Kind.Weight * TimeScale * TimeScale;
        bool floorSlowed;

        void FixedUpdate()
        {
            if (rb == null || Done) return;
            if (!Mathf.Approximately(appliedScale, TimeScale))
            {
                float k = TimeScale / appliedScale;
                rb.linearVelocity *= k; rb.angularVelocity *= k;
                rb.gravityScale = held ? 0f : Gravity;
                appliedScale = TimeScale;
            }
            // 스킬 "바닥 근처 느려짐": 떨어지다 바닥 가까이 오면 속도가 반으로 (한 번만, 다시 맞으면 초기화)
            if (Airborne && !floorSlowed && Game.I != null && Game.I.FloorSlow > 0f && rb.linearVelocity.y < 0f && transform.position.y < Look.DropY + 1.6f)
            {
                floorSlowed = true;
                var v = rb.linearVelocity; v.y *= 1f - Game.I.FloorSlow; rb.linearVelocity = v;
                Fx.Dust(new Vector2(transform.position.x, transform.position.y - 0.3f), 2, 0.4f);
            }
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

        void Build(CanKind kind, Vector2 at)
        {
            Kind = kind;
            transform.position = at + Vector2.up * 0.8f;   // 위에서 툭 떨어져 선반에 앉는다 (10-13: 1.2 → 0.8, 소환 전체 40% 단축)
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.mass = kind.Mass; rb.linearDamping = 0.1f; rb.angularDamping = 0.6f; appliedScale = TimeScale;
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
            if (!IsMenu) Fx.Cloud(transform.position, 1f);
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
                    releasedAt = t; ReleaseHold();
                    if (rb != null) rb.linearVelocity = new Vector2(0f, -2.5f);
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
        public int Hit(Vector2 hitPoint, float scale = 1f, float bounceScale = 1f)
        {
            if (Done) return 0;
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
            int earned = Game.I != null ? Game.I.EarnFor(Kind, Combo, scale) : Kind.Value * Combo;
            Earned += earned;
            HitsLeft--;
            for (int i = 0; i < pips.Length; i++) pips[i].color = i < HitsLeft ? new Color(1f, 1f, 1f, 0.9f) : new Color(1f, 0.4f, 0.3f, 0.35f);
            if (HitsLeft <= 0) { Burst(hitPoint); return earned; }
            Vector2 off = hitPoint - (Vector2)transform.position;
            float side = Mathf.Clamp(-off.x / Mathf.Max(0.01f, Kind.Width * 0.5f), -1f, 1f);
            var v = rb.linearVelocity / TimeScale;   // 느려진 시간 안에서 계산하고 다시 줄인다
            // 기본 튐은 보수적으로 (리볼버 한 발 = 선반에서 화면 중간쯤). 총 배수·스킬로 위로 갈라진다. 천장은 안전망
            v.y = (Mathf.Max(0f, v.y * 0.3f) + 7.2f + BounceBonus + Random.Range(-0.4f, 0.6f)) * bounceScale;
            v.x = v.x * 0.4f + side * 2.2f + Random.Range(-0.6f, 0.6f);
            // 천장 한계 (사용자 10-11): 화면 밖으로 나가면 연속이 끊기니, 꼭대기가 화면 위 끝 바로 아래가 되도록 자른다
            float top = Look.CeilingY - Kind.Height * 0.5f - transform.position.y;
            float g = -Physics2D.gravity.y * BaseGravity * Kind.Weight;
            float vmax = top > 0f ? Mathf.Sqrt(2f * g * top) : 0f;
            if (v.y > vmax) v.y = vmax;
            rb.linearVelocity = v * TimeScale;
            rb.angularVelocity = (-side * Random.Range(400f, 800f) + Random.Range(-200f, 200f)) * TimeScale;
            flashLeft = 0.06f;
            StopAllCoroutines(); StartCoroutine(Squash());
            Sfx.Play(Sfx.Tang(), 0.8f, Kind.Pitch * (1f + Mathf.Min(Combo, 12) * 0.03f));
            Fx.Spark(hitPoint, 5 + Mathf.Min(Combo, 6), 0.8f + Mathf.Min(Combo, 8) * 0.08f);
            Fx.Shake(0.15f + Mathf.Min(Combo, 8) * 0.03f);
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
            if (bouncesLeft < 0) bouncesLeft = IsMenu ? 0 : (Game.I != null ? Game.I.LandBounces : 0);
            if (bouncesLeft > 0)
            {
                // 스킬 "다시 튐": 떨어진 깡통이 혼자 한 번 다시 튀어오른다 (연속은 이어진다)
                bouncesLeft--;
                floorSlowed = false;
                var v = rb.linearVelocity; v.y = (11f + BounceBonus) * TimeScale; v.x *= 0.5f; rb.linearVelocity = v;
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
            Fx.Ring(transform.position, Cursed ? CurseLabel : new Color(1f, 0.95f, 0.7f), 2.2f);
            Fx.Spark(transform.position, 14, 1.4f);
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
            Fx.Dust(new Vector2(transform.position.x, Look.DropY + 0.3f), 6, 1f);
            Fx.Shake(0.2f);
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
                body.color = Cursed ? new Color(0.75f, 0.45f, 0.9f) : Color.white;
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
