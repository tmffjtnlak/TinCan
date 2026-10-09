using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinCan
{
    /// <summary>
    /// 핵심 루프: 돈을 내고 깡통을 소환한다(선반) → 쏴서 띄운다 → 공중에서 계속 맞춘다 → 낙하지역에 떨어지면 정산.
    /// 콘텐츠 세 축: 총기류(쏘는 방식) · 깡통류(뭘 띄우나) · 스킬트리(얼마나 잘). Tab으로 연다. Q/W/E로 탭.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }
        public long Money { get; private set; }
        public int BestCombo { get; private set; }
        public long BestPayout { get; private set; }
        public int MaxCans { get; set; } = 3;
        public float ValueMult { get; set; } = 1f;
        public float ComboMult { get; set; } = 1f;
        public float PayoutMult { get; set; } = 1f;
        public float SpawnDiscount { get; set; } = 0f;
        public bool ClusterSpawn { get; set; } = false;   // 스킬 "붙여 놓기": 마지막 깡통 옆에
        public int SpawnCount { get; set; } = 1;           // 스킬 "짝 소환": Space 한 번에 몇 개
        float lastSpawnX = float.NaN;
        public float AutoGunSeconds { get; set; } = 0f;
        public Gun Gun { get; private set; }
        public List<Can> Cans { get; } = new List<Can>();
        public List<SkillNode> Nodes { get; private set; }
        public HashSet<string> OwnedGuns { get; } = new HashSet<string> { "revolver" };
        public HashSet<string> OwnedCans { get; } = new HashSet<string> { "tin" };
        public bool MenuOpen { get; private set; }
        public CanKind SpawnKind { get; set; } = CanKind.Tin;
        Hud hud;
        public Hud Hud => hud;
        TreeView tree;
        Armory armory;
        float autoGunTimer;

        void Awake() { I = this; Can.BounceBonus = 0f; }

        void Start()
        {
            Look.BuildRange();
            Money = 10;
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
            new GameObject("Markers").AddComponent<OffscreenMarkers>();
        }

        void Update()
        {
            Cans.RemoveAll(c => c == null);
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.tabKey.wasPressedThisFrame)
            {
                MenuOpen = !MenuOpen;
                Gun.Enabled = !MenuOpen;
                if (MenuOpen) OpenPage(lastPage); else { tree.SetOpen(false); armory.Hide(); }
            }
            if (MenuOpen)
            {
                if (kb.qKey.wasPressedThisFrame) OpenPage(0);
                if (kb.wKey.wasPressedThisFrame) OpenPage(1);
                if (kb.eKey.wasPressedThisFrame) OpenPage(2);
                return;
            }
            if (kb.spaceKey.wasPressedThisFrame) TrySpawn();
            AutoGun();
        }

        int lastPage = 2;
        void OpenPage(int p)
        {
            lastPage = p;
            tree.SetOpen(p == 2);
            if (p == 2) armory.Hide(); else armory.Show(p == 0 ? Armory.Page.Guns : Armory.Page.Cans);
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

        /// <summary>자동 사격기: 공중에 있는 깡통 중 하나를 주기적으로 맞춘다 (값 절반)</summary>
        void AutoGun()
        {
            if (AutoGunSeconds <= 0f) return;
            autoGunTimer += Time.deltaTime;
            if (autoGunTimer < AutoGunSeconds) return;
            Can target = null;
            foreach (var c in Cans) if (c != null && c.Airborne && !c.Done) { target = c; break; }
            if (target == null) return;
            autoGunTimer = 0f;
            var at = (Vector2)target.transform.position + new Vector2(Random.Range(-0.1f, 0.1f), -0.1f);
            int earned = target.Hit(at, 0.5f);
            hud.Float($"+{earned}", at, 1);
            Sfx.Play(Sfx.Bang(), 0.35f, 1.4f);
        }

        public int CansOnShelf => Cans.FindAll(c => c != null && !c.Airborne).Count;

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
            // 붙여 놓기: 선반 위 깡통 옆자리 중 비어 있는 곳 (오른쪽, 왼쪽 순)
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

        /// <summary>지금 공중에 떠 있는 깡통 수 (사용자 10-10: 하나만 띄우면 너무 쉽다 → 동시에 띄운 수가 배수)</summary>
        public int Airborne { get { int n = 0; foreach (var c in Cans) if (c != null && c.Airborne && !c.Done) n++; return n; } }

        /// <summary>한 발에 쌓이는 돈: 값 × 값배수 × (연속 × 연속배수) × 총 배수 × 동시 공중 수</summary>
        public int EarnFor(CanKind kind, int combo, float scale) => Mathf.Max(1, Mathf.RoundToInt(kind.Value * ValueMult * combo * ComboMult * scale * Mathf.Max(1, Airborne)));

        public void CanHit(Can can, int earned, Vector2 at)
        {
            int air = Airborne;
            hud.Float(air >= 2 ? $"+{earned}  ×{air}" : $"+{earned}", at, can.Combo + (air - 1) * 2);
            if (can.Combo > BestCombo) BestCombo = can.Combo;
            Gun.OnComboHit(can.Combo);
            StartCoroutine(HitStop(can.Combo >= 5 ? 0.04f : 0.02f));
        }

        /// <summary>떨어져서 정산: 쌓인 돈 × 정산 배수. 터뜨리기 전에 떨어진 것</summary>
        public void CanLanded(Can can)
        {
            long pay = (long)Mathf.Round(can.Earned * PayoutMult);
            Money += pay;
            if (pay > BestPayout) BestPayout = pay;
            hud.Payout(pay, can.Combo, can.transform.position, burst: false);
        }

        public float BurstMult { get; set; } = 2f;   // 터뜨리면 최고치: 쌓인 돈 × 2

        /// <summary>터뜨려서 정산: 쌓인 돈 × 정산 배수 × 터짐 배수. 이게 최고치</summary>
        public void CanBurst(Can can)
        {
            long pay = (long)Mathf.Round(can.Earned * PayoutMult * BurstMult);
            Money += pay;
            Bursts++;
            if (pay > BestPayout) BestPayout = pay;
            hud.Payout(pay, can.Combo, can.transform.position, burst: true);
            StartCoroutine(HitStop(0.07f));
        }
        public int Bursts { get; private set; }

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
