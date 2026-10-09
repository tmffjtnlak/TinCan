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
    /// 핵심 루프: 돈을 내고 깡통을 소환한다(선반) → 쏴서 띄운다 → 공중에서 계속 맞춘다 → 낙하지역에 떨어지면 정산.
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
        public float AutoGunSeconds { get; set; } = 0f;
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
        MenuTabs tabs;
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
            tree = new GameObject("Tree").AddComponent<TreeView>();
            tree.Build(this, Nodes, hud.Canvas);
            tree.SetOpen(false);
            armory = new GameObject("Armory").AddComponent<Armory>();
            armory.Build(this, hud.Canvas);
            tabs = new GameObject("MenuTabs").AddComponent<MenuTabs>();
            tabs.Build(hud.Canvas);
            new GameObject("Markers").AddComponent<OffscreenMarkers>();
            AudioListener.volume = PlayerPrefs.GetFloat("volume", 1f);

            if (pendingPlay) { bool load = pendingLoad; pendingPlay = pendingLoad = false; BeginPlay(load); }
            else EnterTitle();
        }

        // ================= 타이틀 =================
        readonly List<Text> menuLabels = new List<Text>();
        Text signTitle, tagline;
        readonly List<ShootButton> settingButtons = new List<ShootButton>();

        void EnterTitle()
        {
            State = GameState.Title;
            Money = 0;
            hud.SetPlayVisible(false);
            signTitle = Ui.Text("SignTitle", hud.Canvas, new Vector2(0f, 300f), new Vector2(1200f, 120f), 96, TextAnchor.MiddleCenter, Ui.Gold);
            signTitle.text = "깡통";   // 가제. 간판 그림이 오면 교체
            tagline = Ui.Text("Tagline", hud.Canvas, new Vector2(0f, 220f), new Vector2(1200f, 50f), 26, TextAnchor.MiddleCenter, Ui.Dim);
            tagline.text = "쏴서 띄우고, 떨어지기 전에 또 쏴라.";
            float y = Look.ShelfY + 0.11f + 0.35f;
            MenuCan("시작", new Vector2(Save.Exists ? -1.6f : 0f, y), () => StartCoroutine(GoPlay(load: false)));
            if (Save.Exists) MenuCan("이어서", new Vector2(0f, y), () => StartCoroutine(GoPlay(load: true)));
            MenuCan("설정", new Vector2(Save.Exists ? 1.6f : 1.8f, y), OpenSettings);
            if (!Save.Exists) { Cans[0].transform.position = new Vector3(-0.9f, y + 1.2f, 0f); Cans[1].transform.position = new Vector3(0.9f, y + 1.2f, 0f); }
        }

        void MenuCan(string label, Vector2 at, System.Action onLanded)
        {
            var can = Can.Spawn(CanKind.Tin, at);
            can.MakeMenu(onLanded);
            Cans.Add(can);
            var t = Ui.Text("MenuLabel", hud.Canvas, Vector2.zero, new Vector2(300f, 50f), 30, TextAnchor.MiddleCenter, Ui.Ink);
            t.text = label;
            menuLabels.Add(t);
            StartCoroutine(FollowCan(t, can));
        }

        IEnumerator FollowCan(Text t, Can can)
        {
            var cam = Camera.main;
            while (can != null && t != null)
            {
                var sp = cam.WorldToScreenPoint(can.transform.position + Vector3.up * 0.85f);
                ((RectTransform)t.transform).anchoredPosition = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f) * (1080f / Screen.height);
                t.color = can.Airborne ? new Color(Ui.Ink.r, Ui.Ink.g, Ui.Ink.b, 0.35f) : Ui.Ink;
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        IEnumerator GoPlay(bool load)
        {
            if (State != GameState.Title) yield break;
            State = GameState.Settings; // 더 못 쏘게
            Gun.Enabled = false;
            Sfx.Play(Sfx.Reload(), 0.8f, 1.4f);
            // 글자는 원형 페이드 위에 그려지니까 먼저 치운다
            if (signTitle != null) Destroy(signTitle.gameObject);
            if (tagline != null) Destroy(tagline.gameObject);
            foreach (var l in menuLabels) if (l != null) Destroy(l.gameObject);
            yield return Iris.Get().Close(0.55f);
            if (!load) Save.Delete();
            pendingPlay = true; pendingLoad = load;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        void OpenSettings()
        {
            if (State != GameState.Title) return;
            State = GameState.Settings;
            foreach (var l in menuLabels) if (l != null) l.gameObject.SetActive(false);
            float y = 0.9f;
            settingButtons.Add(ShootButton.Make("VolDown", new Vector2(-3.2f, y), new Vector2(1.6f, 0.9f), () => "소리 −", () => SetVolume(AudioListener.volume - 0.1f), hud.Canvas));
            settingButtons.Add(ShootButton.Make("Vol", new Vector2(-1.2f, y), new Vector2(1.9f, 0.9f), () => $"소리 {Mathf.RoundToInt(AudioListener.volume * 100)}%", () => { }, hud.Canvas));
            settingButtons.Add(ShootButton.Make("VolUp", new Vector2(0.8f, y), new Vector2(1.6f, 0.9f), () => "소리 +", () => SetVolume(AudioListener.volume + 0.1f), hud.Canvas));
            settingButtons.Add(ShootButton.Make("Full", new Vector2(3.0f, y), new Vector2(2.2f, 0.9f), () => Screen.fullScreen ? "창 모드로" : "전체 화면으로", () => Screen.fullScreen = !Screen.fullScreen, hud.Canvas));
            settingButtons.Add(ShootButton.Make("Back", new Vector2(0f, y - 1.4f), new Vector2(2.4f, 0.9f), () => "돌아가기", CloseSettings, hud.Canvas));
        }

        void SetVolume(float v) { AudioListener.volume = Mathf.Clamp01(v); PlayerPrefs.SetFloat("volume", AudioListener.volume); PlayerPrefs.Save(); }

        void CloseSettings()
        {
            foreach (var b in settingButtons) if (b != null) Destroy(b.gameObject);
            settingButtons.Clear();
            foreach (var l in menuLabels) if (l != null) Destroy(l.gameObject);
            menuLabels.Clear();
            foreach (var c in Cans) if (c != null) Destroy(c.gameObject);
            Cans.Clear();
            if (signTitle != null) Destroy(signTitle.gameObject);
            if (tagline != null) Destroy(tagline.gameObject);
            EnterTitle();
        }

        // ================= 플레이 =================
        void BeginPlay(bool load)
        {
            State = GameState.Play;
            Money = 10;
            hud.SetPlayVisible(true);
            if (load) Load();
            Iris.Get().StartClosed();
            StartCoroutine(Iris.Get().Open(0.6f));
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
                if (n == null || n.Root) continue;
                n.Level = d.nodeLevels[i];
                if (n.Level > 0) n.Apply(this, n.Level);
            }
            var g = GunKind.ForSale.Find(x => x.Id == d.gun); if (g != null) Gun.Equip(g);
            var k = CanKind.ForSale.Find(x => x.Id == d.spawn); if (k != null) SpawnKind = k;
        }

        void Update()
        {
            Cans.RemoveAll(c => c == null);
            var kb = Keyboard.current;
            if (kb == null) return;
            if (State != GameState.Play)
            {
                Gun.Enabled = State != GameState.Settings || settingButtons.Count > 0;
                if (State == GameState.Settings && settingButtons.Count > 0 && kb.escapeKey.wasPressedThisFrame) CloseSettings();
                return;
            }
            if (kb.tabKey.wasPressedThisFrame)
            {
                MenuOpen = !MenuOpen;
                Gun.Enabled = !MenuOpen;
                if (MenuOpen) OpenPage(lastPage); else { tree.SetOpen(false); armory.Hide(); tabs.Hide(); Save.Write(this); }
            }
            if (MenuOpen)
            {
                if (DebugMoney) Money = 999_999_999;
                if (kb.qKey.wasPressedThisFrame) OpenPage(0);
                if (kb.wKey.wasPressedThisFrame) OpenPage(1);
                if (kb.eKey.wasPressedThisFrame) OpenPage(2);
                return;
            }
            if (kb.spaceKey.wasPressedThisFrame) TrySpawn();
            if (kb.f1Key.wasPressedThisFrame) { DebugMoney = !DebugMoney; hud.Float(DebugMoney ? "디버그: 돈 무한" : "디버그 끔", new Vector2(0f, 2f), 6); }
            if (DebugMoney) Money = 999_999_999;
            UpdateSlow(kb);
            AutoGun();
            saveTimer += Time.deltaTime;
            if (saveTimer > 5f) { saveTimer = 0f; Save.Write(this); }
        }

        void OnApplicationQuit() { if (State == GameState.Play) Save.Write(this); }

        void OpenPage(int p)
        {
            lastPage = p;
            tree.SetOpen(p == 2);
            if (p == 2) armory.Hide(); else armory.Show(p == 0 ? Armory.Page.Guns : Armory.Page.Cans);
            tabs.Show(p);
        }

        // ---------- 총기류 ----------
        public void PickGun(GunKind k)
        {
            if (!OwnedGuns.Contains(k.Id))
            {
                if (Money < k.Price) { Sfx.Play(Sfx.Click(), 0.5f, 0.8f); return; }
                Money -= k.Price; OwnedGuns.Add(k.Id);
            }
            Gun.Equip(k);
            Sfx.Play(Sfx.Reload(), 0.8f, 1.2f);
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

        AutoTurret turret;
        /// <summary>자동 사격기: 스킬을 사면 선반 옆에 기계가 서고, 보이는 깡통에 조준점이 움직여 가서 쏜다</summary>
        void AutoGun()
        {
            if (AutoGunSeconds <= 0f) return;
            if (turret == null) { turret = AutoTurret.Make(); hud.Float("자동 사격기 설치", new Vector2(Look.ShelfRight + 0.5f, Look.ShelfY + 1.2f), 5); }
            turret.Interval = AutoGunSeconds;
            turret.Tick(Time.deltaTime);
        }

        public int CansOnShelf => Cans.FindAll(c => c != null && !c.Airborne).Count;
        public int Airborne { get { int n = 0; foreach (var c in Cans) if (c != null && c.Airborne && !c.Done) n++; return n; } }

        /// <summary>돈을 내고 선반에 깡통. 짝 소환이면 여러 개, 붙여 놓기면 마지막 깡통 옆에</summary>
        public bool TrySpawn()
        {
            long cost = SpawnCost(SpawnKind);
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

        /// <summary>한 발에 쌓이는 돈: 값 × 값배수 × (연속 × 연속배수) × 총 배수 × 동시 공중 수</summary>
        public int EarnFor(CanKind kind, int combo, float scale) => Mathf.Max(1, Mathf.RoundToInt(kind.Value * ValueMult * combo * ComboMult * scale * Mathf.Max(1, Airborne)));

        public void CanHit(Can can, int earned, Vector2 at)
        {
            int air = Airborne;
            hud.Float(air >= 2 ? $"+{earned}  ×{air}" : $"+{earned}", at, can.Combo + (air - 1) * 2);
            if (can.Combo > BestCombo) BestCombo = can.Combo;
            Gun.OnComboHit(can.Combo);
            FillSlow();
            StartCoroutine(HitStop(can.Combo >= 5 ? 0.04f : 0.02f));
        }

        /// <summary>떨어져서 정산: 쌓인 돈 × 정산 배수. 저주통이면 뺏긴다</summary>
        public void CanLanded(Can can)
        {
            if (can.Cursed) { CurseLanded(can); return; }
            long pay = (long)Mathf.Round(can.Earned * PayoutMult);
            Money += pay;
            if (pay > BestPayout) BestPayout = pay;
            hud.Payout(pay, can.Combo, can.transform.position, burst: false);
            if (pay > 0) Fx.Coins(can.transform.position, pay);
        }

        public void CurseLanded(Can can)
        {
            long lost = System.Math.Min(Money, (long)Mathf.Round(can.Earned * PayoutMult));
            Money -= lost;
            hud.Float($"저주!  -${lost:N0}", (Vector2)can.transform.position + Vector2.up * 0.8f, 10);
            hud.Hurt();
            Sfx.Play(Sfx.Bang(), 0.8f, 0.4f);
            StartCoroutine(HitStop(0.1f));
        }

        /// <summary>터뜨려서 정산: 쌓인 돈 × 정산 배수 × 터짐 배수. 저주통도 터뜨리면 평소대로</summary>
        public void CanBurst(Can can)
        {
            long pay = (long)Mathf.Round(can.Earned * PayoutMult * BurstMult);
            Money += pay;
            Bursts++;
            if (pay > BestPayout) BestPayout = pay;
            hud.Payout(pay, can.Combo, can.transform.position, burst: true);
            Fx.Coins(can.transform.position, pay * 3);
            StartCoroutine(HitStop(0.07f));
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
