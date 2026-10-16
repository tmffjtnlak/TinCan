using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinCan
{
    /// <summary>
    /// 손에 든 총. 종류(GunKind)가 쏘는 방식을 정하고, 스킬이 숫자를 보탠다.
    /// 마우스 조준, 좌클릭 사격. 다 쓰면 잠깐 뒤 자동 장전 (R로 즉시).
    /// </summary>
    public class Gun : MonoBehaviour
    {
        public GunKind Kind { get; private set; }   // Build에서 Equip으로 채운다
        public int Capacity => Kind.Capacity + CapacityBonus;
        public int CapacityBonus { get; set; }
        public int Ammo { get; private set; }
        public bool Reloading { get; private set; }
        /// <summary>스킬 "빠른 장전": 장전 시간 배수 (1 = 그대로)</summary>
        public float ReloadScale { get; set; } = 1f;
        /// <summary>스킬 "빠른 손": 쏘는 간격 배수 (10-15 사용자: 초반 노드는 간격·장전 줄이기에 집중 — Click the Button식)</summary>
        public float FireIntervalScale { get; set; } = 1f;
        public float ReloadSeconds => Kind.ReloadSeconds * ReloadScale;
        public int Shots { get; private set; }
        public int Hits { get; private set; }
        /// <summary>스킬트리가 열려 있으면 꺼진다</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>"연속 장전": 공중 연속 N번마다 즉시 장전. 0 = 없음</summary>
        public int ComboReloadEvery { get; set; } = 0;
        /// <summary>스킬 "첫 발": 장전 직후 첫 발 값 배수 (1 = 없음)</summary>
        public float FirstShotMult { get; set; } = 1f;
        /// <summary>스킬 "마지막 발": 탄창 마지막 발의 튀는 힘 배수 (1 = 없음)</summary>
        public float LastShotBounce { get; set; } = 1f;
        bool firstShotPending;

        Transform crosshair, ring;
        GunView view;
        public GunView View => view;
        Camera cam;
        float kickLeft, nextFire, shotValue = 1f, shotBounce = 1f;
        Vector3 camHome;

        // 저스트 장전 (엔터 더 건전식): 조준점 위 칸, 슬래시 구간에 R/클릭이면 즉시 장전.
        // 성공할수록 다음 장전이 빨라지고 구간이 좁아진다. 실패하면 초기화 + 이번 장전은 느려진다.
        public int ReloadStreak { get; private set; }
        public float ReloadProgress { get; private set; }   // 0..1
        float windowStart, windowEnd;
        bool attempted;
        Transform bar; SpriteRenderer barFill, barWindow, barBg;

        public float ActiveReloadSeconds => Mathf.Max(0.35f, ReloadSeconds * (1f - 0.07f * ReloadStreak));
        // 구간은 고정 (사용자 10-10): 자리도 폭도 늘 같다. 어려워지는 건 속도뿐
        // 저스트 구간: 10-14 사용자 "쏘면서 맞추기 빡세다" → 폭 50% 넓힘 (0.62~0.74 → 가운데 0.68 그대로 0.59~0.77)
        const float WindowStart = 0.59f, WindowEnd = 0.77f;
        /// <summary>장전 앞쪽 30%의 입력은 무시한다 (실패로 치지 않음, 사용자 10-14) — 다 쏘고 계속 누르던 클릭이 실패가 되던 문제</summary>
        const float IgnoreBefore = 0.3f;

        // ---------- 도트 조준점 ----------
        const float Dot = 1f / 27f;          // 화면 480×270의 한 도트 (월드)
        const int ArmLen = 3, ArmGap = 4;    // 팔 길이 3도트, 가운데에서 팔 가운데까지 4도트 (사이 빈칸 2도트)
        Transform[] arms;
        static Sprite PixelBar(int w, int h)
        {
            // 흰 몸 w×h + 둘레 남색 1도트 (모서리 포함)
            int W = w + 2, H = h + 2;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var line = new Color(0.15f, 0.13f, 0.25f, 1f);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    tex.SetPixel(x, y, x >= 1 && x <= w && y >= 1 && y <= h ? Color.white : line);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 27f);
        }
        // ---------- 도트 장전 바 부품 ----------
        const int BarDots = 33;   // 점선 길 길이 (홀수: 가운데가 0)
        static readonly string[] BracketArt =
        {
            "XXXX",
            "XWWX",
            "XWXX",
            "XWX.",
            "XWX.",
            "XWX.",
            "XWXX",
            "XWWX",
            "XXXX",
        };
        static SpriteRenderer MakeSR(string name, Sprite sp, int order, Transform parent)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>(); sr.transform.SetParent(parent, false);
            sr.sprite = sp; sr.sortingOrder = order; return sr;
        }
        /// <summary>글자 그림 → 도트 스프라이트 (W = 흰, X = 남색, . = 빈칸). mirror면 좌우 뒤집기</summary>
        static Sprite Art(string[] rows, bool mirror)
        {
            int h = rows.Length, w = rows[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var line = new Color(0.15f, 0.13f, 0.25f, 1f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    char c = rows[h - 1 - y][mirror ? w - 1 - x : x];
                    tex.SetPixel(x, y, c == 'W' ? Color.white : c == 'X' ? line : Color.clear);
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 27f);
        }
        /// <summary>점선 길: 가운데 줄 = 흰 2칸·빈 1칸 반복, 위아래 줄 = 남색 테</summary>
        static Sprite DottedTrack(int w)
        {
            var tex = new Texture2D(w, 3, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var line = new Color(0.15f, 0.13f, 0.25f, 1f);
            for (int x = 0; x < w; x++)
            {
                tex.SetPixel(x, 0, line); tex.SetPixel(x, 2, line);
                tex.SetPixel(x, 1, new Color(1f, 1f, 1f, 0.9f));   // 끊김 없는 한 줄 (사용자 10-13)
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, 3), new Vector2(0.5f, 0.5f), 27f);
        }

        /// <summary>팔 자리: 기본 4도트 + 벌림(도트). 0 = N, 1 = S, 2 = E, 3 = W</summary>
        void PlaceArms(int spreadDots)
        {
            float d = (ArmGap + spreadDots) * Dot;
            arms[0].localPosition = new Vector3(0f, d, 0f); arms[1].localPosition = new Vector3(0f, -d, 0f);
            arms[2].localPosition = new Vector3(d, 0f, 0f); arms[3].localPosition = new Vector3(-d, 0f, 0f);
        }

        public void Build()
        {
            cam = Camera.main; camHome = cam.transform.position;
            Cinematic = false; Time.timeScale = Game.DebugTime; Time.fixedDeltaTime = 0.02f * Game.DebugTime;   // 연출 도중 씬이 바뀌었어도 원래대로 (디버그 슬로우 F10은 유지)
            Cursor.visible = false;
            crosshair = new GameObject("Crosshair").transform;
            // 도트 조준점 (사용자 10-13): 흰 점 + 흰 팔 4개(3도트), 남색 1도트 외곽선. 크기를 늘리지 않고 팔을 도트 단위로 밀어 벌린다
            var center = new GameObject("Dot").AddComponent<SpriteRenderer>(); center.transform.SetParent(crosshair, false);
            center.sprite = PixelBar(1, 1); center.sortingOrder = 50;
            arms = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var a = new GameObject("Arm" + i).AddComponent<SpriteRenderer>(); a.transform.SetParent(crosshair, false);
                a.sprite = i < 2 ? PixelBar(1, ArmLen) : PixelBar(ArmLen, 1); a.sortingOrder = 50;
                arms[i] = a.transform;
            }
            PlaceArms(0);
            // 판정 원 테두리 (샷건처럼 넓은 총일 때 보인다): 점 16개
            ring = new GameObject("Ring").transform; ring.SetParent(crosshair, false);
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                Look.Quad("R", new Vector2(Mathf.Cos(a), Mathf.Sin(a)), new Vector2(0.05f, 0.05f), new Color(1f, 1f, 1f, 0.45f), 50, ring);
            }
            view = new GameObject("GunViewHolder").AddComponent<GunView>();
            view.Build();
            // 장전 칸 (조준점 위)
            // 도트 장전 바 (사용자 10-13): [ -- -- --|-- -- ] — 남색 테 두른 점선 길 + 양 끝 꺾쇠 + 저스트 구간(노랑) + 움직이는 세로 막대
            bar = new GameObject("ReloadBar").transform; bar.SetParent(crosshair, false); bar.localPosition = new Vector2(0f, 14f * Dot);
            barBg = MakeSR("Track", DottedTrack(BarDots), 51, bar);
            MakeSR("BracketL", Art(BracketArt, false), 51, bar).transform.localPosition = new Vector2(-(BarDots / 2 + 2) * Dot, 0f);
            MakeSR("BracketR", Art(BracketArt, true), 51, bar).transform.localPosition = new Vector2((BarDots / 2 + 2) * Dot, 0f);
            // 저스트 타이밍 = 노란 세로줄 한 줄 (1×5 도트, 사용자 10-13)
            barWindow = Look.Quad("Window", Vector2.zero, new Vector2(Dot, 5f * Dot), new Color(1f, 0.85f, 0.25f, 1f), 52, bar);
            barFill = MakeSR("Cursor", PixelBar(1, 5), 53, bar);
            bar.gameObject.SetActive(false);
            Equip(GunKind.Rusty);
        }

        // 총마다 탄 상태를 기억한다 (주/보조 교대용)
        readonly System.Collections.Generic.Dictionary<string, int> ammoByKind = new System.Collections.Generic.Dictionary<string, int>();
        int reloadToken;

        /// <summary>허리춤 장전 스킬: 꺼내지 않은 총에 한 발</summary>
        public void HolsterFeed(GunKind kind)
        {
            if (kind == null || kind == Kind) return;
            int cap = kind.Capacity + CapacityBonus;
            int a = ammoByKind.TryGetValue(kind.Id, out var v) ? v : cap;
            if (a < cap) ammoByKind[kind.Id] = a + 1;
        }
        public int StoredAmmo(GunKind kind) => kind == Kind ? Ammo : (ammoByKind.TryGetValue(kind.Id, out var v) ? v : kind.Capacity + CapacityBonus);

        public void Equip(GunKind kind)
        {
            if (Kind != null) ammoByKind[Kind.Id] = Ammo;
            reloadToken++; Reloading = false; attempted = false;
            Kind = kind;
            Ammo = ammoByKind.TryGetValue(kind.Id, out var stored) ? Mathf.Min(stored, Capacity) : Capacity;
            view.SetKind(kind);
            ring.localScale = Vector3.one * kind.AimRadius;
            ring.gameObject.SetActive(kind.AimRadius > 0.4f);
        }

        public void OnComboHit(int combo)
        {
            if (ComboReloadEvery > 0 && combo > 0 && combo % ComboReloadEvery == 0 && Ammo < Capacity)
            {
                Reloading = false;
                Ammo = Capacity; firstShotPending = true;
                Sfx.Play(Sfx.Reload(), 0.7f, 2.4f);
            }
        }

        /// <summary>지금 총구 자리 (다이너마이트 투척 시험용)</summary>
        public Vector2 MuzzlePos => view.Muzzle;

        /// <summary>시험용: 마우스 대신 이 자리를 겨눈다</summary>
        public static Vector2? AimOverride;

        Vector2 MouseWorld()
        {
            if (AimOverride.HasValue) return AimOverride.Value;
            var m = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            var w = cam.ScreenToWorldPoint(new Vector3(m.x, m.y, 10f));
            return new Vector2(w.x, w.y);
        }

        void Update()
        {
            var aim = MouseWorld();
            crosshair.position = new Vector3(Mathf.Round(aim.x / Dot) * Dot, Mathf.Round(aim.y / Dot) * Dot, 0f);   // 보이는 조준점은 도트 칸에 (판정은 aim 그대로)
            crosshair.gameObject.SetActive(Enabled);
            Cursor.visible = !Enabled;
            view.Tick(aim, Enabled);
            DeadEyeTick(aim);
            if (!Enabled) return;
            var mouse = Mouse.current; var kb = Keyboard.current;
            bool pressR = kb != null && kb.rKey.wasPressedThisFrame;
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;
            if (Reloading)
            {
                if ((pressR || click) && !attempted) ActiveReload();
            }
            else
            {
                if (pressR && Ammo < Capacity) StartCoroutine(Reload());
                if (mouse != null)
                {
                    bool want = Kind.Auto ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
                    if (want && Time.time >= nextFire) { Fire(aim); nextFire = Time.time + Kind.FireInterval * FireIntervalScale; }
                }
            }
            // 장전 칸 그리기
            bar.gameObject.SetActive(Reloading);
            if (Reloading)
            {
                // 도트 칸 단위: 막대는 한 칸씩 움직이고, 저스트 구간은 점선 가운데 줄을 칸 단위로 덮는다
                int half = BarDots / 2;
                int cx = Mathf.Clamp(Mathf.RoundToInt(ReloadProgress * BarDots) - half, -half, half);
                barFill.transform.localPosition = new Vector2(cx * Dot, 0f);
                int x0 = Mathf.RoundToInt(windowStart * BarDots) - half, x1 = Mathf.Max(x0 + 1, Mathf.RoundToInt(windowEnd * BarDots) - half);
                barWindow.transform.localPosition = new Vector2(Mathf.RoundToInt((x0 + x1 - 1) * 0.5f) * Dot, 0f);
                barWindow.transform.localScale = new Vector3(Dot, 5f * Dot, 1f);
                barWindow.color = attempted ? (missed ? new Color(1f, 0.3f, 0.2f, 1f) : new Color(0.5f, 1f, 0.6f, 1f)) : new Color(1f, 0.85f, 0.25f, 1f);
            }

            // 쏠 때 벌어짐: 예전엔 전체를 키웠다(도트가 찌그러짐) → 팔만 도트 단위로 밀어낸다 (최대 약 8도트)
            PlaceArms(Mathf.RoundToInt(kickLeft * 8f * Mathf.Min(1f, Kind.Kick)));
            if (kickLeft > 0f && !Cinematic)
            {
                kickLeft = Mathf.Max(0f, kickLeft - Time.unscaledDeltaTime * 5f);
                cam.transform.position = camHome + (Vector3)Random.insideUnitCircle * kickLeft * 0.12f * Kind.Kick;
                if (kickLeft <= 0f) cam.transform.position = camHome;
            }
        }

        // ---------- 현상금 막타 연출 (사용자 10-13, 2판) ----------
        // 평소 총알은 즉시 맞는다(히트스캔). 현상금 깡통의 마지막 한 발만:
        //  1) 시간이 크게 느려지고 레터박스가 올라온다
        //  2) 얇은 직선 섬광 총알이 총구 → 깡통으로 날아가고, 카메라가 그 총알을 따라가며 깡통 쪽으로 줌인
        //  3) 닿는 순간 거의 멈춤: 흰 번쩍임·고리 둘·불꽃·큰 흔들림·소환 구름(크게)·금 탄피 한 무더기
        //  4) 슬로우와 줌이 천천히 풀리면서 탄피가 사방으로 후두두둑 떨어지고, 바닥에 눕는 것부터 점수판으로 빨려 간다
        /// <summary>연출 중 (이때는 쏘지 않는다)</summary>
        public static bool Cinematic { get; private set; }
        bool IsFinisher(Can can) => !Cinematic && can.Kind.IsBounty && can.HitsLeft <= 1 && !can.Done && !can.IsMenu;
        const float FinisherZoom = 0.5f;      // 깡통에 닿을 때 카메라 크기 (기본의 0.5배 = 2배 확대)

        /// <summary>
        /// 모독 막타 (사용자 10-16): 이 한 발로 터질 연쇄가 최고 기록을 넘으면 현상금 막타처럼 — 레터박스, 느린 총알, 첫 깡통으로 줌인,
        /// 이어 터지는 깡통들을 카메라가 따라가다 마지막 깡통쯤에 풀린다
        /// </summary>
        bool IsDefileFinisher(Can can, out int predicted)
        {
            predicted = 0;
            var g = Game.I;
            if (Cinematic || g == null || g.DefileRange <= 0f || can.HitsLeft > 1 || can.Done || can.IsMenu || can.Kind.IsBounty || can.Kind.IsMinion) return false;
            predicted = g.PredictDefile(can);
            return predicted >= 3 && predicted > g.BestChain;
        }

        IEnumerator Finisher(Can can) => Finisher(can, false, 0);
        IEnumerator Finisher(Can can, bool defile, int predicted)
        {
            Cinematic = true;
            Letterbox.Force = true;
            kickLeft = 0f;
            const float baseFixed = 0.02f;
            void SetTime(float s) { s *= Game.DebugTime; Time.timeScale = s; Time.fixedDeltaTime = baseFixed * Mathf.Max(0.02f, s); }   // 디버그 슬로우(F10) 위에 곱한다
            float size0 = cam.orthographicSize;
            Vector3 camStart = cam.transform.position;
            // 카메라가 배경 밖을 비추지 않게: 크기 s일 때 가운데가 갈 수 있는 범위
            Vector3 Clamp(Vector2 p, float s)
            {
                float hw = Mathf.Max(0f, size0 * cam.aspect - s * cam.aspect), hh = Mathf.Max(0f, size0 - s);
                return new Vector3(Mathf.Clamp(p.x, camStart.x - hw, camStart.x + hw), Mathf.Clamp(p.y, camStart.y - hh, camStart.y + hh), camStart.z);
            }
            void SetCam(Vector2 center, float s)
            {
                cam.orthographicSize = s;
                var pos = Clamp(center, s);
                cam.transform.position = pos;
                Fx.CamFocus = pos;   // 흔들림도 이 자리를 기준으로
            }
            SpriteRenderer[] streak = null;
            try
            {
                // 1) 시간이 0.12초 만에 0.18배로 (OutCubic: 확 느려졌다 가라앉음)
                const float SlowStart = 0.18f, SlowEnd = 0.015f;
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.12f) { SetTime(Mathf.Lerp(1f, SlowStart, Ease.OutCubic(t))); yield return null; }
                SetTime(SlowStart);
                Sfx.Play(Sfx.Whoosh(), 0.7f, 0.5f);
                // 2) 얇은 직선 총알 (10-13 4판: "슈우---------우---우"): 비행 1.2초 동안 시간이 0.18 → 0.015배로 계속 느려진다 (맞기 직전이 가장 느림).
                //    총알도 그 시간 흐름을 그대로 탄다 = 진행도는 시간 배수의 적분. 출발은 빠르고 갈수록 기어가듯 다가간다. 카메라도 같은 진행도로 따라가며 줌인
                // 총알 = 한 줄기 섬광 (10-13 5판): 같은 색 6칸으로 나눠 꼬리 끝 두 칸만 흐리게. 길이는 빠르기에 비례 → 깡통 앞에서 느려지면 짧아진다
                var streakColor = new Color(1f, 0.96f, 0.78f, 1f);
                float[] segAlpha = { 1f, 1f, 1f, 1f, 0.55f, 0.25f };   // 0 = 머리 … 5 = 꼬리 끝
                streak = new SpriteRenderer[segAlpha.Length];
                for (int i = 0; i < streak.Length; i++) streak[i] = Look.Quad("FinisherStreak" + i, Vector2.zero, Vector2.one, new Color(streakColor.r, streakColor.g, streakColor.b, segAlpha[i]), 46);
                Vector2 from = view.Muzzle;
                const float Flight = 1.2f, Thin = 1f / 27f;
                float Ts(float u) => Mathf.Lerp(SlowStart, SlowEnd, Ease.OutQuad(u));   // 처음에 빨리 느려지고 끝으로 갈수록 더 천천히 바닥에 가라앉는다
                float total = 0f; for (int i = 0; i < 200; i++) total += Ts((i + 0.5f) / 200f) / 200f;
                float acc = 0f, prevT = 0f;
                for (float t = 0f; t < 1f && can != null && !can.Done; t += Time.unscaledDeltaTime / Flight)
                {
                    float u = Mathf.Min(1f, t);
                    SetTime(Ts(u));
                    acc += Ts((prevT + u) * 0.5f) * (u - prevT); prevT = u;
                    float k = Mathf.Clamp01(acc / total);
                    Vector2 to = can.transform.position;
                    Vector2 p = Vector2.Lerp(from, to, k);
                    var dir = (to - from); float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    var hDir = dir.normalized;
                    // 줄기 길이 = 지금 빠르기 × 0.12초 (최소 3도트, 총구를 넘지 않게). 빠르기 = 거리 × 시간배수 / (적분 합 × 비행시간)
                    float speed = dir.magnitude * Ts(u) / (total * Flight);
                    float len = Mathf.Clamp(speed * 0.12f, 3f * Thin, Vector2.Distance(from, p));
                    float seg = len / streak.Length;
                    for (int i = 0; i < streak.Length; i++)
                    {
                        streak[i].transform.position = p - hDir * seg * (i + 0.5f);
                        streak[i].transform.rotation = Quaternion.Euler(0f, 0f, ang);
                        streak[i].transform.localScale = new Vector3(seg + Thin * 0.2f, Thin, 1f);   // 칸 사이 틈이 안 보이게 살짝 겹친다
                    }
                    // 카메라: 총알 머리를 따라가며(조금 앞서) 깡통 쪽으로 줌인. 총알과 같은 진행도라 도착할 때 같이 선다
                    float camK = Ease.InOutCubic(k);
                    SetCam(Vector2.Lerp(camStart, Vector2.Lerp(p, to, 0.35f), camK), Mathf.Lerp(size0, size0 * FinisherZoom, camK));
                    yield return null;
                }
                foreach (var s in streak) Destroy(s.gameObject); streak = null;
                if (can == null || can.Done) yield break;
                // 3) 닿는 순간 (10-13 3판): 슬로우는 여기까지. 터지면서 시간이 0.12초 만에 확 원래대로 (슬로우가 연출을 드러내지 않게)
                Vector2 at = can.transform.position;
                SetCam(at, size0 * FinisherZoom * 0.92f);   // 맞는 순간 한 번 더 당긴다 (펀치)
                HitOne(can, at);
                if (!defile) StartCoroutine(ChainBursts(at));   // 연출 동안 조준을 못 했으니 공중의 다른 깡통들은 가까운 것부터 차례로 터진다
                Fx.Spark(at, 26, 1.8f);
                Fx.Cloud(at, 1.6f);
                Fx.Shake(1.5f);
                StartCoroutine(Flash());
                Sfx.Play(Sfx.Bang(), 1f, 0.35f);
                // 탄피는 Game.CanBurst가 흘린다 (현상금은 금 탄피 48개, 바닥에 쌓였다가 세트 끝에 정산과 함께 흡수)
                // 3-1) 모독 (10-16 사용자: "우응--- 펑 - 파바바바바방"): 슬로우는 첫 터짐까지. 펑 하고 한 숨 머문 뒤 시간이 확 풀리고,
                //      연쇄는 제 속도로 빠르게 몰아친다. 카메라는 터지는 깡통들을 따라가며 조금씩 물러나고, 마지막 깡통쯤(예상 수 - 1)에 풀기 시작
                if (defile)
                {
                    for (float t = 0f; t < 0.15f; t += Time.unscaledDeltaTime) yield return null;
                    float slow0 = Time.timeScale;
                    for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.1f) { SetTime(Mathf.Lerp(slow0, 1f, Ease.OutQuad(t))); yield return null; }
                    SetTime(1f);
                    var g = Game.I;
                    for (float t = 0f; t < 4f && g != null && g.Cascading && g.CascadeCount < predicted - 1; t += Time.unscaledDeltaTime)
                    {
                        float s = Mathf.Lerp(size0 * FinisherZoom * 0.92f, size0 * 0.75f, Mathf.Clamp01(g.CascadeCount / (float)predicted));
                        var p = Vector2.Lerp(cam.transform.position, g.CascadeFocus, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 4f));
                        SetCam(p, Mathf.Lerp(cam.orthographicSize, s, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3f)));
                        yield return null;
                    }
                }
                // 4) 시간은 확(0.12초, OutQuad), 카메라는 조금 늦게 빠르게(0.5초, OutCubic) 원래대로. 레터박스는 0.25초에 빠지기 시작
                Vector2 camFrom = cam.transform.position; float sizeFrom = cam.orthographicSize;
                float slowFrom = Time.timeScale;
                for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
                {
                    SetTime(Mathf.Lerp(slowFrom, 1f, Ease.OutQuad(Mathf.Clamp01(t / 0.12f))));
                    float z = Ease.OutCubic(t / 0.5f);
                    SetCam(Vector2.Lerp(camFrom, camStart, z), Mathf.Lerp(sizeFrom, size0, z));
                    if (t > 0.25f) Letterbox.Force = false;
                    yield return null;
                }
            }
            finally
            {
                if (streak != null) foreach (var s in streak) if (s != null) Destroy(s.gameObject);
                SetTime(1f);
                cam.orthographicSize = size0;
                cam.transform.position = camStart;
                Fx.CamFocus = null;
                Letterbox.Force = false;
                Cinematic = false;
            }
        }

        /// <summary>
        /// 연쇄 터짐 (사용자 10-13): 현상금 깡통이 터지고 0.5초 뒤, 그 순간 공중에 떠 있던 다른 깡통들이 가까운 것부터 0.15초 간격으로
        /// 퍼-퍼-퍼-펑 터진다 (한 번에 말고 차례로). 터질 때마다 소리가 조금씩 높아진다. 선반 위(아직 안 쏜) 깡통은 그대로
        /// </summary>
        /// <summary>현상금이 막타 연출 밖에서(다이너마이트 등) 터졌을 때도 연쇄를 건다 (10-14)</summary>
        public void StartChain(Vector2 from) => StartCoroutine(ChainBursts(from));

        IEnumerator ChainBursts(Vector2 from)
        {
            var targets = new List<Can>();
            foreach (var c in Game.I.Cans) if (c != null && !c.Done && c.Airborne && !c.Kind.IsBounty) targets.Add(c);
            targets.Sort((a, b) => ((Vector2)a.transform.position - from).sqrMagnitude.CompareTo(((Vector2)b.transform.position - from).sqrMagnitude));
            foreach (var c in targets) c.HoldForChain();   // 차례가 올 때까지 이 깡통들만 거의 멈춰 매달린다 (그 사이 떨어져 사라지지 않게)
            yield return new WaitForSecondsRealtime(0.5f);    // 큰 터짐이 가라앉고 나서 시작 (10-13: 0.15 → 0.5초)
            int n = 0;
            foreach (var c in targets)
            {
                if (c == null || c.Done || !c.Airborne) continue;
                c.ChainBurst();
                Sfx.Play(Sfx.Tang(), 0.5f, 1f + n * 0.08f);
                n++;
                yield return new WaitForSecondsRealtime(0.15f);   // 10-13: 0.1 → 0.15초 간격
            }
        }

        /// <summary>화면 전체 흰 번쩍임 (0.15초)</summary>
        IEnumerator Flash()
        {
            var f = Look.Quad("Flash", cam.transform.position, new Vector2(40f, 30f), new Color(1f, 1f, 1f, 0.7f), 60);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.15f) { f.transform.position = (Vector2)cam.transform.position; f.color = new Color(1f, 1f, 1f, 0.7f * (1f - t)); yield return null; }
            Destroy(f.gameObject);
        }

        // ---------- 데드아이 (10-16 사용자: 동체시력의 벽을 넘는 길은 시간뿐 → 성장 축 = 슬로우. 레드 데드 리뎀션의 데드아이) ----------
        // 슬로우 중 조준점이 깡통에 들어갈 때마다 표시가 하나 붙는다 (나갔다 다시 들어오면 또 붙어서 한 깡통에 여러 발도).
        // 슬로우가 풀리는 순간 표시한 깡통을 0.07초 간격으로 차례로 쏜다. 표시 수 = 스킬(DeadEyeMarks)과 남은 탄 중 작은 쪽
        readonly List<Can> marks = new List<Can>();
        readonly List<SpriteRenderer> markViews = new List<SpriteRenderer>();
        Can lastOver; bool volleying;
        /// <summary>슬로우가 풀렸다 (Game.UpdateSlow가 부른다): 표시가 있으면 연발</summary>
        public void SlowEnded() { if (marks.Count > 0 && !volleying) StartCoroutine(Volley()); }

        void DeadEyeTick(Vector2 aim)
        {
            var g = Game.I;
            if (g == null) return;
            int max = g.DeadEyeMarks;
            if (Enabled && g.SlowActive && max > 0 && !volleying)
            {
                Can over = null;
                foreach (var col in Physics2D.OverlapCircleAll(aim, Mathf.Max(Kind.AimRadius, 0.3f)))
                {
                    var c = col.GetComponent<Can>();
                    if (c != null && !c.Done && !c.IsMenu) { over = c; break; }
                }
                if (over != null && over != lastOver && marks.Count < Mathf.Min(max, Ammo)) AddMark(over);
                lastOver = over;
            }
            else lastOver = null;
            // 표시는 깡통을 따라다닌다 (깡통 위에 쌓아서). 깡통이 사라지면 표시도
            var count = new Dictionary<Can, int>();
            for (int i = 0; i < marks.Count; i++)
            {
                var c = marks[i]; var v = markViews[i];
                if (v == null) continue;
                if (c == null || c.Done) { v.gameObject.SetActive(false); continue; }
                count.TryGetValue(c, out int k); count[c] = k + 1;
                v.transform.position = c.transform.position + new Vector3(0f, c.Kind.Height * 0.5f + 0.22f + k * 0.2f, 0f);
                v.transform.rotation = Quaternion.Euler(0f, 0f, 45f + Mathf.Sin(Time.unscaledTime * 8f + i) * 6f);
            }
        }

        void AddMark(Can c)
        {
            marks.Add(c);
            var v = Look.Quad("DeadEyeMark", c.transform.position, new Vector2(0.16f, 0.16f), new Color(0.95f, 0.15f, 0.12f), 20);
            markViews.Add(v);
            Sfx.Play(Sfx.Click(), 0.55f, 1.8f + marks.Count * 0.12f);
        }

        IEnumerator Volley()
        {
            volleying = true;
            yield return new WaitForSecondsRealtime(0.06f);
            for (int i = 0; i < marks.Count; i++)
            {
                var c = marks[i];
                if (markViews[i] != null) Destroy(markViews[i].gameObject);
                if (c == null || c.Done) continue;
                if (Ammo <= 0 || Reloading) continue;
                Physics2D.SyncTransforms();
                Fire(c.transform.position);
                yield return new WaitForSecondsRealtime(0.07f);
            }
            foreach (var v in markViews) if (v != null) Destroy(v.gameObject);
            marks.Clear(); markViews.Clear();
            volleying = false;
        }

        void Fire(Vector2 aim)
        {
            if (Reloading || Cinematic) return;
            if (Ammo <= 0) { if (!Kind.Auto) Sfx.Play(Sfx.Click(), 0.7f, 1f); return; }
            bool wasFull = Ammo == Capacity;
            bool last = Ammo == 1;
            Ammo--; Shots++;
            kickLeft = 1f;
            shotValue = wasFull && firstShotPending ? FirstShotMult : 1f;
            shotBounce = last ? LastShotBounce : 1f;
            firstShotPending = false;
            if (shotValue > 1f) Game.I?.Hud.Float("첫 발 ×2", aim + Vector2.up * 0.5f, 4);
            Sfx.Play(Sfx.Bang(), Kind.Auto ? 0.5f : 0.9f, Kind.Pitch * Random.Range(0.95f, 1.08f));
            // 조준점 마름모 번쩍임은 뺐다 (사용자 10-13: "마름모꼴은 지워봐")
            view.Fire(aim, Kind.Kick);
            // 쏠 때마다 낮은 확률로 다이너마이트를 공중에 던진다 (사용자 10-14: 소환 깡통이 아니라 공격 효과)
            if (!Cinematic && Random.value < Can.DynamiteChance) Can.ThrowDynamite(view.Muzzle);
            // 판정: 원 안의 깡통. 리볼버는 가장 가까운 하나, 샷건은 전부. 쏘는 버튼도 여기서
            Can best = null; float bestD = float.MaxValue; bool any = false; Bartender keeper = null;
            foreach (var col in Physics2D.OverlapCircleAll(aim, Mathf.Max(Kind.AimRadius, 0.3f)))
            {
                var bt = col.GetComponent<Bartender>();
                if (bt != null) { keeper = bt; continue; }   // 주인은 깡통을 하나도 못 맞혔을 때만 (10-15)
                var btn = col.GetComponent<ShootButton>();
                if (btn != null) { btn.Shot(); any = true; continue; }
                var can = col.GetComponent<Can>();
                if (can == null || can.Done) continue;
                if (IsFinisher(can)) { StartCoroutine(Finisher(can)); any = true; continue; }   // 현상금 막타: 연출로 넘긴다
                if (Kind.HitAll) { HitOne(can, aim); any = true; continue; }
                float d = ((Vector2)can.transform.position - aim).sqrMagnitude;
                if (d < bestD) { bestD = d; best = can; }
            }
            if (best != null && IsDefileFinisher(best, out int predicted)) { StartCoroutine(Finisher(best, true, predicted)); any = true; best = null; }   // 모독 최고 기록 연출 (10-16)
            if (best != null) { HitOne(best, aim); any = true; }
            if (!any && keeper != null) keeper.Shot(aim);
            if (any) Hits++;   // 맞춤 마름모 표시는 뺐다 (사용자 10-13)
            Game.I?.Shot(any);
            if (Ammo == 0) StartCoroutine(AutoReload());
        }

        /// <summary>총 한 발의 공격력 (보통 1. 10-16 시험: 패거리 두목이 있는 동안 3)</summary>
        public static int ShotDamage = 1;

        void HitOne(Can can, Vector2 aim)
        {
            // 샷건은 깡통마다 맞은 자리가 다르다: 깡통 쪽으로 살짝 당긴 점
            Vector2 at = Kind.HitAll ? Vector2.Lerp(aim, can.transform.position, 0.7f) : aim;
            int earned = can.Hit(at, Kind.ValueScale * shotValue, Kind.BounceScale * shotBounce, ShotDamage);
            if (can.IsMenu) return;
            Game.I?.CanHit(can, earned, at);
            if (Kind.CurseChance > 0f && !can.Cursed && !can.Done && Random.value < Kind.CurseChance)
            {
                can.Curse();
                Game.I?.Hud.Float("저주!", at + Vector2.up * 0.5f, 6);
            }
        }

        /// <summary>다 쏘면 즉시 장전 (설정으로 끌 수 있다. PlayerPrefs "autoReload")</summary>
        public static bool AutoReloadOnEmpty
        {
            get => PlayerPrefs.GetInt("autoReload", 1) == 1;
            set { PlayerPrefs.SetInt("autoReload", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        IEnumerator AutoReload()
        {
            if (!AutoReloadOnEmpty) yield break;   // 꺼져 있으면 R을 눌러야 한다
            yield return null;                      // 마지막 발의 반동 한 프레임 뒤 바로
            if (Ammo == 0 && !Reloading) yield return Reload();
        }

        bool missed, justDone;

        public IEnumerator Reload()
        {
            if (Reloading) yield break;
            int token = ++reloadToken;
            Reloading = true; attempted = false; missed = false; justDone = false;
            ReloadProgress = 0f;
            windowStart = WindowStart; windowEnd = WindowEnd;
            float seconds = ActiveReloadSeconds;
            Sfx.Play(Sfx.Reload(), 0.8f, 1f);
            view.Reload(seconds, Capacity - Ammo);
            float t = 0f;
            while (t < seconds && !justDone && token == reloadToken)
            {
                t += Time.deltaTime;
                if (missed) t += Time.deltaTime * 0.35f;   // 실패: 이번 장전은 느려진다
                ReloadProgress = Mathf.Clamp01(t / seconds);
                yield return null;
            }
            if (token != reloadToken) { view.ReloadSnap(); yield break; }   // 교대로 끊김
            Ammo = Capacity;
            Reloading = false;
            firstShotPending = true;
        }

        /// <summary>저스트 장전 시도: 슬래시 구간 안이면 즉시 끝, 아니면 실패</summary>
        public void ActiveReload()
        {
            if (ReloadProgress < IgnoreBefore) return;   // 앞쪽 30%: 아무 일도 없다 (시도 기회도 그대로 남는다)
            attempted = true;
            if (ReloadProgress >= windowStart && ReloadProgress <= windowEnd)
            {
                ReloadStreak++;
                justDone = true;
                Ammo = Capacity; firstShotPending = true;
                Sfx.Play(Sfx.Tang(), 0.6f, 1.8f + Mathf.Min(ReloadStreak, 8) * 0.1f);
                view.ReloadSnap();
                Game.I?.Hud.Float($"저스트!  ×{ReloadStreak}", (Vector2)crosshair.position + Vector2.up * 0.9f, 3 + ReloadStreak);
            }
            else
            {
                ReloadStreak = 0;
                missed = true;
                Sfx.Play(Sfx.Click(), 0.8f, 0.6f);
            }
        }
    }
}
