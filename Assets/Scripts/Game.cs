using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinCan
{
    /// <summary>
    /// 핵심 루프: 돈을 내고 깡통을 소환한다(선반) → 쏴서 띄운다 → 공중에서 계속 맞춘다 → 낙하지역에 떨어지면 정산.
    /// 상점은 Tab으로 따로 연다.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }
        public long Money { get; private set; }
        public int BestCombo { get; private set; }
        public long BestPayout { get; private set; }
        public int MaxCans { get; set; } = 3;         // 선반에 동시에 둘 수 있는 수
        public Gun Gun { get; private set; }
        public List<Can> Cans { get; } = new List<Can>();
        public List<Upgrade> Upgrades { get; private set; }
        public bool ShopOpen { get; private set; }
        public CanKind SpawnKind { get; set; } = CanKind.Tin;
        Hud hud;
        Shop shop;

        void Awake() { I = this; Can.BounceBonus = 0f; }

        void Start()
        {
            Look.BuildRange();
            Money = 10;
            Gun = new GameObject("Gun").AddComponent<Gun>();
            Gun.Build();
            Upgrades = TinCan.Upgrades.Make();
            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.Bind(this);
            shop = new GameObject("Shop").AddComponent<Shop>();
            shop.Build(this, hud.Canvas);
            shop.SetOpen(false);
        }

        void Update()
        {
            Cans.RemoveAll(c => c == null);
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.tabKey.wasPressedThisFrame) { ShopOpen = !ShopOpen; shop.SetOpen(ShopOpen); Gun.Enabled = !ShopOpen; }
            if (!ShopOpen && kb.spaceKey.wasPressedThisFrame) TrySpawn();
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

        public void CanHit(Can can, int earned, Vector2 at)
        {
            hud.Float($"+{earned}", at, can.Combo);
            if (can.Combo > BestCombo) BestCombo = can.Combo;
            StartCoroutine(HitStop(can.Combo >= 5 ? 0.04f : 0.02f));
        }

        /// <summary>정산: 쌓인 돈이 들어온다</summary>
        public void CanLanded(Can can)
        {
            Money += can.Earned;
            if (can.Earned > BestPayout) BestPayout = can.Earned;
            hud.Payout(can.Earned, can.Combo, can.transform.position);
        }

        public void Buy(Upgrade u)
        {
            if (u.Maxed || Money < u.NextCost) { Sfx.Play(Sfx.Click(), 0.5f, 0.8f); return; }
            Money -= u.NextCost;
            u.Level++;
            u.Apply(this, u.Level);
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
