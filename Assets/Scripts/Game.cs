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
        long money;
        public long Money { get => money; private set { long d = value - money; money = value; if (!DebugMoney) PlayLog.MoneyDelta(d); } }   // 들고 나는 돈은 밸런스 기록에
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
        public int LandBounces { get; set; } = 0;
        public float LandBouncePower { get; set; } = 11f;   // 다시 튀는 세기 (스킬 "낮은 튐" 6.5 → "다시 튐" 11)
        /// <summary>스킬 "둘이면 두 배" (10-14 초반 저글링 유도): 공중에 둘 이상 떠 있을 때 떨어짐·터뜨림 돈 ×N</summary>
        public float JuggleScoreMult { get; set; } = 1f;
        /// <summary>이 깡통까지 쳐서 지금 공중에 몇 개 (떨어지거나 터지는 순간 이미 빠졌을 수 있다)</summary>
        int AirWith(Can can) { int n = Airborne; if (can.Done || !can.Airborne) n++; return n; }
        long JugglePay(Can can, long pay, out bool doubled) { doubled = JuggleScoreMult > 1f && AirWith(can) >= 2; return doubled ? (long)Mathf.Round(pay * JuggleScoreMult) : pay; }        // 스킬 "다시 튐": 떨어진 깡통이 혼자 N번 다시 튀어오른다
        public float FloorSlow { get; set; } = 0f;       // 스킬 "바닥 근처 느려짐": 바닥 가까이에서 낙하 속도가 이만큼 줄어든다 (0.5 = 반)

        // ---------- 슬로우 (우클릭) ----------
        public float SlowGauge { get; private set; }          // 0..1
        public float SlowGaugeMax { get; set; } = 1f;         // 스킬: 게이지 크기 (1 = 3초)
        public float SlowStrength { get; set; } = 0.3f;       // 스킬: 깡통 시간 배수 (작을수록 느림)
        public float SlowFillPerHit { get; set; } = 0.1f;     // 스킬: 맞출 때 차는 양
        public bool SlowActive { get; private set; }
        /// <summary>게이지 1칸(1.0)이 버티는 초 (사용자 10-13: 2 → 3초. 꽉 차야 쓸 수 있으니 덜 자주 쓰는 대신 길게)</summary>
        public const float SlowSecondsPerGauge = 3f;
        /// <summary>남은 초 (켜져 있을 때만). 1초 아래로 내려가면 레터박스가 깜박여 끝을 알린다</summary>
        public float SlowLeft => SlowActive ? SlowGauge * SlowSecondsPerGauge : 0f;
        public bool SlowReady => SlowGauge >= SlowGaugeMax - 0.001f;

        /// <summary>
        /// 우클릭 능력 (사용자 10-13 개정): 게이지가 꽉 찼을 때만 우클릭으로 켠다. 누르는 동안만 쓰고, 떼거나 바닥나면 꺼진다.
        /// 다시 켜려면 또 꽉 채워야 한다. 켜져 있는 동안은 맞혀도 게이지가 차지 않는다 (끝없이 늘어나지 않게).
        /// </summary>
        void UpdateSlow(Keyboard kb)
        {
            var mouse = Mouse.current;
            bool held = mouse != null && mouse.rightButton.isPressed;
            if (!SlowActive && SlowReady && mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                SlowActive = true; Sfx.Play(Sfx.Whoosh(), 0.5f, 0.6f);
            }
            if (SlowActive)
            {
                SlowGauge = Mathf.Max(0f, SlowGauge - Time.deltaTime / SlowSecondsPerGauge);
                if (SlowGauge <= 0f || !held) { SlowActive = false; Sfx.Play(Sfx.Whoosh(), 0.4f, 1.4f); }
            }
            Can.TimeScale = SlowActive ? SlowStrength : 1f;
        }

        public void FillSlow() { if (SlowActive) return; SlowGauge = Mathf.Min(SlowGaugeMax, SlowGauge + SlowFillPerHit); }
        public Gun Gun { get; private set; }
        public List<Can> Cans { get; } = new List<Can>();
        public List<SkillNode> Nodes { get; private set; }
        public HashSet<string> OwnedGuns { get; } = new HashSet<string> { "revolver" };
        public HashSet<string> OwnedCans { get; } = new HashSet<string> { "tin" };
        public bool MenuOpen { get; private set; }
        /// <summary>디버그 (F1): 돈 무한</summary>
        public bool DebugMoney { get; private set; }
        /// <summary>현상금 증표 (사용자 10-14): 현상금을 잡을 때마다 1개. 총은 증표로만 산다 → 총 바꾸는 순간 = 현상금 잡은 순간, 돈은 스킬트리로</summary>
        public int Tokens { get; set; }
        public CanKind SpawnKind { get; set; } = CanKind.Tin;
        Hud hud;
        public Hud Hud => hud;
        TreeView tree;
        Armory armory;
        Booklet booklet;
        float saveTimer;
        float lastSpawnX = float.NaN;
        int lastPage = 2;

        void Awake() { I = this; Can.BounceBonus = 0f; Can.TimeScale = 1f; Can.JuggleFall = Can.NoJuggleFall; Can.AirFall = 1f; Can.DynamiteChance = 0f; }   // 스킬로 바뀌는 정적 값은 씬마다 처음으로

        void Start()
        {
            Look.BuildRange();
            Gun = new GameObject("Gun").AddComponent<Gun>();
            Gun.Build();
            Nodes = SkillTree.Make();
            rootNode = Nodes.Find(n => n.Root);
            nodeNbr = SkillTree.Neighbors(Nodes);
            foreach (var n in Nodes) nodeById[n.Id] = n;
            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.Bind(this);
            new GameObject("AmmoHud").AddComponent<AmmoHud>().Build(hud.Canvas, Gun);
            new GameObject("CanDebug").AddComponent<CanDebug>();   // F9 디버그 표시 (10-14)
            Letterbox.Make(hud.Canvas);   // 우클릭 연출: 위아래 띠 + UI 흐리게 (사용자 10-13)
            tree = new GameObject("Tree").AddComponent<TreeView>();
            tree.Build(this, Nodes, hud.Canvas);
            tree.SetOpen(false);
            armory = new GameObject("Armory").AddComponent<Armory>();
            armory.Build(this, hud.Canvas);
            booklet = new GameObject("Booklet").AddComponent<Booklet>();
            booklet.Build(this, hud.Canvas);
            // 화면 밖 깡통 표식(마름모 + 줄기)은 뺐다 (사용자 10-14: 현상금 줌인 때 엉뚱하게 떴다. 예전 화면용)
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
            PlayLog.Stop();
            if (title != null) { Destroy(title.gameObject); title = null; }
            Money = 10;
            hud.SetPlayVisible(false);   // 원형이 다 열린 뒤에 보인다 (OpenAfterLoad)
            uiReady = false; spawnedOnce = false; summonIdle = 0f;
            if (load) Load();
            PlayLog.Begin(this, load);
            Iris.Get().StartClosed();
            StartCoroutine(OpenAfterLoad());
        }

        /// <summary>
        /// 시작 화면의 원형 닫힘이 플레이 화면에서 이어져 열린다 (사용자 10-14: 전환 뒤엔 효과가 없었다).
        /// 로딩 프레임이 지나간 뒤(2프레임) 잠깐 검정을 쥐었다가(0.1초) 0.45초에 퍼진다
        /// </summary>
        IEnumerator OpenAfterLoad()
        {
            yield return null; yield return null;
            yield return new WaitForSecondsRealtime(0.1f);
            yield return Iris.Get().Open(0.45f);
            if (State == GameState.Play) { uiReady = true; uiStage = -1; }   // 보이는 UI는 단계대로 (UpdateStageUi)
        }

        void Load()
        {
            var d = Save.Read(); if (d == null) return;
            Money = d.money; Bursts = d.bursts; BestCombo = d.bestCombo; BestPayout = d.bestPayout;
            Tokens = d.tokens;
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
            UpdateStageUi();
            if (MenuOpen)
            {
                if (DebugMoney) { Money = 999_999_999; Tokens = 99; }
                if (kb.escapeKey.wasPressedThisFrame) CloseMenu();
                return;
            }
            hud.Buttons(dtUnscaled: Time.unscaledDeltaTime, onTree: () => OpenMenu(2), onShop: () => OpenMenu(0));
            if (kb.spaceKey.wasPressedThisFrame) TrySpawn();
            if (SetActive && !ending && Airborne == 0) StartCoroutine(Settle());
            if (kb.f1Key.wasPressedThisFrame) { DebugMoney = !DebugMoney; hud.Float(DebugMoney ? "디버그: 돈 무한" : "디버그 끔", new Vector2(0f, 2f), 6); }
            if (DebugMoney) { Money = 999_999_999; Tokens = 99; }
            if (kb.f2Key.wasPressedThisFrame) bounty.Summon();
            if (kb.f5Key.wasPressedThisFrame) Can.ThrowDynamite(Gun.MuzzlePos);   // 시험: 다이너마이트 투척
            if (kb.f6Key.wasPressedThisFrame) DebugSpawn(CanKind.Bundle);
            if (kb.f7Key.wasPressedThisFrame) DebugSpawn(CanKind.Blue);
            if (kb.f8Key.wasPressedThisFrame) DebugSpawn(CanKind.Magnet);
            if (kb.f4Key.wasPressedThisFrame) bounty.Summon(kb.shiftKey.isPressed ? CanKind.BountyRoller : CanKind.BountyTest);   // 시험: F4 체력 3 현상금 / Shift+F4 구르는 무법자
            if (kb.f10Key.wasPressedThisFrame) CycleDebugTime();   // 디버그 슬로우
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
            PlayLog.Tick(this);
        }

        void OnApplicationQuit() { if (State == GameState.Play) Save.Write(this); }

        public void OpenMenu(int page) { MenuOpen = true; Gun.Enabled = false; hud.SetButtonsVisible(false); OpenPage(page); }
        public void CloseMenu() { MenuOpen = false; Gun.Enabled = true; tree.SetOpen(false); armory.Hide(); booklet.Hide(); uiStage = -1; Save.Write(this); }   // 버튼은 단계대로 다시 (UpdateStageUi)

        // ---------- 첫 흐름 (사용자 10-14): 들어오면 총만 → 스킬트리 버튼 → 시작 노드 "깡통 소환" → Space ----------
        SkillNode rootNode;
        Dictionary<string, HashSet<string>> nodeNbr; readonly Dictionary<string, SkillNode> nodeById = new Dictionary<string, SkillNode>();
        /// <summary>지금 살 수 있는 노드가 있나 (열린 노드의 이웃 + 돈이 됨). 스킬트리 버튼에 표시 (10-14: 살 때를 몰라 모았다 몰아 샀다)</summary>
        public bool AnyAffordable()
        {
            foreach (var n in Nodes)
            {
                if (n.Maxed || n.NextCost > Money) continue;
                if (n.Root) return true;
                foreach (var o in nodeNbr[n.Id]) if (nodeById.TryGetValue(o, out var m) && m.Level >= 1) return true;
            }
            return false;
        }
        /// <summary>시작 노드를 열었나 = 깡통을 부를 수 있나. 그전엔 화면에 총과 스킬트리 버튼뿐</summary>
        public bool SummonUnlocked => rootNode == null || rootNode.Level >= 1;
        /// <summary>총 상점 버튼: 증표가 생기거나 총이 둘 이상일 때부터</summary>
        public bool ShopUnlocked => Tokens > 0 || OwnedGuns.Count > 1;
        const float SpawnPromptDelay = 8f;   // 소환을 열고 이만큼 안 부르면 "Space" 안내 (사용자: 10초 안에)
        bool uiReady, spawnedOnce; int uiStage = -1; float summonIdle;
        void UpdateStageUi()
        {
            if (!uiReady) return;
            int stage = (SummonUnlocked ? 1 : 0) + (ShopUnlocked ? 2 : 0);
            if (stage != uiStage && !MenuOpen) { uiStage = stage; hud.SetStage(SummonUnlocked, ShopUnlocked); }
            bool canBuy = AnyAffordable();
            hud.PulseTree = !SummonUnlocked || canBuy;
            hud.TreeBadge(SummonUnlocked && canBuy && !MenuOpen, Time.unscaledDeltaTime);
            bool waiting = SummonUnlocked && !spawnedOnce && !MenuOpen;
            if (waiting) summonIdle += Time.unscaledDeltaTime;
            hud.SpawnPrompt(waiting && summonIdle >= SpawnPromptDelay, Time.unscaledDeltaTime);
        }

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
                if (Tokens < k.Price) { Sfx.Play(Sfx.Click(), 0.5f, 0.8f); return; }
                Tokens -= k.Price; OwnedGuns.Add(k.Id);
                PlayLog.Event("gun", k.Id, k.Price);   // 총값 = 증표 개수
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
        public long SpawnCost(CanKind k) => 0;   // 10-14 사용자: 소환은 공짜 ("있으나 마나"). CanKind.Cost는 떨어짐 값 계산에만 쓴다

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
        public int Airborne { get { int n = 0; foreach (var c in Cans) if (c != null && c.Airborne && !c.Done && !c.Kind.NotScored) n++; return n; } }   // 다이너마이트는 공중 수 밖 (배율·세트 끝·소환 잠금)

        /// <summary>돈을 내고 선반에 깡통. 짝 소환이면 여러 개, 붙여 놓기면 마지막 깡통 옆에. 공중에 깡통이 있으면 잠긴다 (세트 중)</summary>
        public float BottleChance { get; set; } = 0f;   // 10-14 사용자: 소환은 일단 기본 깡통만 (전 20%). 트리로 열 자리
        /// <summary>특성 깡통 각각의 확률 (사용자 10-14: 묶음·파랑·자석. 다이너마이트는 쏠 때 1% 투척). 설정·해금은 아직 없음</summary>
        public float TraitChance { get; set; } = 0f;    // 10-14: 일단 0 (전 6%씩)

        /// <summary>깡통 소환 한 번에 나올 종류: 병 20%, 특성 깡통 각 6%, 나머지 깡통</summary>
        CanKind RollKind()
        {
            float r = Random.value;
            if (r < BottleChance) return CanKind.Bottle;
            r -= BottleChance;
            var traits = new[] { CanKind.Bundle, CanKind.Blue, CanKind.Magnet };   // 다이너마이트는 소환이 아니라 쏠 때 1% 투척 (Gun.Fire)
            foreach (var t in traits) { if (r < TraitChance) return t; r -= TraitChance; }
            return CanKind.Tin;
        }

        /// <summary>시험용: 그 종류를 선반 빈자리에 공짜로 (F6 묶음, F7 파랑, F8 자석. F5는 다이너마이트 투척)</summary>
        public void DebugSpawn(CanKind kind)
        {
            if (Airborne > 0) return;
            float x = FreeX(kind);
            Cans.Add(Can.Spawn(kind, new Vector2(x, Look.ShelfY + 0.11f + kind.Height * 0.5f)));
        }
        public bool TrySpawn()
        {
            if (!SummonUnlocked) return false;   // 시작 노드(깡통 소환)를 열어야 부를 수 있다
            spawnedOnce = true;
            long cost = SpawnCost(SpawnKind);
            if (Airborne > 0 && !BountyAlive) { Sfx.Play(Sfx.Click(), 0.5f, 0.6f);   // 현상금전엔 공중에 있어도 소환된다 (사용자 10-14: 터뜨린 깡통의 파편이 현상금을 깎으니 계속 깡통이 필요)
                hud.Float("공중 깡통을 다 처리하면 소환", new Vector2(0f, Look.ShelfY + 0.9f), 0); return false; }
            if (CansOnShelf >= MaxCans) { Sfx.Play(Sfx.Click(), 0.5f, 0.7f); hud.Float("선반이 꽉 찼다", new Vector2(0f, Look.ShelfY + 0.9f), 0); return false; }
            if (Money < cost) { Sfx.Play(Sfx.Click(), 0.5f, 0.7f); hud.Float("돈이 모자라다", new Vector2(0f, Look.ShelfY + 0.9f), 0); return false; }
            if (filling) return false;
            // 10-14 사용자: Space 한 번에 선반이 꽉 차게, 하나씩 차례로 (둘 이상 띄울 기회가 늘어난다)
            StartCoroutine(FillShelf());
            return true;
        }

        bool filling;
        const float FillInterval = 0.09f;
        IEnumerator FillShelf()
        {
            filling = true;
            int n = 0;
            while (CansOnShelf < MaxCans)
            {
                var kind = SpawnKind == CanKind.Tin ? RollKind() : SpawnKind;
                float x = FreeX(kind);   // 종류를 먼저 정해야 폭에 맞는 빈자리를 찾는다
                Cans.Add(Can.Spawn(kind, new Vector2(x, Look.ShelfY + 0.11f + kind.Height * 0.5f)));
                lastSpawnX = x;
                Sfx.Play(Sfx.Reload(), 0.35f, 2.0f + n * 0.15f);   // 차곡차곡: 하나씩 음이 오른다
                n++;
                yield return new WaitForSeconds(FillInterval);
            }
            filling = false;
        }

        /// <summary>
        /// 선반 위 빈자리 (10-14 사용자: 소환이 겹친다 → 많아야 폭의 20%만 겹치게).
        /// 두 깡통 가운데 거리 ≥ (두 폭의 평균) × 0.8. 선반 끝 밖으로 나가지 않게 반폭만큼 안쪽.
        /// 무작위로 몇 번 찾고, 못 찾으면 선반을 훑어 가장 넉넉한 자리 (그래도 겹침은 그 자리에서 가장 적게)
        /// </summary>
        public float FreeX(CanKind kind)
        {
            float half = kind.Width * 0.5f;
            float lo = Look.ShelfLeft + half + 0.05f, hi = Look.ShelfRight - half - 0.05f;
            if (ClusterSpawn)
            {
                float anchor = float.NaN;
                foreach (var c in Cans) if (c != null && !c.Airborne && !c.Done) { anchor = c.transform.position.x; }
                if (float.IsNaN(anchor) && !float.IsNaN(lastSpawnX)) anchor = lastSpawnX;
                if (!float.IsNaN(anchor))
                {
                    float gap = kind.Width + 0.1f;
                    for (int k = 1; k <= 8; k++)
                        foreach (float s in new[] { 1f, -1f })
                        {
                            float x = anchor + s * gap * k;
                            if (x < lo || x > hi) continue;
                            if (Room(x, kind) >= 0f) return x;
                        }
                }
            }
            for (int tries = 0; tries < 30; tries++)
            {
                float x = Random.Range(lo, hi);
                if (Room(x, kind) >= 0f) return x;
            }
            // 못 찾음: 선반을 훑어 여유가 가장 큰 자리
            float best = (lo + hi) * 0.5f, bestRoom = float.MinValue;
            for (float x = lo; x <= hi; x += 1f / 27f) { float r = Room(x, kind); if (r > bestRoom) { bestRoom = r; best = x; } }
            return best;
        }

        public const float MaxOverlap = 0.2f;

        /// <summary>그 자리의 여유: 선반 위 다른 깡통들과의 (가운데 거리 − 허용 최소 거리) 중 가장 작은 값. 0 이상이면 20% 이하로만 겹친다</summary>
        float Room(float x, CanKind kind)
        {
            float room = float.MaxValue;
            foreach (var c in Cans)
            {
                if (c == null || c.Airborne || c.Done) continue;
                float need = (kind.Width + c.Kind.Width) * 0.5f * (1f - MaxOverlap);
                room = Mathf.Min(room, Mathf.Abs(c.transform.position.x - x) - need);
            }
            return room;
        }

        /// <summary>동시 공중 배수 (사용자 10-11: 둘을 띄우게 유도): 1개 ×1, 2개 ×3, 3개 ×6, 4개 ×10 — n(n+1)/2</summary>
        public static int AirMult(int n) => Mathf.Max(1, n * (n + 1) / 2);
        public int MostAirborne { get; private set; }

        // ---------- 세트 배율 (사용자 10-12: 세트가 끝날 때 낚시/오버워치 점수판처럼 딱딱딱 보여 준다) ----------
        // 세트 동안 기본 돈만 쌓이고, 끝에 한꺼번에 곱한다. 저글링 = 세트 중 최대 동시 공중 n(n+1)/2 ·
        // 퍼펙트샷 = 세트 내내 빗나감 없음 ×1.2 · 저스트리로드 = 세트 중 최대 연속마다 +0.2 (최대 ×2)
        // 세트 배율 (10-14 사용자: 초반엔 짜게, 수치 노드로 올린다). 전: 퍼펙트 1.2 / 저스트 +0.2 (최대 2) / 터뜨림 +0.5
        public float PerfectMult { get; set; } = 1.1f;
        public float JustPerStreak { get; set; } = 0.1f;
        public float JustMax { get; set; } = 1.5f;
        public float BurstMultStep { get; set; } = 0.2f;
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

        // ---------- 돈 시스템 단순화 (사용자 10-14): 맞힐 땐 돈 없음. 떨어지면 소환비보다 조금 더(×1.25), 터뜨리면 보상 + 세트 배율 +0.5 ----------
        public const float LandRate = 1.25f;
        /// <summary>떨어졌을 때 받는 돈: 소환비 × 1.25 (올림) × 값 배수 × 정산 배수</summary>
        public long LandPay(CanKind k) => k.Cost <= 0 ? 0 : (long)Mathf.CeilToInt(k.Cost * LandRate * ValueMult * PayoutMult);
        /// <summary>터뜨렸을 때 보상: 깡통 보상 × 값 배수 × (터짐 배수 / 2 — 스킬로 오르는 몫)</summary>
        public long BurstPay(CanKind k) => (long)Mathf.Round(k.BurstReward * ValueMult * (BurstMult / 2f));
        /// <summary>이번 세트의 터뜨림 배율: ×1에서 시작해 터뜨릴 때마다 +0.5. 세트가 끝날 때 합계에 곱한다</summary>
        public float SetBurstMult { get; private set; } = 1f;
        /// <summary>이번 세트에 터뜨린 수. 배율은 둘째부터 붙는다 (10-14: 하나만 터뜨려도 ×1.5가 붙던 것 → 1개 ×1, 2개 ×1.5, 3개 ×2)</summary>
        public int SetBursts { get; private set; }
        string MultTag() => SetBursts >= 2 ? $"  ×{SetBurstMult:0.#}" : "";
        void AddSetBurst() { SetBursts++; SetBurstMult = 1f + BurstMultStep * Mathf.Max(0, SetBursts - 1); }

        float nudgeTimer; int nudges;
        /// <summary>하나만 떠 있고 선반에 깡통이 있으면 "하나 더 띄우면 ×3" (처음 다섯 번, 10초에 한 번)</summary>
        void Nudge()
        {
            return;   // 10-14: 저글링 배율(×3)이 없어져서 안내도 끈다
#pragma warning disable CS0162
            if (nudges >= 5 || MostAirborne >= 2) return;
            nudgeTimer -= Time.deltaTime;
            if (nudgeTimer > 0f) return;
            if (Airborne != 1 || CansOnShelf == 0) return;
            foreach (var c in Cans) if (c != null && !c.Airborne) { hud.Float("하나 더 띄우면 ×3", (Vector2)c.transform.position + Vector2.up * 0.9f, 5); break; }
            nudgeTimer = 10f; nudges++;
#pragma warning restore CS0162
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

        /// <summary>세트 정산 중 (점수판·탄피가 날아가는 동안). 현상금은 이게 끝나야 나온다 (10-14)</summary>
        public bool Settling { get; private set; }
        IEnumerator Settle() { Settling = true; yield return EndSet(); Settling = false; }

        IEnumerator EndSet()
        {
            ending = true;
            yield return new WaitForSeconds(0.15f);
            if (Airborne > 0) { ending = false; yield break; }   // 그새 또 띄웠으면 세트가 이어진다
            long baseSum = SetPending;
            float burstMult = SetBurstMult;
            float perfect = missed ? 1f : PerfectMult, just = JustMultOf(SetMaxStreak);
            long total = (long)Mathf.Round(baseSum * burstMult * perfect * just);
            SetPending = 0; SetActive = false; ending = false;
            if (total <= 0) yield break;
            PlayLog.Event("set", "", total);
            bool best = total > BestPayout;
            if (best) BestPayout = total;
            float k = Ceremony(total, best);
            // 점수판 (돈 판자 아래): 기본 → 켜진 배율들 → 합계. 작으면 빠르게 몇 줄, 크면 천천히 전부
            var lines = new List<(string, bool)>
            {
                ($"기본  ${baseSum:N0}", true),
                ($"터뜨림  ×{burstMult:0.#}", burstMult > 1f),
                ($"퍼펙트샷  ×{perfect:0.#}", perfect > 1f),
                ($"저스트리로드  ×{just:0.#}", just > 1f),
            };
            if (k < 0.5f) lines.RemoveAll(l => !l.Item2);   // 작은 세트는 ×1 줄을 안 찍는다
            if (k < 0.2f) lines.Clear();                      // 아주 작으면 합계 한 줄만
            Vector2 totalAt = Vector2.zero;
            yield return hud.Tally(lines, best ? $"${total:N0}  최고!" : $"${total:N0}", k, at => totalAt = at);
            // 정산과 함께 바닥의 탄피가 점수판으로 빨려 들어간다 (사용자 10-13). 이번 세트에 흘린 탄피 전부에 합계를 나눠 싣는다.
            // 가까운(왼쪽) 것부터 차례로, 쏟는 시간은 연출 크기로. 아직 떨어지는 중인 탄피는 눕는 순간 출발
            var cs = setCasings.FindAll(c => c != null && !c.Flying);
            setCasings.Clear();
            if (cs.Count == 0) { Money += total; hud.BumpMoney(); yield break; }
            var target = hud.MoneyWorld();
            cs.Sort((a, b) => ((Vector2)a.transform.position - target).sqrMagnitude.CompareTo(((Vector2)b.transform.position - target).sqrMagnitude));
            int count = cs.Count;
            long per = total / count, rem = total - per * count;
            float interval = Mathf.Lerp(0.25f, 1.2f, k) / count;
            int every = Mathf.Max(1, count / 30);   // 소리는 많아야 30번쯤
            for (int i = 0; i < count; i++)
            {
                var c = cs[i];
                if (c == null) { Money += per + (i == count - 1 ? rem : 0); continue; }
                c.Value = per + (i == count - 1 ? rem : 0);
                int idx = i;
                System.Action fly = () => StartCoroutine(c.FlyTo(target, 0f, v => { Money += v; hud.BumpMoney(); if (idx % every == 0) Sfx.Play(Sfx.Click(), 0.25f, 1.4f + Mathf.Min(idx / every, 30) * 0.03f); }));
                if (c.Resting) fly(); else c.OnRest = fly;
                if (interval >= 0.01f) yield return new WaitForSecondsRealtime(interval);
                else if (idx % Mathf.CeilToInt(0.01f / interval) == 0) yield return null;
            }
        }

        public void CanHit(Can can, int earned, Vector2 at)
        {
            if (can.Kind.NotScored) { StartCoroutine(HitStop(0.02f)); return; }   // 다이너마이트: 세트·기록·배율·게이지 밖 (맞는 손맛만)
            if (!SetActive) { SetActive = true; SetPending = 0; missed = false; SetMaxAir = 0; SetMaxStreak = 0; SetBurstMult = 1f; SetBursts = 0; }
            int air = Airborne;
            if (air > MostAirborne) MostAirborne = air;
            if (air > SetMaxAir) SetMaxAir = air;
            if (Gun.ReloadStreak > SetMaxStreak) SetMaxStreak = Gun.ReloadStreak;
            if (can.Combo > BestCombo) BestCombo = can.Combo;
            Gun.OnComboHit(can.Combo);
            FillSlow();
            StartCoroutine(HitStop(can.Combo >= 5 ? 0.04f : 0.02f));
        }

        /// <summary>떨어졌다: 쌓인 돈 × 정산 배수가 세트에 들어가고, 맞춘 발 수만큼 탄피가 바닥에. 돈은 세트가 끝나야 온다. 저주통이면 뺏긴다</summary>
        public void CanLanded(Can can)
        {
            if (can.Kind.NotScored) return;   // 계산 밖 물체: 떨어져도 정산 없음
            if (!can.Kind.NoCasings) SpillCasings(new Vector2(can.transform.position.x, Look.DropY + 0.4f), LandCasings, false, false);   // 떨어진 자리에 탄피를 조금 흘린다 (보조 깡통은 안 흘림)
            if (can.Cursed) { CurseLanded(can); return; }
            long pay = JugglePay(can, LandPay(can.Kind), out bool dbl);   // 소환비보다 조금 더
            SetPending += pay;
            if (pay > 0) hud.Float(dbl ? $"+${pay:N0}  ×{JuggleScoreMult:0.#}" : $"+${pay:N0}", (Vector2)can.transform.position + Vector2.up * 0.8f, dbl ? 4 : 2);
            if (can.Kind.IsBounty) bounty.Escaped(can);
        }

        /// <summary>저주통이 떨어졌다: 이번 세트에 쌓인 돈에서 뺏긴다</summary>
        public void CurseLanded(Can can)
        {
            long lost = System.Math.Min(SetPending, LandPay(can.Kind) + can.Kind.Cost);   // 10-14: 떨어진 값 + 소환비만큼
            SetPending -= lost;
            hud.Float($"저주!  -${lost:N0}", (Vector2)can.transform.position + Vector2.up * 0.8f, 10);
            hud.Hurt();
            Sfx.Play(Sfx.Bang(), 0.8f, 0.4f);
            StartCoroutine(HitStop(0.1f));
        }

        // ---------- 탄피 (사용자 10-13 개정) ----------
        // 터뜨리면 깡통에서 탄피가 튀어나와 바닥에 떨어지고, 바닥에 떨어진 깡통은 그 자리에 조금 흘린다. 탄피는 바닥에 쌓여 있다가
        // 세트가 끝나면 정산과 함께 점수판으로 빨려 들어가 돈이 된다 (합계를 탄피 수로 나눠 싣는다). 탄피 단위는 나중에 (지금은 통일)
        const int BurstCasings = 8, BountyCasings = 48, LandCasings = 3;
        readonly List<Casing> setCasings = new List<Casing>();

        /// <summary>탄피를 흘린다. burst = 깡통에서 사방으로 튀어나옴 / 아니면 바닥 근처에서 옆으로 조금</summary>
        public void SpillCasings(Vector2 at, int n, bool burst, bool gold)
        {
            for (int i = 0; i < n; i++)
            {
                Vector2 v = burst
                    ? new Vector2(Random.Range(-1f, 1f), Random.Range(0.3f, 1f)).normalized * Random.Range(3f, 8f)
                    : new Vector2(Random.Range(-1.6f, 1.6f), Random.Range(1f, 2.6f));
                setCasings.Add(Casing.Drop(at + Random.insideUnitCircle * 0.15f, 0, gold, v));
            }
        }

        /// <summary>터뜨렸다: 쌓인 돈 × 정산 배수 × 터짐 배수가 세트에. 탄피가 튀어나와 바닥에. 저주통도 터뜨리면 평소대로</summary>
        public void CanBurst(Can can)
        {
            if (can.Kind.NotScored) { StartCoroutine(HitStop(0.07f)); return; }   // 계산 밖 물체: 터져도 돈·터뜨림 수 없음 (폭발 자체는 Can.TraitBurst)
            if (!can.Kind.NoCasings) SpillCasings(can.transform.position, can.Kind.IsBounty ? BountyCasings : BurstCasings, true, can.Kind.IsBounty);   // 보조 깡통(다이너마이트)은 탄피 없음
            // 10-14 사용자: 하나가 터지면 선반의 모든 깡통이 파편에 튀어 오른다 (저글링 학습). 현상금은 제외 (파편 피해 규칙이 따로 있다)
            foreach (var c in Cans) if (c != null && c != can && !c.Done && !c.Airborne && !c.Kind.IsBounty) c.Pop(can.transform.position);
            long pay = JugglePay(can, BurstPay(can.Kind), out bool dbl);
            SetPending += pay;
            AddSetBurst();   // 한 세트에 둘째부터 배율 +0.5
            Bursts++;
            hud.Float($"터짐!  +${pay:N0}{(dbl ? "  둘 ×" + JuggleScoreMult.ToString("0.#") : "")}{MultTag()}", (Vector2)can.transform.position + Vector2.up * 0.6f, 6);
            StartCoroutine(HitStop(can.Kind.IsBounty ? 0.18f : 0.07f));
            if (can.Kind.IsBounty) bounty.Busted(can);
            if (can.Kind.IsBounty && !Gun.Cinematic) Gun.StartChain(can.transform.position);   // 막타 연출이 아니었어도 공중 깡통들을 차례로 터뜨린다
            // (현상금전 피해는 부하 박치기가 맡는다 — Can.Ram, MinionScored. 부하는 현상금이 있는 동안 터지지 않는다)
        }

        /// <summary>
        /// 부하 박치기 정산 (사용자 10-14): 부하는 터지지 않지만 보상은 세트에 쌓이고 탄피가 나온다. 세트 배율·터뜨림 수에는 안 들어간다 (10-14: 박치기 5번이 배율 +2.5·다음 현상금 카운트까지 올려 과했다).
        /// 피해는 박치기가 현상금에 닿을 때 (Can.Ram)
        /// </summary>
        public void MinionScored(Can can)
        {
            SpillCasings(can.transform.position, BurstCasings, true, false);
            long pay = BurstPay(can.Kind);
            SetPending += pay;
            hud.Float($"+${pay:N0}", (Vector2)can.transform.position + Vector2.up * 0.6f, 6);
            StartCoroutine(HitStop(0.05f));
        }
        /// <summary>현상금이 살아 있나 (선반·공중 상관없이). 살아 있는 동안 다른 깡통은 터지지 않는다 (사용자 10-14)</summary>
        /// <summary>파편 피해 (현상금전, 사용자 10-14). 스킬로 키울 자리</summary>
        public static int ShrapnelDamage = 3;   // 10-14: 부하 파편만 피해 → 체력 15를 부하 다섯으로 막타 직전까지

        public bool BountyAlive { get { foreach (var c in Cans) if (c != null && !c.Done && c.Kind.IsBounty) return true; return false; } }

        public void BuyNode(SkillNode n)
        {
            if (n.Maxed || Money < n.NextCost) { Sfx.Play(Sfx.Click(), 0.5f, 0.8f); return; }
            PlayLog.Event("node", n.Id, n.NextCost);
            Money -= n.NextCost;
            n.Level++;
            n.Apply(this, n.Level);
            Sfx.Play(Sfx.Reload(), 0.6f, 1.6f);
        }

        IEnumerator HitStop(float seconds)
        {
            if (Time.timeScale < DebugTime - 0.001f) yield break;   // 이미 멈춤·연출 중
            Time.timeScale = 0.05f * DebugTime;
            float t = 0f;
            while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
            Time.timeScale = DebugTime;
        }

        // ---------- 디버그 슬로우 F10 (사용자 10-14): 누를 때마다 전체 시간 1 → 0.5 → 0.3 → 0.2 → 1 ----------
        /// <summary>게임 전체 시간 배수 (디버그). 멈춤·현상금 연출은 이 위에 곱한다</summary>
        public static float DebugTime = 1f;
        static readonly float[] DebugSteps = { 1f, 0.5f, 0.3f, 0.2f };
        void CycleDebugTime()
        {
            int i = System.Array.IndexOf(DebugSteps, DebugTime);
            DebugTime = DebugSteps[(i + 1) % DebugSteps.Length];
            if (!Gun.Cinematic) { Time.timeScale = DebugTime; Time.fixedDeltaTime = 0.02f * DebugTime; }   // 물리 칸도 같이 줄여야 느려도 부드럽다
            hud.Float($"속도 ×{DebugTime:0.#}", new Vector2(0f, 2.8f), 8);
        }
    }
}
