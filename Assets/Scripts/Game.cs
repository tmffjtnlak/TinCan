using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TinCan
{
    public enum GameState { Title, Settings, Play }

    /// <summary>
    /// 핵심 루프 (사용자 10-12 세트 구조): 선반에 깡통을 놓는다(준비) → 첫 발로 띄우면 소환이 잠기고 세트 시작 →
    /// 맞출 때마다 금액 확정 → 떨어지거나 터지면 탄피가 바닥에 → 공중이 비면 세트 끝, 탄피가 돈 판자로 날아가 돈이 된다.
    /// 콘텐츠 세 축: 총기류(쏘는 방식) · 깡통류(뭘 띄우나) · 스킬트리(얼마나 잘). Tab으로 연다. Q/W/E로 탭.
    /// 타이틀 = 같은 사격장. 선반 위 "시작"/"이어서"/"설정" 깡통을 쏘면 떨어지고, 떨어지면 원형으로 닫혔다 열리며 시작.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }
        /// <summary>씬을 다시 열 때: 타이틀을 건너뛰고 바로 플레이 (true면 저장 불러오기)</summary>
        static bool pendingPlay, pendingLoad;

        public GameState State { get; private set; } = GameState.Title;
        public long Money { get; private set; }
        public int BestCombo { get; private set; }
        public long BestPayout { get; private set; }
        public int Bursts { get; private set; }
        public int MaxCans { get; set; } = 3;
        public float ValueMult { get; set; } = 1f;
        public float ComboMult { get; set; } = 1f;
        public float PayoutMult { get; set; } = 1f;
        public float SpawnDiscount { get; set; } = 0f;
        public bool ClusterSpawn { get; set; } = false;
        public int SpawnCount { get; set; } = 1;
        public float BurstMult { get; set; } = 2f;
        public int LandBounces { get; set; } = 0;        // 스킬 "다시 튐": 떨어진 깡통이 혼자 N번 다시 튀어오른다
        public float FloorSlow { get; set; } = 0f;       // 스킬 "바닥 근처 느려짐": 바닥 가까이에서 낙하 속도가 이만큼 줄어든다 (0.5 = 반)

        // ---------- 슬로우 (우클릭) ----------
        public float SlowGauge { get; private set; }          // 0..1
        public float SlowGaugeMax { get; set; } = 1f;         // 스킬: 게이지 크기 (초 단위로 생각: 1 = 2초)
        public float SlowStrength { get; set; } = 0.3f;       // 스킬: 깡통 시간 배수 (작을수록 느림)
        public float SlowFillPerHit { get; set; } = 0.1f;     // 스킬: 맞출 때 차는 양
        public bool SlowActive { get; private set; }
        const float SlowDrainPerSec = 0.5f;

        void UpdateSlow(Keyboard kb)
        {
            var mouse = Mouse.current;
            bool want = mouse != null && mouse.rightButton.isPressed && SlowGauge > 0.02f;
            if (want)
            {
                SlowGauge = Mathf.Max(0f, SlowGauge - SlowDrainPerSec * Time.deltaTime);
                if (!SlowActive) { SlowActive = true; Sfx.Play(Sfx.Whoosh(), 0.5f, 0.6f); }
            }
            else if (SlowActive) { SlowActive = false; Sfx.Play(Sfx.Whoosh(), 0.4f, 1.4f); }
            Can.TimeScale = SlowActive ? SlowStrength : 1f;
        }

        public void FillSlow() => SlowGauge = Mathf.Min(SlowGaugeMax, SlowGauge + SlowFillPerHit);
        public Gun Gun { get; private set; }
        public List<Can> Cans { get; } = new List<Can>();
        public List<SkillNode> Nodes { get; private set; }
        public HashSet<string> OwnedGuns { get; } = new HashSet<string> { "revolver" };
        public HashSet<string> OwnedCans { get; } = new HashSet<string> { "tin" };
        public bool MenuOpen { get; private set; }
        /// <summary>디버그 (F1): 돈 무한</summary>
        public bool DebugMoney { get; private set; }
        public CanKind SpawnKind { get; set; } = CanKind.Tin;
        Hud hud;
        public Hud Hud => hud;
        TreeView tree;
        Armory armory;
        Booklet booklet;
        float saveTimer;
        float lastSpawnX = float.NaN;
        int lastPage = 2;

        void Awake() { I = this; Can.BounceBonus = 0f; Can.TimeScale = 1f; }

        void Start()
        {
            Look.BuildRange();
            Gun = new GameObject("Gun").AddComponent<Gun>();
            Gun.Build();
            Nodes = SkillTree.Make();
            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.Bind(this);
            new GameObject("AmmoHud").AddComponent<AmmoHud>().Build(hud.Canvas, Gun);
            Letterbox.Make(hud.Canvas);   // 우클릭 연출: 위아래 띠 + UI 흐리게 (사용자 10-13)
            tree = new GameObject("Tree").AddComponent<TreeView>();
            tree.Build(this, Nodes, hud.Canvas);
            tree.SetOpen(false);
            armory = new GameObject("Armory").AddComponent<Armory>();
            armory.Build(this, hud.Canvas);
            booklet = new GameObject("Booklet").AddComponent<Booklet>();
            booklet.Build(this, hud.Canvas);
            new GameObject("Markers").AddComponent<OffscreenMarkers>();
            bounty = Bounty.Make(hud.Canvas);
            viewer = GunViewer.Make(hud.Canvas);
            AudioListener.volume = PlayerPrefs.GetFloat("volume", 1f);

            if (pendingPlay) { bool load = pendingLoad; pendingPlay = pendingLoad = false; BeginPlay(load); }
            else EnterTitle();
        }

        // ================= 타이틀 =================
        Text signTitle, tagline;
        bool entering;   // 시작을 눌러 술집으로 들어가는 중

        /// <summary>시작 화면 (사용자 10-13): 술집 그림 3띠 시차 + 입구 문 + 글자 메뉴. 플레이로 가면 없앤다.
        /// 깡통·총으로 쏘는 메뉴는 폐기 (사용자 10-13 "총 쏘는 방식은 안 쓰는 걸로") — 설정도 같은 글자 메뉴</summary>
        Title title;

        void EnterTitle()
        {
            State = GameState.Title;
            Money = 0;
            hud.SetPlayVisible(false);
            if (title == null) title = Title.Make(hud.Canvas);
            title.ClearButtons();
            // 로고 자리 = 오른쪽 위 (Shelldiver 분석 10-13). 「깡통」 글자는 가제, 손글씨 워드마크 그림이 오면 교체. 한 문장은 그 아래 작게
            signTitle = Title.Label("SignTitle", hud.Canvas, new Vector2(300f, 400f), new Vector2(900f, 200f), 144, TextAnchor.MiddleCenter);
            signTitle.text = "깡통";
            tagline = Title.Label("Tagline", hud.Canvas, new Vector2(300f, 290f), new Vector2(900f, 50f), 24, TextAnchor.MiddleCenter);
            tagline.text = "쏴서 띄우고, 떨어지기 전에 또 쏴라.";
            BuildMainMenu();
        }

        /// <summary>메뉴 = 왼쪽 글자 세로. 상자 없음, 위에서 아래로 하나씩 뜬다</summary>
        void BuildMainMenu()
        {
            title.ClearButtons();
            title.AddButton("시작", () => StartCoroutine(GoPlay(load: false)));
            if (Save.Exists) title.AddButton("이어서", () => StartCoroutine(GoPlay(load: true)));
            title.AddButton("설정", OpenSettings);
            title.ShowButtons();
        }

        /// <summary>메뉴 바꾸기: 지금 항목이 차례로 빠지고 → 새 항목이 차례로 들어온다</summary>
        IEnumerator SwapMenu(System.Action build)
        {
            title.SetButtonsVisible(false);
            yield return new WaitForSecondsRealtime(0.22f);
            build();
        }

        IEnumerator GoPlay(bool load)
        {
            if (State != GameState.Title) yield break;
            State = GameState.Settings; // 메뉴 입력 막기
            entering = true;
            Gun.Enabled = false;
            // 글자는 원형 페이드 위에 그려지니까 먼저 치운다
            if (signTitle != null) Destroy(signTitle.gameObject);
            if (tagline != null) Destroy(tagline.gameObject);
            // 문으로 줌인 → 문 열림 → 원형 페이드 닫힘 (Title.Enter)
            bool done = false;
            yield return title.Enter(() => done = true);
            while (!done) yield return null;
            if (!load) Save.Delete();
            pendingPlay = true; pendingLoad = load;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void OpenSettings()
        {
            if (State != GameState.Title) return;
            State = GameState.Settings;
            StartCoroutine(SwapMenu(BuildSettingsMenu));
        }

        /// <summary>설정 = 같은 글자 메뉴 (사용자 10-13). 값 항목은 글자 왼쪽 반을 누르면 줄고 오른쪽 반이면 늘어난다 (우클릭 = 줄임). 켜고 끄는 항목은 누르면 바뀐다</summary>
        void BuildSettingsMenu()
        {
            title.ClearButtons();
            title.AddHeader("설정");
            title.AddOption(() => $"소리  < {Mathf.RoundToInt(AudioListener.volume * 100)}% >", d => SetVolume(AudioListener.volume + 0.1f * d));
            title.AddOption(() => Screen.fullScreen ? "화면  전체 화면" : "화면  창 모드", d => Screen.fullScreen = !Screen.fullScreen);
            title.AddOption(() => Gun.AutoReloadOnEmpty ? "다 쏘면 자동 장전  켜짐" : "다 쏘면 자동 장전  꺼짐", d => Gun.AutoReloadOnEmpty = !Gun.AutoReloadOnEmpty);
            title.AddButton("돌아가기", CloseSettings);
            title.ShowButtons();
        }

        void SetVolume(float v) { AudioListener.volume = Mathf.Clamp01(Mathf.Round(v * 10f) / 10f); PlayerPrefs.SetFloat("volume", AudioListener.volume); PlayerPrefs.Save(); }

        void CloseSettings()
        {
            if (State != GameState.Settings || entering) return;
            StartCoroutine(SwapMenu(() => { State = GameState.Title; BuildMainMenu(); }));
        }

        // ================= 플레이 =================
        void BeginPlay(bool load)
        {
            State = GameState.Play;
            if (title != null) { Destroy(title.gameObject); title = null; }
            Money = 10;
            hud.SetPlayVisible(true);
            if (load) Load();
            Iris.Get().StartClosed();
            StartCoroutine(Iris.Get().Open(0.2f));   // Shelldiver 분석: 열림도 짧게
        }

        void Load()
        {
            var d = Save.Read(); if (d == null) return;
            Money = d.money; Bursts = d.bursts; BestCombo = d.bestCombo; BestPayout = d.bestPayout;
            OwnedGuns.Clear(); OwnedGuns.UnionWith(d.ownedGuns); OwnedGuns.Add("revolver");
            OwnedCans.Clear(); OwnedCans.UnionWith(d.ownedCans); OwnedCans.Add("tin");
            for (int i = 0; i < d.nodeIds.Count && i < d.nodeLevels.Count; i++)
            {
                var n = Nodes.Find(x => x.Id == d.nodeIds[i]);
                if (n == null) continue;
                n.Level = d.nodeLevels[i];
                if (n.Level > 0 && !n.Root) n.Apply(this, n.Level);   // 시작 노드는 해방 여부만 기억
            }
            var g = GunKind.ForSale.Find(x => x.Id == d.gun); if (g != null) { Primary = g; Gun.Equip(g); }
            var s2 = GunKind.ForSale.Find(x => x.Id == d.secondary); if (s2 != null && s2 != Primary) Secondary = s2;
            ActiveSlot = 0;
            var k = CanKind.ForSale.Find(x => x.Id == d.spawn); if (k != null) SpawnKind = k;
        }

        void Update()
        {
            Cans.RemoveAll(c => c == null);
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f3Key.wasPressedThisFrame) viewer.Toggle();   // 시험용 총 뷰어
            if (viewer.Open) return;
            if (State != GameState.Play)
            {
                Gun.Enabled = false;   // 시작 화면·설정은 마우스 글자 메뉴. 총은 플레이에서만
                if (State == GameState.Settings && kb.escapeKey.wasPressedThisFrame) CloseSettings();
                return;
            }
            // 가게는 오른쪽 아래 버튼 둘(스킬트리·총 상점)로 연다 (사용자 10-12). Tab/Esc = 닫기 (Tab은 마지막 페이지 다시 열기도)
            if (kb.tabKey.wasPressedThisFrame) { if (MenuOpen) CloseMenu(); else OpenMenu(lastPage); }
            if (MenuOpen)
            {
                if (DebugMoney) Money = 999_999_999;
                if (kb.escapeKey.wasPressedThisFrame) CloseMenu();
                return;
            }
            hud.Buttons(dtUnscaled: Time.unscaledDeltaTime, onTree: () => OpenMenu(2), onShop: () => OpenMenu(0));
            if (kb.spaceKey.wasPressedThisFrame) TrySpawn();
            if (SetActive && !ending && Airborne == 0) StartCoroutine(EndSet());
            if (kb.f1Key.wasPressedThisFrame) { DebugMoney = !DebugMoney; hud.Float(DebugMoney ? "디버그: 돈 무한" : "디버그 끔", new Vector2(0f, 2f), 6); }
            if (DebugMoney) Money = 999_999_999;
            if (kb.f2Key.wasPressedThisFrame) bounty.Summon();
            if (kb.digit1Key.wasPressedThisFrame) SelectSlot(0);
            if (kb.digit2Key.wasPressedThisFrame) SelectSlot(1);
            if (kb.digit3Key.wasPressedThisFrame) hud.Float("3: 투척류 — 아직 없다", new Vector2(0f, 1.5f), 0);
            HolsterReload();
            Nudge();
            bounty.Tick();
            UpdateSlow(kb);
            AutoGun();
            saveTimer += Time.deltaTime;
            if (saveTimer > 5f) { saveTimer = 0f; Save.Write(this); }
        }

        void OnApplicationQuit() { if (State == GameState.Play) Save.Write(this); }

        public void OpenMenu(int page) { MenuOpen = true; Gun.Enabled = false; hud.SetButtonsVisible(false); OpenPage(page); }
        public void CloseMenu() { MenuOpen = false; Gun.Enabled = true; tree.SetOpen(false); armory.Hide(); booklet.Hide(); hud.SetButtonsVisible(true); Save.Write(this); }

        void OpenPage(int p)
        {
            lastPage = p;
            tree.SetOpen(p == 2);
            // 0 = 총기 책자 (사용자 10-12 종이 책자 가게), 1 = 깡통류(옛 카드, 소환비 손보기 전까지), 2 = 트리. QWE 탭은 뺐다
            if (p == 0) { armory.Hide(); booklet.Show(); }
            else { booklet.Hide(); if (p == 2) armory.Hide(); else armory.Show(Armory.Page.Cans); }
        }

        // ---------- 총기류: 주무기·보조무기 (사용자 10-11) ----------
        public GunKind Secondary { get; private set; }
        public float HolsterReloadSeconds { get; set; } = 0f;   // 스킬 "허리춤 장전": 꺼내지 않은 총이 N초마다 한 발
        float holsterTimer;

        /// <summary>가게에서 번호를 누르면: 안 샀으면 사서 보조로, 보조를 누르면 주무기와 교대, 다른 보유 총이면 보조로</summary>
        public void PickGun(GunKind k)
        {
            if (!OwnedGuns.Contains(k.Id))
            {
                if (Money < k.Price) { Sfx.Play(Sfx.Click(), 0.5f, 0.8f); return; }
                Money -= k.Price; OwnedGuns.Add(k.Id);
                Secondary = k;
                Sfx.Play(Sfx.Reload(), 0.8f, 1.2f);
                return;
            }
            if (k == Primary) return;
            if (k == Secondary) { SwapGuns(); Sfx.Play(Sfx.Reload(), 0.6f, 1.4f); return; }
            Secondary = k;
            if (ActiveSlot == 1) Gun.Equip(k);
            Sfx.Play(Sfx.Reload(), 0.6f, 1.4f);
        }

        /// <summary>슬롯 두 개: [0] 주무장, [1] 보조무기. 숫자키로 고른다 (FPS 도구 느낌)</summary>
        public GunKind Primary { get; private set; } = GunKind.Revolver;
        public int ActiveSlot { get; private set; } = 0;

        public void SelectSlot(int slot)
        {
            var want = slot == 0 ? Primary : Secondary;
            if (want == null) { Sfx.Play(Sfx.Click(), 0.4f, 0.9f); hud.Float("보조무기가 없다", new Vector2(0f, 1.5f), 0); return; }
            if (slot == ActiveSlot) return;
            ActiveSlot = slot;
            Gun.Equip(want);
            Gun.View.Swap();
            Sfx.Play(Sfx.Reload(), 0.7f, 1.6f);
        }

        /// <summary>(가게에서 보조를 누르면) 주무장과 보조무기 자리를 바꾼다</summary>
        public void SwapGuns()
        {
            if (Secondary == null) return;
            var p = Primary; Primary = Secondary; Secondary = p;
            Gun.Equip(ActiveSlot == 0 ? Primary : Secondary);
        }

        void HolsterReload()
        {
            if (HolsterReloadSeconds <= 0f || Secondary == null) return;
            holsterTimer += Time.deltaTime;
            if (holsterTimer < HolsterReloadSeconds) return;
            holsterTimer = 0f;
            Gun.HolsterFeed(Secondary);
        }

        // ---------- 깡통류 ----------
        public long SpawnCost(CanKind k) => Mathf.Max(1, Mathf.RoundToInt(k.Cost * (1f - SpawnDiscount)));

        public void PickCan(CanKind k)
        {
            if (!OwnedCans.Contains(k.Id))
            {
                if (Money < k.Price) { Sfx.Play(Sfx.Click(), 0.5f, 0.8f); return; }
                Money -= k.Price; OwnedCans.Add(k.Id);
            }
            SpawnKind = k;
            Sfx.Play(Sfx.Tang(), 0.5f, k.Pitch);
        }

        AutoGunner gunner;
        Bounty bounty;
        GunViewer viewer;
        /// <summary>자동 사격 (10-12 2판): 스킬로 총 종류를 뚫으면 화면 아래에 총이 서서 쏜다. 많아질수록 난투</summary>
        public void AddAutoGun(GunKind kind)
        {
            if (gunner == null) gunner = AutoGunner.Make();
            gunner.Add(kind);
        }
        public float AutoRate { get; set; } = 1f;   // 스킬 "빠른 사격기": 간격 배수
        void AutoGun()
        {
            if (gunner == null) return;
            gunner.RateScale = AutoRate;
            gunner.Tick(Time.deltaTime);
        }

        public int CansOnShelf => Cans.FindAll(c => c != null && !c.Airborne).Count;
        public int Airborne { get { int n = 0; foreach (var c in Cans) if (c != null && c.Airborne && !c.Done) n++; return n; } }

        /// <summary>돈을 내고 선반에 깡통. 짝 소환이면 여러 개, 붙여 놓기면 마지막 깡통 옆에. 공중에 깡통이 있으면 잠긴다 (세트 중)</summary>
        public bool TrySpawn()
        {
            long cost = SpawnCost(SpawnKind);
            if (Airborne > 0) { Sfx.Play(Sfx.Click(), 0.5f, 0.6f); hud.Float("공중 깡통을 다 처리하면 소환", new Vector2(0f, Look.ShelfY + 0.9f), 0); return false; }
            if (CansOnShelf >= MaxCans) { Sfx.Play(Sfx.Click(), 0.5f, 0.7f); hud.Float("선반이 꽉 찼다", new Vector2(0f, Look.ShelfY + 0.9f), 0); return false; }
            if (Money < cost) { Sfx.Play(Sfx.Click(), 0.5f, 0.7f); hud.Float("돈이 모자라다", new Vector2(0f, Look.ShelfY + 0.9f), 0); return false; }
            int n = 0;
            for (int i = 0; i < SpawnCount; i++)
            {
                if (CansOnShelf >= MaxCans || Money < cost) break;
                Money -= cost;
                float x = FreeX();
                Cans.Add(Can.Spawn(SpawnKind, new Vector2(x, Look.ShelfY + 0.11f + SpawnKind.Height * 0.5f)));
                lastSpawnX = x; n++;
            }
            Sfx.Play(Sfx.Reload(), 0.4f, 2.2f);
            return n > 0;
        }

        float FreeX()
        {
            float gap = SpawnKind.Width + 0.25f;
            if (ClusterSpawn)
            {
                float anchor = float.NaN;
                foreach (var c in Cans) if (c != null && !c.Airborne) { anchor = c.transform.position.x; }
                if (float.IsNaN(anchor) && !float.IsNaN(lastSpawnX)) anchor = lastSpawnX;
                if (!float.IsNaN(anchor))
                    for (int k = 1; k <= 4; k++)
                        foreach (float s in new[] { 1f, -1f })
                        {
                            float x = anchor + s * gap * k;
                            if (x < Look.ShelfLeft + 0.5f || x > Look.ShelfRight - 0.5f) continue;
                            if (Clear(x, gap * 0.9f)) return x;
                        }
            }
            for (int tries = 0; tries < 20; tries++)
            {
                float x = Random.Range(Look.ShelfLeft + 0.8f, Look.ShelfRight - 0.8f);
                if (Clear(x, 0.9f)) return x;
            }
            return Random.Range(Look.ShelfLeft + 0.8f, Look.ShelfRight - 0.8f);
        }

        bool Clear(float x, float minGap)
        {
            foreach (var c in Cans) if (c != null && !c.Airborne && Mathf.Abs(c.transform.position.x - x) < minGap) return false;
            return true;
        }

        /// <summary>동시 공중 배수 (사용자 10-11: 둘을 띄우게 유도): 1개 ×1, 2개 ×3, 3개 ×6, 4개 ×10 — n(n+1)/2</summary>
        public static int AirMult(int n) => Mathf.Max(1, n * (n + 1) / 2);
        public int MostAirborne { get; private set; }

        // ---------- 세트 배율 (사용자 10-12: 세트가 끝날 때 낚시/오버워치 점수판처럼 딱딱딱 보여 준다) ----------
        // 세트 동안 기본 돈만 쌓이고, 끝에 한꺼번에 곱한다. 저글링 = 세트 중 최대 동시 공중 n(n+1)/2 ·
        // 퍼펙트샷 = 세트 내내 빗나감 없음 ×1.2 · 저스트리로드 = 세트 중 최대 연속마다 +0.2 (최대 ×2)
        public const float PerfectMult = 1.2f, JustPerStreak = 0.2f, JustMax = 2f;
        bool missed;
        public int SetMaxAir { get; private set; }
        public int SetMaxStreak { get; private set; }
        public bool Perfect => SetActive && !missed;
        public float JustMultOf(int streak) => Mathf.Min(JustMax, 1f + JustPerStreak * streak);
        /// <summary>총이 한 발 쐈다: 빗나가면 이번 세트 퍼펙트가 깨진다</summary>
        public void Shot(bool hit)
        {
            if (hit || !SetActive || missed) return;
            missed = true;
            hud.Float("빗나감", (Vector2)Gun.transform.position + Vector2.up * 1.6f, 0);
        }

        /// <summary>한 발에 쌓이는 기본 돈: 값 × 값배수 × (연속 × 연속배수) × 총 배수. 배율은 세트 끝에</summary>
        public int EarnFor(CanKind kind, int combo, float scale) => Mathf.Max(1, Mathf.RoundToInt(kind.Value * ValueMult * combo * ComboMult * scale));

        float nudgeTimer; int nudges;
        /// <summary>하나만 떠 있고 선반에 깡통이 있으면 "하나 더 띄우면 ×3" (처음 다섯 번, 10초에 한 번)</summary>
        void Nudge()
        {
            if (nudges >= 5 || MostAirborne >= 2) return;
            nudgeTimer -= Time.deltaTime;
            if (nudgeTimer > 0f) return;
            if (Airborne != 1 || CansOnShelf == 0) return;
            foreach (var c in Cans) if (c != null && !c.Airborne) { hud.Float("하나 더 띄우면 ×3", (Vector2)c.transform.position + Vector2.up * 0.9f, 5); break; }
            nudgeTimer = 10f; nudges++;
        }

        // ---------- 세트 (사용자 10-12) ----------
        // 첫 발로 띄우면 시작, 공중이 비면 끝. 세트 안에서 번 돈은 바닥 탄피로 쌓여 있다가 끝나면 판자로 날아간다.
        public bool SetActive { get; private set; }
        public long SetPending { get; private set; }     // 이번 세트에서 확정됐지만 아직 안 받은 돈
        bool ending;

        /// <summary>연출 크기 0..1 (사용자 10-12: 점수가 높으면 딱딱딱·후두둑을 길게, 낮으면 빠르게). $10이면 0, $1,000이면 1. 최고 기록이면 최소 0.7</summary>
        float Ceremony(long total, bool best)
        {
            float k = Mathf.Clamp01((Mathf.Log10(Mathf.Max(1, total)) - 1f) / 2f);
            return best ? Mathf.Max(k, 0.7f) : k;
        }

        IEnumerator EndSet()
        {
            ending = true;
            yield return new WaitForSeconds(0.15f);
            if (Airborne > 0) { ending = false; yield break; }   // 그새 또 띄웠으면 세트가 이어진다
            long baseSum = SetPending;
            int airMult = AirMult(SetMaxAir);
            float perfect = missed ? 1f : PerfectMult, just = JustMultOf(SetMaxStreak);
            long total = (long)Mathf.Round(baseSum * airMult * perfect * just);
            SetPending = 0; SetActive = false; ending = false;
            if (total <= 0) yield break;
            bool best = total > BestPayout;
            if (best) BestPayout = total;
            float k = Ceremony(total, best);
            // 점수판 (돈 판자 아래): 기본 → 켜진 배율들 → 합계. 작으면 빠르게 몇 줄, 크면 천천히 전부
            var lines = new List<(string, bool)>
            {
                ($"기본  ${baseSum:N0}", true),
                ($"저글링  ×{airMult}", airMult > 1),
                ($"퍼펙트샷  ×{perfect:0.#}", perfect > 1f),
                ($"저스트리로드  ×{just:0.#}", just > 1f),
            };
            if (k < 0.5f) lines.RemoveAll(l => !l.Item2);   // 작은 세트는 ×1 줄을 안 찍는다
            if (k < 0.2f) lines.Clear();                      // 아주 작으면 합계 한 줄만
            Vector2 totalAt = Vector2.zero;
            yield return hud.Tally(lines, best ? $"${total:N0}  최고!" : $"${total:N0}", k, at => totalAt = at);
            // 합계 글자에서 탄피가 주르르륵 쏟아진다. 클수록 많이, 천천히. 바닥에 눕는 것부터 판자로 올라간다
            // 개수는 금액에 비례 (사용자 10-12: $50과 $1,000이 비슷하면 안 된다). $8에 하나, 최대 240. 쏟는 시간은 연출 크기로
            int count = Mathf.Clamp(Mathf.RoundToInt(total / 8f), 3, 240);
            float interval = Mathf.Lerp(0.3f, 1.6f, k) / count;
            long per = total / count, rem = total - per * count;
            var target = hud.MoneyWorld();
            for (int i = 0; i < count; i++)
            {
                var c = Casing.Drop(totalAt + Vector2.right * Random.Range(0f, 2.5f), per + (i == count - 1 ? rem : 0), k > 0.6f && i % 3 == 0,
                    new Vector2(Random.Range(-0.5f, 2.5f), Random.Range(1f, 4f)));
                int idx = i;
                int every = Mathf.Max(1, count / 30);   // 소리는 많아야 30번쯤
                c.OnRest = () => StartCoroutine(c.FlyTo(target, Random.Range(0.05f, 0.3f), v => { Money += v; hud.BumpMoney(); if (idx % every == 0) Sfx.Play(Sfx.Click(), 0.25f, 1.4f + Mathf.Min(idx / every, 30) * 0.03f); }));
                if (idx % every == 0) Sfx.Play(Sfx.Click(), 0.12f, Random.Range(2f, 2.6f));
                if (interval >= 0.01f) yield return new WaitForSecondsRealtime(interval);
                else if (idx % Mathf.CeilToInt(0.01f / interval) == 0) yield return null;
            }
        }

        public void CanHit(Can can, int earned, Vector2 at)
        {
            if (!SetActive) { SetActive = true; SetPending = 0; missed = false; SetMaxAir = 0; SetMaxStreak = 0; }
            int air = Airborne;
            if (air > MostAirborne) MostAirborne = air;
            if (air > SetMaxAir) SetMaxAir = air;
            if (Gun.ReloadStreak > SetMaxStreak) SetMaxStreak = Gun.ReloadStreak;
            hud.Float($"+{earned}", at, can.Combo + (air - 1) * 3);
            if (can.Combo > BestCombo) BestCombo = can.Combo;
            Gun.OnComboHit(can.Combo);
            FillSlow();
            StartCoroutine(HitStop(can.Combo >= 5 ? 0.04f : 0.02f));
        }

        /// <summary>떨어졌다: 쌓인 돈 × 정산 배수가 세트에 들어가고, 맞춘 발 수만큼 탄피가 바닥에. 돈은 세트가 끝나야 온다. 저주통이면 뺏긴다</summary>
        public void CanLanded(Can can)
        {
            if (can.Cursed) { CurseLanded(can); return; }
            long pay = (long)Mathf.Round(can.Earned * PayoutMult);
            SetPending += pay;
            if (pay > 0) hud.Float($"+${pay:N0}", (Vector2)can.transform.position + Vector2.up * 0.8f, 2);
            if (can.Kind.IsBounty) bounty.Escaped(can);
        }

        /// <summary>저주통이 떨어졌다: 이번 세트에 쌓인 돈에서 뺏긴다</summary>
        public void CurseLanded(Can can)
        {
            long lost = System.Math.Min(SetPending, (long)Mathf.Round(can.Earned * PayoutMult));
            SetPending -= lost;
            hud.Float($"저주!  -${lost:N0}", (Vector2)can.transform.position + Vector2.up * 0.8f, 10);
            hud.Hurt();
            Sfx.Play(Sfx.Bang(), 0.8f, 0.4f);
            StartCoroutine(HitStop(0.1f));
        }

        /// <summary>터뜨렸다: 쌓인 돈 × 정산 배수 × 터짐 배수가 세트에. 금색 탄피. 저주통도 터뜨리면 평소대로</summary>
        public void CanBurst(Can can)
        {
            long pay = (long)Mathf.Round(can.Earned * PayoutMult * BurstMult);
            SetPending += pay;
            Bursts++;
            hud.Float($"터짐!  +${pay:N0}", (Vector2)can.transform.position + Vector2.up * 0.6f, 6);
            StartCoroutine(HitStop(can.Kind.IsBounty ? 0.18f : 0.07f));
            if (can.Kind.IsBounty) bounty.Busted(can);
        }

        public void BuyNode(SkillNode n)
        {
            if (n.Maxed || Money < n.NextCost) { Sfx.Play(Sfx.Click(), 0.5f, 0.8f); return; }
            Money -= n.NextCost;
            n.Level++;
            n.Apply(this, n.Level);
            Sfx.Play(Sfx.Reload(), 0.6f, 1.6f);
        }

        IEnumerator HitStop(float seconds)
        {
            if (Time.timeScale < 1f) yield break;
            Time.timeScale = 0.05f;
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            Time.timeScale = 1f;
        }
    }
}
