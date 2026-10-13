using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 임시 실린더 HUD (사용자 10-14): 오른쪽 탄 기둥 대신 리볼버 실린더 하나. 탄 8발 이하 총만 (그 이상은 예전 기둥).
    /// 매 프레임 코드로 도트를 찍는다 → 돌아가도 늘 도트 (그림을 돌리지 않고, 약실 자리를 칸에 맞춰 다시 찍는다).
    /// 약실 셋 상태: 탄(놋쇠 + 뇌관) / 쏜 탄피(어두운 놋쇠 + 찍힌 뇌관) / 빈 구멍(남색).
    ///  - 쏘면: 맨 위 약실이 탄피가 되고, 실린더가 한 칸 딸깍 돈다 (용수철, 살짝 넘침)
    ///  - 장전 시작: 실린더가 왼쪽으로 열리며(밀림) 탄피가 아래로 쏟아지고 구멍이 빈다
    ///  - 장전 끝: 빈 약실이 하나씩 0.03초 간격으로 차고, 실린더가 제자리로 닫히며 한 바퀴 휙 돈다
    /// 그림이 오면 몸통·약실 그림으로 바꾼다 (자리·움직임은 그대로).
    /// </summary>
    public class CylinderHud : MonoBehaviour
    {
        const int R = 14;                  // 몸통 반지름 (도트)
        const int Size = R * 2 + 3;
        const float Px = Ui.Px;            // 화면 도트 = 1080p 4픽셀
        static readonly Vector2 Home = new Vector2(840f, -40f);
        enum Ch { Loaded, Spent, Empty }

        Gun gun;
        Transform canvas;
        RawImage img; Texture2D tex; Color32[] buf;
        Ch[] ch = new Ch[0];
        int n;
        float ang, angV, angTarget;        // 도 (반시계 +). 쏠 때마다 -step
        float open, openV;                 // 0 = 닫힘, 1 = 열림 (왼쪽으로 밀림)
        float popY, popYV;
        int lastAmmo = -1; bool wasReloading; GunKind kind;
        bool filling;

        static readonly Color32 Line = new Color32(38, 33, 64, 255);
        static readonly Color32 Steel = new Color32(140, 143, 153, 255), SteelDark = new Color32(104, 106, 118, 255), SteelLight = new Color32(196, 200, 210, 255);
        static readonly Color32 Brass = new Color32(250, 196, 70, 255), BrassRim = new Color32(190, 128, 40, 255), Primer = new Color32(230, 226, 210, 255);
        static readonly Color32 SpentBrass = new Color32(176, 126, 52, 255), SpentRim = new Color32(118, 78, 30, 255);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public static bool Active;   // AmmoHud가 기둥을 숨길지 본다

        public void Build(Gun g)
        {
            gun = g;
            var go = new GameObject("CylinderCanvas", typeof(Canvas), typeof(CanvasScaler));
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.pixelPerfect = true; c.sortingOrder = 1;
            var s = go.GetComponent<CanvasScaler>(); s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; s.referenceResolution = new Vector2(1920f, 1080f);
            canvas = go.transform;
            tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            buf = new Color32[Size * Size];
            var r = new GameObject("Cylinder", typeof(RectTransform)); r.transform.SetParent(canvas, false);
            var rt = (RectTransform)r.transform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = Vector2.one * Size * Px;
            img = r.AddComponent<RawImage>(); img.texture = tex; img.raycastTarget = false;
        }

        float Step => n > 0 ? 360f / n : 60f;
        static float Snap(float v) => Mathf.Round(v / Px) * Px;

        void LateUpdate()
        {
            bool play = gun != null && gun.Kind != null && Game.I != null && Game.I.State == GameState.Play && !Game.I.MenuOpen;
            Active = play && gun.Capacity <= 8;
            img.gameObject.SetActive(Active);
            if (!Active) { lastAmmo = -1; return; }
            float dt = Time.unscaledDeltaTime;
            // 총이 바뀌면 다시
            if (gun.Kind != kind || n != gun.Capacity)
            {
                kind = gun.Kind; n = gun.Capacity; ch = new Ch[n];
                for (int i = 0; i < n; i++) ch[i] = i < gun.Ammo ? Ch.Loaded : Ch.Spent;
                ang = angTarget = 0f; angV = 0f; lastAmmo = gun.Ammo; wasReloading = gun.Reloading;
            }
            int ammo = gun.Ammo;
            // 쏨: 탄이 줄어든 만큼 맨 위 약실이 탄피가 되고 한 칸씩 돈다
            if (!gun.Reloading && !filling && ammo < lastAmmo)
            {
                for (int k = 0; k < lastAmmo - ammo; k++)
                {
                    int top = TopChamber();
                    if (ch[top] == Ch.Loaded) ch[top] = Ch.Spent;
                    angTarget -= Step;
                }
                popY = Px * 2f; popYV = 0f;   // 반동: 2도트 위로 톡
            }
            // 장전 시작: 열리며 탄피 쏟기
            if (gun.Reloading && !wasReloading) StartCoroutine(Eject());
            // 장전 끝: 하나씩 채우고 닫으며 휙
            if (!gun.Reloading && wasReloading) StartCoroutine(Fill());
            wasReloading = gun.Reloading;
            lastAmmo = ammo;

            Ease.Spring(ref ang, ref angV, angTarget, 900f, 26f, dt);
            Ease.Spring(ref open, ref openV, gun.Reloading || filling ? 1f : 0f, 500f, 22f, dt);
            Ease.Spring(ref popY, ref popYV, 0f, 700f, 20f, dt);
            var pos = Home + new Vector2(-open * 10f * Px, popY);
            img.rectTransform.anchoredPosition = new Vector2(Snap(pos.x), Snap(pos.y));
            Draw(ang);
        }

        /// <summary>지금 맨 위(12시)에 가장 가까운 약실</summary>
        int TopChamber()
        {
            int best = 0; float bestD = 999f;
            for (int i = 0; i < n; i++) { float a = 90f + i * Step + angTarget; float d = Mathf.Abs(Mathf.DeltaAngle(a, 90f)); if (d < bestD) { bestD = d; best = i; } }
            return best;
        }

        IEnumerator Eject()
        {
            yield return new WaitForSecondsRealtime(0.08f);   // 열리는 동안
            for (int i = 0; i < n; i++)
                if (ch[i] == Ch.Spent)
                {
                    ch[i] = Ch.Empty;
                    var c = ChamberCenter(i, ang);
                    StartCoroutine(Drop(c));
                }
        }

        IEnumerator Fill()
        {
            filling = true;
            for (int i = 0; i < n; i++)
            {
                if (ch[i] == Ch.Loaded) continue;
                ch[i] = Ch.Loaded;
                popY = Px; popYV = 0f;
                yield return new WaitForSecondsRealtime(0.03f);
            }
            yield return new WaitForSecondsRealtime(0.05f);
            filling = false;
            // 닫히며 한 바퀴 휙 (맨 위 = 0번 약실로 맞춘다)
            float baseAng = Mathf.Floor(angTarget / 360f) * 360f;
            angTarget = baseAng - 360f; angV -= 900f;
        }

        /// <summary>약실 가운데 (텍스처 픽셀 좌표, 칸에 맞춤)</summary>
        Vector2Int ChamberCenter(int i, float a)
        {
            float rc = R - 6f;
            float t = (90f + i * Step + a) * Mathf.Deg2Rad;
            return new Vector2Int(Mathf.RoundToInt(R + 1 + Mathf.Cos(t) * rc), Mathf.RoundToInt(R + 1 + Mathf.Sin(t) * rc));
        }

        /// <summary>쏟아지는 탄피: 작은 놋쇠 도트 막대가 아래로 떨어져 화면 밖으로 (90°쯤 기움)</summary>
        IEnumerator Drop(Vector2Int texPos)
        {
            var go = new GameObject("Shell", typeof(RectTransform)); go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform; rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(2f, 4f) * Px;
            var im = go.AddComponent<Image>(); im.color = Brass; im.raycastTarget = false;
            var start = img.rectTransform.anchoredPosition + (new Vector2(texPos.x, texPos.y) - Vector2.one * (Size * 0.5f)) * Px;
            var pos = start; var vel = new Vector2(Random.Range(-60f, 40f), Random.Range(60f, 160f));
            float tilt = (Random.value < 0.5f ? -1f : 1f) * Random.Range(60f, 100f);
            for (float t = 0f; t < 1.5f && pos.y > -620f; t += Time.unscaledDeltaTime)
            {
                float dt = Time.unscaledDeltaTime;
                vel.y -= 2600f * dt; pos += vel * dt;
                rt.anchoredPosition = new Vector2(Snap(pos.x), Snap(pos.y));
                rt.localRotation = Quaternion.Euler(0f, 0f, tilt * Ease.OutQuad(Mathf.Clamp01(t / 0.3f)));
                yield return null;
            }
            Destroy(go);
        }

        void Draw(float a)
        {
            float cx = R + 1, cy = R + 1;
            // 몸통: 강철 원 + 남색 테 + 왼쪽 위 밝은 테두리, 약실 사이 홈(돌아감이 보이게)
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float dx = x - cx, dy = y - cy, d = Mathf.Sqrt(dx * dx + dy * dy);
                    Color32 c = Clear;
                    if (d <= R + 0.5f)
                    {
                        if (d > R - 0.5f) c = Line;
                        else
                        {
                            c = Steel;
                            float deg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                            if (d > R - 2.5f && deg > 95f && deg < 175f) c = SteelLight;
                            else if (d > R - 2.5f && deg < -20f && deg > -100f) c = SteelDark;
                        }
                    }
                    buf[y * Size + x] = c;
                }
            // 홈: 약실 사이 바깥 테에 남색 2칸
            for (int i = 0; i < n; i++)
            {
                float t = (90f + (i + 0.5f) * Step + a) * Mathf.Deg2Rad;
                for (int k = 0; k < 2; k++)
                {
                    int x = Mathf.RoundToInt(cx + Mathf.Cos(t) * (R - 1 - k)), y = Mathf.RoundToInt(cy + Mathf.Sin(t) * (R - 1 - k));
                    Put(x, y, Line);
                }
            }
            // 약실
            int rh = n > 6 ? 2 : 3;
            for (int i = 0; i < n; i++)
            {
                var c = ChamberCenter(i, a);
                for (int y = -rh - 1; y <= rh + 1; y++)
                    for (int x = -rh - 1; x <= rh + 1; x++)
                    {
                        float d = Mathf.Sqrt(x * x + y * y);
                        if (d > rh + 0.6f) continue;
                        Color32 col;
                        bool rim = d > rh - 0.4f;
                        switch (ch[i])
                        {
                            case Ch.Loaded: col = rim ? BrassRim : (x == 0 && y == 0 ? Primer : Brass); break;
                            case Ch.Spent: col = rim ? SpentRim : (x == 0 && y == 0 ? Line : SpentBrass); break;
                            default: col = Line; break;
                        }
                        Put(c.x + x, c.y + y, col);
                    }
            }
            // 가운데 축
            for (int y = -2; y <= 2; y++) for (int x = -2; x <= 2; x++) if (x * x + y * y <= 4) Put((int)cx + x, (int)cy + y, x * x + y * y <= 1 ? SteelLight : SteelDark);
            tex.SetPixels32(buf); tex.Apply(false);
        }

        void Put(int x, int y, Color32 c) { if (x >= 0 && y >= 0 && x < Size && y < Size) buf[y * Size + x] = c; }
    }
}
