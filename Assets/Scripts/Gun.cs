using System.Collections;
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
        const float WindowStart = 0.62f, WindowEnd = 0.74f;

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
            Cinematic = false; Time.timeScale = 1f; Time.fixedDeltaTime = 0.02f;   // 연출 도중 씬이 바뀌었어도 원래대로
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
            bar = new GameObject("ReloadBar").transform; bar.SetParent(crosshair, false); bar.localPosition = new Vector2(0f, 0.5f);
            barBg = Look.Quad("Bg", Vector2.zero, new Vector2(1.3f, 0.12f), new Color(0f, 0f, 0f, 0.55f), 51, bar);
            barWindow = Look.Quad("Window", Vector2.zero, new Vector2(0.2f, 0.16f), new Color(1f, 0.9f, 0.3f, 0.9f), 52, bar);
            barFill = Look.Quad("Fill", Vector2.zero, new Vector2(0.02f, 0.08f), new Color(1f, 1f, 1f, 0.95f), 53, bar);
            bar.gameObject.SetActive(false);
            Equip(GunKind.Revolver);
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
                    if (want && Time.time >= nextFire) { Fire(aim); nextFire = Time.time + Kind.FireInterval; }
                }
            }
            // 장전 칸 그리기
            bar.gameObject.SetActive(Reloading);
            if (Reloading)
            {
                float w = 1.3f;
                barFill.transform.localPosition = new Vector2(-w * 0.5f + ReloadProgress * w, 0f);
                barWindow.transform.localPosition = new Vector2(-w * 0.5f + (windowStart + windowEnd) * 0.5f * w, 0f);
                barWindow.transform.localScale = new Vector3((windowEnd - windowStart) * w, 0.16f, 1f);
                barWindow.color = attempted ? (missed ? new Color(1f, 0.3f, 0.2f, 0.9f) : new Color(0.5f, 1f, 0.6f, 0.9f)) : new Color(1f, 0.9f, 0.3f, 0.9f);
            }

            // 쏠 때 벌어짐: 예전엔 전체를 키웠다(도트가 찌그러짐) → 팔만 도트 단위로 밀어낸다 (최대 약 8도트)
            PlaceArms(Mathf.RoundToInt(kickLeft * 8f * Mathf.Min(1f, Kind.Kick)));
            if (kickLeft > 0f)
            {
                kickLeft = Mathf.Max(0f, kickLeft - Time.unscaledDeltaTime * 5f);
                cam.transform.position = camHome + (Vector3)Random.insideUnitCircle * kickLeft * 0.12f * Kind.Kick;
                if (kickLeft <= 0f) cam.transform.position = camHome;
            }
        }

        // ---------- 현상금 막타 연출 (사용자 10-13) ----------
        // 평소 총알은 즉시 맞는다(히트스캔). 현상금 깡통의 마지막 한 발만: 시간이 크게 느려지고 레터박스가 올라오고,
        // 눈에 보이는 총알이 총구에서 깡통까지 날아가 닿는 순간 터진다 → 잠깐 멈춘 듯 머물다 → 시간이 천천히 풀린다.
        /// <summary>연출 중 (이때는 쏘지 않는다)</summary>
        public static bool Cinematic { get; private set; }
        bool IsFinisher(Can can) => !Cinematic && can.Kind.IsBounty && can.HitsLeft <= 1 && !can.Done && !can.IsMenu;

        IEnumerator Finisher(Can can)
        {
            Cinematic = true;
            Letterbox.Force = true;
            float baseFixed = 0.02f;
            void SetTime(float s) { Time.timeScale = s; Time.fixedDeltaTime = baseFixed * Mathf.Max(0.02f, s); }
            try
            {
                // 1) 시간이 0.12초 만에 0.12배로 (OutCubic: 확 느려졌다 가라앉음)
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.12f) { SetTime(Mathf.Lerp(1f, 0.12f, Ease.OutCubic(t))); yield return null; }
                SetTime(0.12f);
                Sfx.Play(Sfx.Whoosh(), 0.7f, 0.5f);
                // 2) 눈에 보이는 총알: 총구 → 깡통 (0.5초, 깡통이 느리게 움직여도 따라간다). 지나간 자리에 도트 꼬리
                var art = GunView.LoadArt(Kind.Id == "shotgun" ? "bullet_shotgun" : "bullet_revolver");
                var b = new GameObject("FinisherBullet").AddComponent<SpriteRenderer>(); b.sortingOrder = 46;
                if (art != null) b.sprite = art; else { b.sprite = Look.Square(); b.transform.localScale = new Vector3(0.3f, 0.1f, 1f); b.color = new Color(1f, 0.85f, 0.4f); }
                Vector2 from = view.Muzzle;
                float trailClock = 0f;
                for (float t = 0f; t < 1f && can != null && !can.Done; t += Time.unscaledDeltaTime / 0.5f)
                {
                    Vector2 to = can.transform.position;
                    float k = Ease.InOutCubic(t) * 0.85f + t * 0.15f;   // 출발·도착이 살짝 느리고 가운데가 빠르다 (완전히 멈추진 않게 직선 15% 섞음)
                    Vector2 p = Vector2.Lerp(from, to, k);
                    var dir = to - from;
                    b.transform.position = new Vector3(Mathf.Round(p.x * 27f) / 27f, Mathf.Round(p.y * 27f) / 27f, 0f);
                    b.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                    trailClock += Time.unscaledDeltaTime;
                    if (trailClock > 0.03f) { trailClock = 0f; StartCoroutine(TrailDot(p)); }
                    yield return null;
                }
                Destroy(b.gameObject);
                if (can == null || can.Done) yield break;
                // 3) 닿는 순간: 거의 멈춤(0.03배)에서 터진다 → 0.35초 머문다
                SetTime(0.03f);
                Vector2 at = can.transform.position;
                HitOne(can, at);
                Fx.HitMark(at);
                Fx.Shake(1f);
                yield return new WaitForSecondsRealtime(0.35f);
                // 4) 천천히 풀린다 (1초, InQuad: 처음엔 거의 그대로 느리다가 점점 원래 속도로). 절반쯤에서 레터박스가 빠지기 시작
                for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 1f)
                {
                    SetTime(Mathf.Lerp(0.03f, 1f, Ease.InQuad(t)));
                    if (t > 0.5f) Letterbox.Force = false;
                    yield return null;
                }
            }
            finally
            {
                SetTime(1f);
                Letterbox.Force = false;
                Cinematic = false;
            }
        }

        IEnumerator TrailDot(Vector2 at)
        {
            var d = Look.Quad("Trail", new Vector2(Mathf.Round(at.x * 27f) / 27f, Mathf.Round(at.y * 27f) / 27f), Vector2.one * (2f / 27f), new Color(1f, 0.95f, 0.75f, 0.9f), 45);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f) { d.color = new Color(1f, 0.95f, 0.75f, 0.9f * (1f - t)); yield return null; }
            Destroy(d.gameObject);
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
            StartCoroutine(MuzzleFlash(aim));
            view.Fire(aim, Kind.Kick);
            // 판정: 원 안의 깡통. 리볼버는 가장 가까운 하나, 샷건은 전부. 쏘는 버튼도 여기서
            Can best = null; float bestD = float.MaxValue; bool any = false;
            foreach (var col in Physics2D.OverlapCircleAll(aim, Mathf.Max(Kind.AimRadius, 0.3f)))
            {
                var btn = col.GetComponent<ShootButton>();
                if (btn != null) { btn.Shot(); any = true; continue; }
                var can = col.GetComponent<Can>();
                if (can == null || can.Done) continue;
                if (IsFinisher(can)) { StartCoroutine(Finisher(can)); any = true; continue; }   // 현상금 막타: 연출로 넘긴다
                if (Kind.HitAll) { HitOne(can, aim); any = true; continue; }
                float d = ((Vector2)can.transform.position - aim).sqrMagnitude;
                if (d < bestD) { bestD = d; best = can; }
            }
            if (best != null) { HitOne(best, aim); any = true; }
            if (any) { Hits++; Fx.HitMark(aim); }
            Game.I?.Shot(any);
            if (Ammo == 0) StartCoroutine(AutoReload());
        }

        void HitOne(Can can, Vector2 aim)
        {
            // 샷건은 깡통마다 맞은 자리가 다르다: 깡통 쪽으로 살짝 당긴 점
            Vector2 at = Kind.HitAll ? Vector2.Lerp(aim, can.transform.position, 0.7f) : aim;
            int earned = can.Hit(at, Kind.ValueScale * shotValue, Kind.BounceScale * shotBounce);
            if (can.IsMenu) return;
            Game.I?.CanHit(can, earned, at);
            if (Kind.CurseChance > 0f && !can.Cursed && !can.Done && Random.value < Kind.CurseChance)
            {
                can.Curse();
                Game.I?.Hud.Float("저주!", at + Vector2.up * 0.5f, 6);
            }
        }

        IEnumerator MuzzleFlash(Vector2 at)
        {
            var f = Look.Quad("Flash", at, Vector2.one * (0.5f + Kind.AimRadius * 0.5f), new Color(1f, 0.95f, 0.6f, 0.9f), 49);
            f.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            yield return null; yield return null;
            Destroy(f.gameObject);
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
