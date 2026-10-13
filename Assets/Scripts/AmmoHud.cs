using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 오른쪽 가장자리, 세로로 쌓인 총알 (사용자 도트). 현재 총의 탄. 총 가족마다 탄 그림이 다르다 (리볼버 노랑, 샷건 빨강).
    /// 움직임은 전부 용수철 (Click the Button 문법: 들어갈 땐 즉시, 나올 땐 튕기며):
    ///   쏘면 = 1프레임에 옆으로 밀리며 납작 → 튕겨 돌아옴, 검게 물듦
    ///   장전 시작 = 쓴 탄들이 튕겨 날아간다
    ///   장전 끝 = 새 탄이 작고 기울어진 채 흰색으로 떴다가 용수철로 커지며 제 색 (아래부터 차례로)
    /// 칸마다 연출은 하나만 돈다 (10-12 버그: 날아가는 연출이 등장 연출 뒤에 끝나며 탄을 지웠다).
    /// </summary>
    public class AmmoHud : MonoBehaviour
    {
        const float Scale = Ui.Px;           // 도트 규칙: 그림 1픽셀 = 화면 3픽셀. HUD 탄도 같은 칸
        const float Gap = Ui.Px * 2f;
        const float SpentShift = Ui.Px * 5f;
        static readonly Vector2 Anchor = new Vector2(860f, 0f);
        static readonly Color Spent = new Color(200f / 255f, 200f / 255f, 200f / 255f, 1f);

        class Slot
        {
            public Image Img, White; public RectTransform Rt; public Vector2 Home; public bool Full;
            public Coroutine Anim;                            // 칸마다 하나
            public float Sx = 1f, Sy = 1f, Vx, Vy, Rot, RotV; // 용수철
            public float Shift, ShiftV;                       // 옆으로 밀림 (쏜 탄)
            public bool Flying;                               // 날아가는 중엔 용수철이 손대지 않는다
        }
        Slot[] slots = new Slot[0];
        Transform canvas;
        Gun gun;
        GunKind shownKind;
        int shownAmmo = -1;
        bool wasReloading;

        /// <summary>
        /// 탄은 자기 캔버스에 둔다: 우클릭 레터박스 중 HUD 캔버스가 투명도 0이 되어도 탄만 남게 (사용자 10-13).
        /// (HUD 캔버스 안의 묶음으로는 안 된다: 캔버스 투명도가 0이면 유니티가 캔버스를 통째로 건너뛴다.)
        /// 크기·배율 설정은 HUD 캔버스와 같다 (1920×1080 기준, 픽셀 고정). HUD 위에 그린다.
        /// </summary>
        public void Build(Transform hudCanvas, Gun g)
        {
            var go = new GameObject("AmmoCanvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.pixelPerfect = true; c.sortingOrder = 1;
            var s = go.GetComponent<UnityEngine.UI.CanvasScaler>(); s.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920f, 1080f);
            canvas = go.transform; gun = g;
        }

        static Sprite Load(string name)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/{name}.png");
#else
            return Resources.Load<Sprite>("Art/" + name);
#endif
        }

        static readonly System.Collections.Generic.Dictionary<Sprite, Sprite> whites = new System.Collections.Generic.Dictionary<Sprite, Sprite>();
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
            catch { w = null; }
            whites[s] = w;
            return w;
        }

        static Sprite SpriteFor(GunKind k) => Load(k.Kin == GunKind.Family.Shotgun ? "bullet_shotgun" : "bullet_revolver");

        // 10-14 통일: 쏜 칸은 회색 총알이 아니라 탄피 그림 (탄두가 빠졌다). 장전 때 날아가는 것도 탄피
        Sprite bulletSp, bulletWh, casingSp, casingWh;
        Vector2 bulletSize, casingSize;
        void SetArt(Slot s, bool casing)
        {
            bool c = casing && casingSp != null;
            s.Img.sprite = c ? casingSp : bulletSp; s.White.sprite = c ? casingWh : bulletWh;
            s.Rt.sizeDelta = c ? casingSize : bulletSize; s.White.rectTransform.sizeDelta = s.Rt.sizeDelta;
        }

        void Rebuild(GunKind kind)
        {
            StopAllCoroutines();
            foreach (var s in slots) if (s != null && s.Img != null) Destroy(s.Img.gameObject);
            int cap = Mathf.Min(gun.Capacity, 30);
            var sp = SpriteFor(kind); var wh = WhiteOf(sp);
            bulletSp = sp; bulletWh = wh;
            casingSp = Load("casing"); casingWh = WhiteOf(casingSp);
            bulletSize = sp != null ? new Vector2(sp.rect.width, sp.rect.height) * Scale : new Vector2(25f, 11f) * Scale;
            casingSize = casingSp != null ? new Vector2(casingSp.rect.width, casingSp.rect.height) * Scale : bulletSize;
            slots = new Slot[cap];
            float w = (sp != null ? sp.rect.width : 25f) * Scale, h = (sp != null ? sp.rect.height : 11f) * Scale;
            float pitch = Mathf.Max(h, casingSize.y);   // 줄 간격은 탄피(조금 더 높다) 기준 → 탄피끼리 겹치지 않게
            float total = cap * pitch + (cap - 1) * Gap;
            for (int i = 0; i < cap; i++)
            {
                var home = Anchor + new Vector2(0f, total * 0.5f - pitch * 0.5f - i * (pitch + Gap));
                var img = Ui.Image($"Bullet{i}", canvas, home, new Vector2(w, h), Color.white);
                img.sprite = sp; img.preserveAspect = true;
                var white = Ui.Image("White", img.transform, Vector2.zero, new Vector2(w, h), Color.clear);
                white.sprite = wh; white.preserveAspect = true;
                slots[i] = new Slot { Img = img, White = white, Rt = img.rectTransform, Home = home, Full = true };
            }
            shownKind = kind; shownAmmo = -1;
        }

        /// <summary>칸의 연출을 바꾼다: 돌던 건 멈추고 새 것 하나만</summary>
        void Play(Slot s, IEnumerator co)
        {
            if (s.Anim != null) StopCoroutine(s.Anim);
            s.Flying = false;
            s.Anim = StartCoroutine(co);
        }

        void LateUpdate()
        {
            // 플레이 중에만, 가게(트리)가 열려 있으면 숨긴다 (사용자 10-12)
            if (gun == null || gun.Kind == null || Game.I == null || Game.I.State != GameState.Play || Game.I.MenuOpen) { foreach (var s in slots) if (s != null) s.Img.gameObject.SetActive(false); return; }
            if (gun.Kind != shownKind || slots.Length != Mathf.Min(gun.Capacity, 30)) Rebuild(gun.Kind);
            foreach (var s in slots) s.Img.gameObject.SetActive(true);
            int ammo = gun.Ammo;
            if (ammo != shownAmmo)
            {
                bool first = shownAmmo < 0;
                int order = 0;
                for (int i = slots.Length - 1; i >= 0; i--)   // 아래 칸부터 차례로 (장전 때 아래에서 위로 차오른다)
                {
                    bool isFull = i < ammo;
                    if (isFull == slots[i].Full && !first) continue;
                    if (isFull) { if (first) SetFull(slots[i]); else Play(slots[i], Appear(slots[i], order++)); }
                    else Play(slots[i], Spend(slots[i]));
                    slots[i].Full = isFull;
                }
                shownAmmo = ammo;
            }
            if (gun.Reloading != wasReloading)
            {
                wasReloading = gun.Reloading;
                if (wasReloading) for (int i = ammo; i < slots.Length; i++) Play(slots[i], Toss(slots[i], i - ammo));   // 위 칸부터 차례로
            }
            // 용수철: 크기·회전·옆 밀림. 날아가는 칸은 제외
            float dt = Time.unscaledDeltaTime;
            foreach (var s in slots)
            {
                if (s.Flying) continue;
                Ease.Spring(ref s.Sx, ref s.Vx, 1f, 500f, 15f, dt);
                Ease.Spring(ref s.Sy, ref s.Vy, 1f, 500f, 15f, dt);
                Ease.Spring(ref s.Rot, ref s.RotV, 0f, 380f, 10f, dt);
                Ease.Spring(ref s.Shift, ref s.ShiftV, s.Full ? 0f : SpentShift, 600f, 18f, dt);
                s.Rt.localScale = new Vector3(s.Sx, s.Sy, 1f);
                s.Rt.localRotation = Quaternion.Euler(0f, 0f, s.Rot);
                s.Rt.anchoredPosition = s.Home + new Vector2(s.Shift, 0f);
            }
        }

        void SetFull(Slot s) { SetArt(s, false); s.Img.color = Color.white; s.Sx = s.Sy = 1f; s.Vx = s.Vy = 0f; s.Rot = s.RotV = 0f; s.Shift = 0f; s.White.color = Color.clear; }

        /// <summary>쐈다: 1프레임에 옆으로 밀리며 납작 (가로 1.3, 세로 0.7) → 튕겨 돌아오고, 검게 물든다</summary>
        IEnumerator Spend(Slot s)
        {
            s.White.color = Color.clear;                          // 등장 중(흰 실루엣)에 쏘면 흰색이 남던 버그 (사용자 10-12)
            s.Shift = SpentShift * 1.6f; s.ShiftV = 0f;          // 즉시, 넘치게
            s.Sx = 1.3f; s.Sy = 0.7f; s.Vx = s.Vy = 0f;
            s.Rot = Random.Range(-6f, 6f); s.RotV = 0f;
            // 10-14: 색이 서서히 바뀌지 않고 바로. 흰 번쩍임은 뺐다 (사용자: HUD가 너무 반짝인다)
            // 10-14 통일: 탄두가 빠져 탄피만 남는다 (그림 교체) + 살짝 어둡게
            SetArt(s, true);
            s.White.color = Color.clear;
            s.Img.color = Spent;
            yield break;
        }

        static float Snap4(float v) => Mathf.Round(v / Scale) * Scale;

        /// <summary>
        /// 장전 시작 (10-14 통일, 리볼버 탄피 털기): 탄피 기둥이 다 같이 2도트 톡 들렸다(납작) →
        /// 위 칸부터 0.03초씩 하나씩, 오른쪽으로 살짝 밀리며 아래로 떨어져 화면 밖으로. 회전은 90°쯤만 기운다.
        /// 위로 솟구치던 것·여섯 개 동시·계속 빙빙 돌던 것을 바꿨다 (총에서 튀는 탄피·바닥 탄피·깡통처럼 아래로)
        /// </summary>
        IEnumerator Toss(Slot s, int order)
        {
            s.Flying = true; s.White.color = Color.clear;
            SetArt(s, true);
            var from = s.Home + new Vector2(s.Shift, 0f);
            // 1) 톡 들림: 0.1초 동안 2도트 위로 올랐다 내려오며 납작 → 용수철 느낌
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.1f)
            {
                float k = Mathf.Sin(t * Mathf.PI);
                s.Rt.anchoredPosition = from + new Vector2(0f, Snap4(Scale * 2f * k));
                s.Rt.localScale = new Vector3(1f + 0.15f * k, 1f - 0.15f * k, 1f);
                s.Rt.localRotation = Quaternion.identity;
                yield return null;
            }
            s.Rt.localScale = Vector3.one; s.Rt.anchoredPosition = from;
            // 2) 차례 기다림 (위 칸부터)
            float wait = order * 0.03f; while (wait > 0f) { wait -= Time.unscaledDeltaTime; yield return null; }
            // 3) 떨어짐: 오른쪽으로 살짝, 거의 바로 아래로. 90°쯤까지만 기운다. 화면 아래(-540) 밖으로 나가면 끝
            var vel = new Vector2(Random.Range(60f, 160f), Random.Range(40f, 120f));
            float tilt = (Random.value < 0.5f ? -1f : 1f) * Random.Range(70f, 100f);
            var pos = from;
            for (float t = 0f; t < 1.5f && pos.y > -620f; t += Time.unscaledDeltaTime)
            {
                float dt = Time.unscaledDeltaTime;
                vel.y -= 2600f * dt; pos += vel * dt;
                s.Rt.anchoredPosition = new Vector2(Snap4(pos.x), Snap4(pos.y));
                s.Rt.localRotation = Quaternion.Euler(0f, 0f, tilt * Ease.OutQuad(Mathf.Clamp01(t / 0.35f)));
                yield return null;
            }
            s.Img.color = Color.clear; s.Flying = false;
            s.Rt.localScale = Vector3.one; s.Rt.localRotation = Quaternion.identity;
            s.Sx = s.Sy = 1f; s.Rot = 0f; s.Shift = 0f;
        }

        /// <summary>장전 끝: 작고 기울어진 채 흰색으로 떴다가 용수철로 커지며(넘쳤다 자리) 제 색으로. 아래 칸부터 0.035초씩</summary>
        IEnumerator Appear(Slot s, int order)
        {
            s.Flying = false;
            s.Img.color = Color.clear; s.White.color = Color.clear;
            float delay = order * 0.035f; while (delay > 0f) { delay -= Time.unscaledDeltaTime; yield return null; }
            SetArt(s, false);                                          // 새 탄은 다시 총알 그림
            s.Img.color = Color.white; s.White.color = Color.white;
            s.Shift = 0f; s.ShiftV = 0f;
            s.Sx = 0.3f; s.Sy = 0.3f; s.Vx = 9f; s.Vy = 9f;           // 작게 시작해 튀어 오른다
            s.Rot = (order % 2 == 0 ? -1f : 1f) * Random.Range(16f, 24f); s.RotV = 0f;
            yield return null;                                        // 흰 실루엣 1프레임 (10-14: 2프레임은 너무 반짝였다)
            s.White.color = Color.clear;                              // 10-14 통일: 서서히 빼지 않고 딱 제 색 (중간 파스텔이 생겼다)
        }
    }
}
