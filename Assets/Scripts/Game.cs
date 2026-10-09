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
        Hud hud;

        void Awake() { I = this; }

        void Start()
        {
            Look.BuildSaloon();
            Gun = new GameObject("Gun").AddComponent<Gun>();
            Gun.Build();
            // 울타리 위 깡통 셋
            float railTop = -2.3f + 0.11f;
            Cans.Add(Can.Spawn(CanKind.Tin, new Vector2(3.0f, railTop + 0.35f)));
            Cans.Add(Can.Spawn(CanKind.Tin, new Vector2(4.6f, railTop + 0.35f)));
            Cans.Add(Can.Spawn(CanKind.Bottle, new Vector2(6.2f, railTop + 0.45f)));
            hud = new GameObject("Hud").AddComponent<Hud>();
            hud.Bind(this);
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
