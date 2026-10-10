using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 오른쪽 가장자리, 세로로 쌓인 총알 (사용자 도트). 현재 총의 탄. 총 가족마다 탄 그림이 다르다 (리볼버 노랑, 샷건 빨강).
    /// 쏘면: 그 탄이 검게 물들고 옆으로 살짝 밀린다.
    /// 장전 시작: 쓴 탄들이 튕겨 날아간다.  장전 끝: 새 탄이 흰색에서 커지며 등장한다 (트윈).
    /// </summary>
    public class AmmoHud : MonoBehaviour
    {
        const float Scale = 3f;
        const float Gap = 6f;
        const float SpentShift = 14f;
        static readonly Vector2 Anchor = new Vector2(880f, 0f);
        static readonly Color Spent = new Color(0.12f, 0.1f, 0.1f, 1f);

        class Slot { public Image Img, White; public RectTransform Rt; public Vector2 Home; public bool Full; }
        Slot[] slots = new Slot[0];
        Transform canvas;
        Gun gun;
        GunKind shownKind;
        int shownAmmo = -1;
        bool wasReloading;

        public void Build(Transform canvasRoot, Gun g) { canvas = canvasRoot; gun = g; }

        static Sprite Load(string name)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/{name}.png");
#else
            return Resources.Load<Sprite>("Art/" + name);
#endif
        }

        static readonly System.Collections.Generic.Dictionary<Sprite, Sprite> whites = new System.Collections.Generic.Dictionary<Sprite, Sprite>();
        /// <summary>그림의 흰 실루엣 (등장 때 겹친다). 한 번 만들어 둔다</summary>
        static Sprite WhiteOf(Sprite s)
        {
            if (s == null) return null;
            if (whites.TryGetValue(s, out var w)) return w;
            try
            {
                var src = s.texture; var r = s.textureRect;
                var tex = new Texture2D((int)r.width, (int)r.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var px = src.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
                for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 1f, 1f, px[i].a);
                tex.SetPixels(px); tex.Apply();
                w = Sprite.Create(tex, new Rect(0, 0, r.width, r.height), new Vector2(0.5f, 0.5f), s.pixelsPerUnit);
            }
            catch { w = null; }   // 읽기 불가면 흰 실루엣 없이 (가져오기 설정에서 Read/Write 켜야 한다)
            whites[s] = w;
            return w;
        }

        static Sprite SpriteFor(GunKind k)
        {
            string name = k.Kin == GunKind.Family.Shotgun ? "bullet_shotgun" : "bullet_revolver";
            return Load(name);
        }

        void Rebuild(GunKind kind)
        {
            foreach (var s in slots) if (s != null && s.Img != null) Destroy(s.Img.gameObject);
            int cap = Mathf.Min(gun.Capacity, 30);
            var sp = SpriteFor(kind); var wh = WhiteOf(sp);
            slots = new Slot[cap];
            float w = (sp != null ? sp.rect.width : 25f) * Scale, h = (sp != null ? sp.rect.height : 11f) * Scale;
            float total = cap * h + (cap - 1) * Gap;
            for (int i = 0; i < cap; i++)
            {
                var home = Anchor + new Vector2(0f, total * 0.5f - h * 0.5f - i * (h + Gap));
                var img = Ui.Image($"Bullet{i}", canvas, home, new Vector2(w, h), Color.white);
                img.sprite = sp; img.preserveAspect = true;
                var white = Ui.Image("White", img.transform, Vector2.zero, new Vector2(w, h), Color.clear);
                white.sprite = wh; white.preserveAspect = true;
                slots[i] = new Slot { Img = img, White = white, Rt = img.rectTransform, Home = home, Full = true };
            }
            shownKind = kind; shownAmmo = -1;
        }

        void LateUpdate()
        {
            if (gun == null || gun.Kind == null || Game.I == null || Game.I.State != GameState.Play) { foreach (var s in slots) if (s != null) s.Img.gameObject.SetActive(false); return; }
            if (gun.Kind != shownKind || slots.Length != Mathf.Min(gun.Capacity, 30)) Rebuild(gun.Kind);
            foreach (var s in slots) s.Img.gameObject.SetActive(true);
            int ammo = gun.Ammo;
            if (ammo != shownAmmo)
            {
                bool first = shownAmmo < 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    bool isFull = i < ammo;
                    if (isFull == slots[i].Full && !first) continue;
                    if (isFull) { if (first) SetFull(slots[i]); else StartCoroutine(Appear(slots[i], i - Mathf.Max(0, shownAmmo))); }
                    else StartCoroutine(Spend(slots[i]));
                    slots[i].Full = isFull;
                }
                shownAmmo = ammo;
            }
            if (gun.Reloading != wasReloading)
            {
                wasReloading = gun.Reloading;
                if (wasReloading) for (int i = ammo; i < slots.Length; i++) StartCoroutine(Toss(slots[i]));
            }
        }

        void SetFull(Slot s) { s.Img.color = Color.white; s.Rt.anchoredPosition = s.Home; s.Rt.localScale = Vector3.one; s.Rt.localRotation = Quaternion.identity; s.White.color = Color.clear; }

        /// <summary>쐈다: 검게 물들며 옆으로 밀린다</summary>
        IEnumerator Spend(Slot s)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 10f)
            {
                s.Img.color = Color.Lerp(Color.white, Spent, t);
                s.Rt.anchoredPosition = s.Home + new Vector2(SpentShift * Mathf.SmoothStep(0f, 1f, t), 0f);
                yield return null;
            }
            s.Img.color = Spent; s.Rt.anchoredPosition = s.Home + new Vector2(SpentShift, 0f);
        }

        /// <summary>장전 시작: 쓴 탄이 튕겨 날아간다</summary>
        IEnumerator Toss(Slot s)
        {
            var from = s.Rt.anchoredPosition;
            var vel = new Vector2(Random.Range(60f, 140f), Random.Range(220f, 320f));
            float spin = Random.Range(-540f, 540f);
            var c = s.Img.color;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 2.4f)
            {
                s.Rt.anchoredPosition = from + vel * t + new Vector2(0f, -900f * t * t);
                s.Rt.localRotation = Quaternion.Euler(0f, 0f, spin * t);
                s.Img.color = new Color(c.r, c.g, c.b, 1f - t * t);
                yield return null;
            }
            s.Img.color = Color.clear; s.Rt.anchoredPosition = s.Home; s.Rt.localRotation = Quaternion.identity;
        }

        /// <summary>장전 끝: 흰색에서 커지며 등장한다 (0 → 1.3 → 1), 흰 실루엣이 사라지며 본색</summary>
        IEnumerator Appear(Slot s, int order)
        {
            s.Rt.anchoredPosition = s.Home; s.Rt.localRotation = Quaternion.identity;
            s.Img.color = Color.white; s.Rt.localScale = Vector3.zero; s.White.color = Color.white;
            float delay = Mathf.Max(0, order) * 0.04f; while (delay > 0f) { delay -= Time.unscaledDeltaTime; yield return null; }
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 7f)
            {
                float k = t < 0.6f ? Mathf.Lerp(0f, 1.3f, t / 0.6f) : Mathf.Lerp(1.3f, 1f, (t - 0.6f) / 0.4f);
                s.Rt.localScale = Vector3.one * k;
                s.White.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - t * 1.6f));
                yield return null;
            }
            s.Rt.localScale = Vector3.one; s.White.color = Color.clear;
        }
    }
}
