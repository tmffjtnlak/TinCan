using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>돈과 콤보. 깡통이 공중에서 맞을 때마다 값 × 콤보가 들어온다. 업그레이드는 다음.</summary>
    public class Game : MonoBehaviour
    {
        public static Game I { get; private set; }
        public long Money { get; private set; }
        public int BestCombo { get; private set; }
        public int LastCombo { get; private set; }
        public Gun Gun { get; private set; }
        public List<Can> Cans { get; } = new List<Can>();
        public List<Upgrade> Upgrades { get; private set; }
        Hud hud;
        Shop shop;

        // 울타리 위 자리들 (왼쪽부터). 깡통 수가 늘면 이 순서로 채운다
        static readonly Vector2[] Slots = { new Vector2(3.0f, -1.84f), new Vector2(4.6f, -1.84f), new Vector2(6.2f, -1.74f), new Vector2(3.8f, -1.84f), new Vector2(5.4f, -1.84f), new Vector2(7.0f, -1.84f) };
        static readonly CanKind[] SlotKinds = { CanKind.Tin, CanKind.Tin, CanKind.Bottle, CanKind.Tin, CanKind.Bottle, CanKind.Tin };

        /// <summary>깡통 수를 맞춘다 (늘리기만)</summary>
        public void SetCanCount(int n)
        {
            n = Mathf.Min(n, Slots.Length);
            while (Cans.Count < n) Cans.Add(Can.Spawn(SlotKinds[Cans.Count], Slots[Cans.Count]));
        }

        public void Buy(Upgrade u)
        {
            if (u.Maxed || Money < u.NextCost) { Sfx.Play(Sfx.Click(), 0.5f, 0.8f); return; }
            Money -= u.NextCost;
            u.Level++;
            u.Apply(this, u.Level);
            Sfx.Play(Sfx.Reload(), 0.6f, 1.6f);
            hud.Float($"{u.Name} Lv{u.Level}", new Vector2(4.5f, 0.5f), 4);
        }

        void Awake() { I = this; Can.BounceBonus = 0f; }

        void Start()
        {
            Look.BuildSaloon();
            Gun = new GameObject("Gun").AddComponent<Gun>();
            Gun.Build();
            SetCanCount(3);
            Upgrades = TinCan.Upgrades.Make();
            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.Bind(this);
            shop = new GameObject("Shop").AddComponent<Shop>();
            shop.Build(this, hud.Canvas);
        }

        public void CanHit(Can can, int earned, Vector2 at)
        {
            if (earned > 0)
            {
                Money += earned;
                hud.Float($"+{earned}", at, can.Combo);
                if (can.Combo > BestCombo) BestCombo = can.Combo;
            }
            StartCoroutine(HitStop(can.Combo >= 5 ? 0.04f : 0.02f));
        }

        public void CanDropped(Can can)
        {
            LastCombo = can.Combo;
            if (can.Combo >= 3) hud.Float($"{can.Combo}연속!", can.transform.position + Vector3.up * 0.6f, can.Combo);
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
