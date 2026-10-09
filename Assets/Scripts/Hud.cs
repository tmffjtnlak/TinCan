using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>돈, 실린더(6발 점), 떠오르는 +돈 글자. 전부 코드로 만든 UGUI.</summary>
    public class Hud : MonoBehaviour
    {
        Game game;
        Text money, best, hint;
        Image[] bullets;
        Font font;
        Transform canvas;

        public void Bind(Game g) => game = g;
        public Transform Canvas => canvas;

        void Awake()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var cg = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            var c = cg.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay;
            var s = cg.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920f, 1080f);
            canvas = cg.transform;
            money = Make("Money", new Vector2(-660f, 470f), new Vector2(500f, 70f), 48, TextAnchor.MiddleLeft, new Color(1f, 0.9f, 0.4f));
            best = Make("Best", new Vector2(-660f, 415f), new Vector2(500f, 40f), 24, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.7f));
            hint = Make("Hint", new Vector2(0f, 470f), new Vector2(1200f, 40f), 20, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.6f));
            hint.text = "";
            bullets = new Image[33];
            for (int i = 0; i < bullets.Length; i++)
            {
                var go = new GameObject($"Bullet{i}", typeof(RectTransform));
                var rt = (RectTransform)go.transform; rt.SetParent(canvas, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(380f + (i % 11) * 34f, -455f - (i / 11) * 26f); rt.sizeDelta = new Vector2(22f, 22f);
                bullets[i] = go.AddComponent<Image>(); bullets[i].raycastTarget = false;
            }
        }

        Text Make(string name, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform; rt.SetParent(canvas, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.alignment = anchor; t.color = color; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>정산: 큰 글자로 "+$N (M연속)". 터뜨린 거면 더 크고 "터짐!"</summary>
        public void Payout(long amount, int combo, Vector2 world, bool burst)
        {
            if (burst) Float($"터짐!  +${amount:N0}", world + Vector2.up * 0.6f, 10);
            else Float(combo >= 3 ? $"+${amount:N0}  {combo}연속" : $"+${amount:N0}", world + Vector2.up * 0.8f, Mathf.Max(combo, 4));
        }

        /// <summary>떠오르는 글자 (월드 위치에서). 콤보가 높을수록 크고 노랗다</summary>
        public void Float(string msg, Vector2 world, int combo)
        {
            var t = Make("Float", Vector2.zero, new Vector2(400f, 60f), 28 + Mathf.Min(combo, 10) * 3, TextAnchor.MiddleCenter,
                Color.Lerp(Color.white, new Color(1f, 0.85f, 0.2f), Mathf.Clamp01(combo / 8f)));
            t.text = msg;
            StartCoroutine(FloatCo(t, world));
        }

        IEnumerator FloatCo(Text t, Vector2 world)
        {
            var rt = (RectTransform)t.transform;
            float life = 0.7f, time = 0f;
            var cam = Camera.main;
            while (time < life)
            {
                time += Time.unscaledDeltaTime;
                float k = time / life;
                var sp = cam.WorldToScreenPoint(world + Vector2.up * (0.3f + k * 0.9f));
                rt.anchoredPosition = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f) * (1080f / Screen.height);
                var c = t.color; c.a = 1f - k * k; t.color = c;
                yield return null;
            }
            Destroy(t.gameObject);
        }

        void Update()
        {
            if (game == null) return;
            money.text = $"${game.Money:N0}";
            int air = game.Airborne;
            best.text = $"터뜨림 {game.Bursts}   ·   최고 연속 {game.BestCombo}   ·   최고 정산 ${game.BestPayout:N0}" + (air >= 2 ? $"      <color=#ffd966>공중 {air}개  ×{air}</color>" : "") + (gun.ReloadStreak >= 1 ? $"      <color=#88ffaa>저스트 장전 {gun.ReloadStreak}</color>" : "");
            best.supportRichText = true;
            var gun = game.Gun;
            hint.text = game.MenuOpen ? "" : $"{gun.Kind.Name}   ·   Space = {game.SpawnKind.Name} 소환 (${game.SpawnCost(game.SpawnKind)})   ·   좌클릭 = 쏜다   ·   R = 장전   ·   Tab = 가게      선반 {game.CansOnShelf}/{game.MaxCans}";
            for (int i = 0; i < bullets.Length; i++)
            {
                bullets[i].gameObject.SetActive(i < gun.Capacity);
                bool loaded = i < gun.Ammo;
                bullets[i].color = gun.Reloading ? new Color(1f, 0.8f, 0.3f, 0.5f + 0.5f * Mathf.PingPong(Time.unscaledTime * 6f, 1f))
                    : (loaded ? new Color(1f, 0.95f, 0.8f) : new Color(1f, 1f, 1f, 0.2f));
            }
        }
    }
}
