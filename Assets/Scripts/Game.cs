using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinCan
{
    /// <summary>
    /// 핵심 루프: 돈을 내고 깡통을 소환한다(선반) → 쏴서 띄운다 → 공중에서 계속 맞춘다 → 낙하지역에 떨어지면 정산.
    /// 스킬트리는 Tab으로 따로 연다.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }
        public long Money { get; private set; }
        public int BestCombo { get; private set; }
        public long BestPayout { get; private set; }
        public int MaxCans { get; set; } = 3;         // 선반에 동시에 둘 수 있는 수
        public float ValueMult { get; set; } = 1f;    // 맞출 때 값 배수
        public float ComboMult { get; set; } = 1f;    // 연속 하나당 배수
        public float PayoutMult { get; set; } = 1f;   // 정산 배수
        public float AutoGunSeconds { get; set; } = 0f; // 0 = 자동 사격기 없음
        public Gun Gun { get; private set; }
        public List<Can> Cans { get; } = new List<Can>();
        public List<SkillNode> Nodes { get; private set; }
        public bool TreeOpen { get; private set; }
        public CanKind SpawnKind { get; set; } = CanKind.Tin;
        Hud hud;
        TreeView tree;
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
        }

        void Update()
        {
            Cans.RemoveAll(c => c == null);
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.tabKey.wasPressedThisFrame) { TreeOpen = !TreeOpen; tree.SetOpen(TreeOpen); Gun.Enabled = !TreeOpen; }
            if (TreeOpen) return;
            if (kb.spaceKey.wasPressedThisFrame) TrySpawn();
            AutoGun();
        }

        /// <summary>자동 사격기: 공중에 있는 깡통 중 하나를 주기적으로 맞춘다 (손맛보다 약하게 — 값은 절반)</summary>
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

        /// <summary>돈을 내고 선반에 깡통 하나. 자리는 선반 위 빈 곳 중 무작위</summary>
        public bool TrySpawn()
        {
            if (CansOnShelf >= MaxCans) { Sfx.Play(Sfx.Click(), 0.5f, 0.7f); hud.Float("선반이 꽉 찼다", new Vector2(0f, Look.ShelfY + 0.9f), 0); return false; }
            if (Money < SpawnKind.Cost) { Sfx.Play(Sfx.Click(), 0.5f, 0.7f); hud.Float("돈이 모자라다", new Vector2(0f, Look.ShelfY + 0.9f), 0); return false; }
            Money -= SpawnKind.Cost;
            float x = FreeX();
            Cans.Add(Can.Spawn(SpawnKind, new Vector2(x, Look.ShelfY + 0.11f + SpawnKind.Height * 0.5f)));
            Sfx.Play(Sfx.Reload(), 0.4f, 2.2f);
            return true;
        }

        float FreeX()
        {
            for (int tries = 0; tries < 20; tries++)
            {
                float x = Random.Range(Look.ShelfLeft + 0.8f, Look.ShelfRight - 0.8f);
                bool clear = true;
                foreach (var c in Cans) if (c != null && !c.Airborne && Mathf.Abs(c.transform.position.x - x) < 0.9f) { clear = false; break; }
                if (clear) return x;
            }
            return Random.Range(Look.ShelfLeft + 0.8f, Look.ShelfRight - 0.8f);
        }

        /// <summary>한 발에 쌓이는 돈: 값 × 값배수 × (연속 × 연속배수)</summary>
        public int EarnFor(CanKind kind, int combo, float scale) => Mathf.Max(1, Mathf.RoundToInt(kind.Value * ValueMult * combo * ComboMult * scale));

        public void CanHit(Can can, int earned, Vector2 at)
        {
            hud.Float($"+{earned}", at, can.Combo);
            if (can.Combo > BestCombo) BestCombo = can.Combo;
            Gun.OnComboHit(can.Combo);
            StartCoroutine(HitStop(can.Combo >= 5 ? 0.04f : 0.02f));
        }

        /// <summary>정산: 쌓인 돈 × 정산 배수</summary>
        public void CanLanded(Can can)
        {
            long pay = (long)Mathf.Round(can.Earned * PayoutMult);
            Money += pay;
            if (pay > BestPayout) BestPayout = pay;
            hud.Payout(pay, can.Combo, can.transform.position);
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
