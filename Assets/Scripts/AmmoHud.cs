using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 오른쪽 가장자리, 세로로 쌓인 총알 (사용자 10-11 도트). 현재 총의 탄.
    /// 쏘면 맨 위 탄이 빈 탄피로 바뀌며 살짝 튀고, 장전하면 탄피들이 오른쪽으로 빠져나가고 새 탄이 하나씩 들어온다.
    /// </summary>
    public class AmmoHud : MonoBehaviour
    {
        const float Scale = 3f;              // 도트 1px = 3 UI px
        const float Gap = 6f;
        static readonly Vector2 Anchor = new Vector2(880f, 0f);   // 오른쪽 가장자리, 세로 가운데

        Sprite full, empty;
        Image[] slots = new Image[0];
        RectTransform[] rts = new RectTransform[0];
        Transform canvas;
        Gun gun;
        int shownAmmo = -1, shownCap = -1;
        bool wasReloading;

        public void Build(Transform canvasRoot, Gun g)
        {
            canvas = canvasRoot; gun = g;
            full = Resources.Load<Sprite>("Art/bullet_full");
            if (full == null) full = LoadFromAssets("bullet_full");
            empty = LoadFromAssets("bullet_empty");
        }

        // Resources 밖(Assets/Art)에 둔 그림: 에디터에선 AssetDatabase, 빌드에선 Resources로 옮겨야 한다 (지금은 에디터 작업 단계)
        static Sprite LoadFromAssets(string name)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/{name}.png");
#else
            return Resources.Load<Sprite>("Art/" + name);
#endif
        }

        void Rebuild(int cap)
        {
            foreach (var s in slots) if (s != null) Destroy(s.gameObject);
            slots = new Image[cap]; rts = new RectTransform[cap];
            float h = (full != null ? full.rect.height : 12f) * Scale;
            float total = cap * h + (cap - 1) * Gap;
            for (int i = 0; i < cap; i++)
            {
                var img = Ui.Image($"Bullet{i}", canvas, Anchor + new Vector2(0f, total * 0.5f - h * 0.5f - i * (h + Gap)), new Vector2((full != null ? full.rect.width : 36f) * Scale, h), Color.white);
                img.sprite = full; img.preserveAspect = true;
                slots[i] = img; rts[i] = img.rectTransform;
            }
            shownCap = cap;
        }

        void LateUpdate()
        {
            if (gun == null || Game.I == null || Game.I.State != GameState.Play) { foreach (var s in slots) if (s != null) s.gameObject.SetActive(false); return; }
            int cap = Mathf.Min(gun.Capacity, 30);
            if (cap != shownCap) { Rebuild(cap); shownAmmo = -1; }
            foreach (var s in slots) s.gameObject.SetActive(true);
            int ammo = gun.Ammo;
            // 위에서부터 탄이 차 있다: i < ammo → 장전, 나머지 → 탄피
            if (ammo != shownAmmo)
            {
                bool fired = shownAmmo >= 0 && ammo < shownAmmo;
                bool loaded = shownAmmo >= 0 && ammo > shownAmmo;
                for (int i = 0; i < cap; i++)
                {
                    bool isFull = i < ammo;
                    slots[i].sprite = isFull ? full : empty;
                    slots[i].preserveAspect = true;
                    slots[i].color = isFull ? Color.white : new Color(1f, 1f, 1f, 0.55f);
                }
                if (fired && ammo < cap) StartCoroutine(Kick(rts[ammo]));          // 방금 비워진 자리
                if (loaded) for (int i = shownAmmo; i < ammo && i >= 0; i++) StartCoroutine(SlideIn(rts[i], i - shownAmmo));
                shownAmmo = ammo;
            }
            if (gun.Reloading != wasReloading)
            {
                wasReloading = gun.Reloading;
                if (wasReloading) for (int i = ammo; i < cap; i++) StartCoroutine(Eject(rts[i]));   // 탄피들이 빠져나간다
            }
        }

        IEnumerator Kick(RectTransform rt)
        {
            var home = rt.anchoredPosition;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 8f) { rt.anchoredPosition = home + new Vector2(Mathf.Sin(t * Mathf.PI) * 18f, 0f); rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI) * -12f); yield return null; }
            rt.anchoredPosition = home; rt.localRotation = Quaternion.identity;
        }

        IEnumerator Eject(RectTransform rt)
        {
            var home = rt.anchoredPosition;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 5f) { rt.anchoredPosition = home + new Vector2(t * 160f, -t * t * 60f); rt.localRotation = Quaternion.Euler(0f, 0f, t * 90f); yield return null; }
            rt.anchoredPosition = home; rt.localRotation = Quaternion.identity;
        }

        IEnumerator SlideIn(RectTransform rt, int order)
        {
            var home = rt.anchoredPosition;
            rt.anchoredPosition = home + new Vector2(140f, 0f);
            float delay = order * 0.03f; while (delay > 0f) { delay -= Time.unscaledDeltaTime; yield return null; }
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 9f) { rt.anchoredPosition = home + new Vector2((1f - t) * (1f - t) * 140f, 0f); yield return null; }
            rt.anchoredPosition = home;
        }
    }
}
