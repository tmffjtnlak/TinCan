using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 시작 화면 (사용자 10-13): 평범한 버튼 셋 + 술집 그림을 3띠(뒤 하늘·산 / 가운데 울타리·술집 / 앞 모래)로 잘라
    /// 마우스 위치 따라 살짝 어긋나는 시차 + 입구의 여닫이문. 시작을 누르면 문으로 줌인 → 문이 열리고 → 원형 페이드.
    /// 지금은 한 장을 가로 띠로 자른 시험판. 사용자가 레이어별 그림을 주면 교체.
    /// 아직 안 한 것: 아지랑이(지평선 일렁임), 빛내림(굵은 직선 빛줄기).
    /// </summary>
    public class Title : MonoBehaviour
    {
        const float Ppu = 27f;
        // 띠 경계 (원본 아래쪽부터 센 픽셀 행). 겹침 8줄: 세로 시차로 틈이 안 보이게
        static readonly int[] cutLo = { 196, 56, 0 }, cutHi = { 270, 204, 64 };   // 뒤, 가운데, 앞
        static readonly float[] ampX = { 2f, 4f, 8f }, ampY = { 1f, 2f, 3f };     // 마우스 끝까지 갔을 때 밀리는 도트 수
        readonly Transform[] layer = new Transform[3];
        readonly Vector2[] basePos = new Vector2[3];
        Vector2 look, lookV;        // -1..1 시차 입력 (용수철로 따라간다)
        Transform doorL, doorR;
        Vector3 doorWorld = new Vector3(0f, (135f - 165f) / Ppu, 0f);   // 입구 여닫이문 자리 (그림 x 229~251, 위에서 158~172행)
        bool frozen;                // 줌인 중엔 시차 멈춤

        class Btn { public Image Panel; public Text Label; public System.Action Act; public float S = 1f, SV, Rot, RotV; }
        readonly List<Btn> btns = new List<Btn>();
        Transform canvas;

        public static Title Make(Transform canvas)
        {
            var t = new GameObject("Title").AddComponent<Title>();
            t.canvas = canvas;
            t.BuildLayers();
            t.BuildDoor();
            t.BuildRays();
            return t;
        }

        // ---------- 그림 ----------
        void BuildLayers()
        {
            var src = GunView.LoadArt("bg_saloon"); if (src == null) return;
            var tex = src.texture;
            for (int i = 0; i < 3; i++)
            {
                int lo = cutLo[i], hi = cutHi[i];
                var rect = new Rect(0f, lo, tex.width, hi - lo);
                var sp = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
                var go = new GameObject($"Layer{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp; sr.sortingOrder = 10 + i * 2;             // 사격장 도형(-10~-3) 전부 덮는다. 뒤 10, 가운데 12, 앞 14
                sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous;
                sr.size = new Vector2(tex.width * 3f / Ppu, (hi - lo) / Ppu);   // 옆으로 밀려도 끝이 안 보이게 세 장
                basePos[i] = new Vector2(0f, ((lo + hi) * 0.5f - tex.height * 0.5f) / Ppu);
                go.transform.localPosition = basePos[i];
                layer[i] = go.transform;
                // 아지랑이: 지평선 띠(원본 아래에서 204~236행)가 행마다 한두 도트 흔들린다. 뒤 띠는 아래쪽, 가운데 띠는 위쪽 끝
                if (i < 2 && hazeShader != null)
                {
                    var m = new Material(hazeShader);
                    m.SetVector("_TexSize", new Vector4(tex.width, tex.height, 0f, 0f));
                    float bLo = i == 0 ? 204f / tex.height : 196f / tex.height, bHi = i == 0 ? 236f / tex.height : 216f / tex.height;
                    m.SetVector("_Band", new Vector4(bLo, bHi, 0f, 0f));
                    m.SetFloat("_Amp", 1.6f); m.SetFloat("_Rate", 10f);
                    sr.material = m;
                }
            }
        }
        static Shader hazeShader => Shader.Find("TinCan/Haze");

        // ---------- 빛내림: 오른쪽 위에서 사선으로 내려오는 굵은 직선 빛줄기 ----------
        // 480×270 도트 한 장에 직접 찍는다 (가장자리가 도트 계단, 회전 스프라이트의 매끈한 대각선이 아니라). 초당 12번 다시 그린다.
        const int RayW = 480, RayH = 270;
        Texture2D rayTex; Color32[] rayPx; SpriteRenderer raySr; float rayClock;
        static readonly float[] rayCx = { 150f, 215f, 262f, 330f, 395f, 450f, 520f }, rayWd = { 14f, 4f, 22f, 8f, 18f, 3f, 12f }, rayA = { 0.16f, 0.1f, 0.2f, 0.12f, 0.18f, 0.1f, 0.15f };
        const float RaySlope = 0.404f;    // tan 22°: 한 줄 내려갈 때 왼쪽으로 가는 도트 수 (위가 오른쪽, 아래가 왼쪽)

        void BuildRays()
        {
            rayTex = new Texture2D(RayW, RayH, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            rayPx = new Color32[RayW * RayH];
            var go = new GameObject("Rays"); go.transform.SetParent(transform, false);
            raySr = go.AddComponent<SpriteRenderer>(); raySr.sortingOrder = 20;
            raySr.sprite = Sprite.Create(rayTex, new Rect(0, 0, RayW, RayH), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            DrawRays(0f);
        }

        void TickRays(float t)
        {
            if (rayTex == null) return;
            if (t - rayClock < 1f / 12f) return;
            rayClock = t;
            DrawRays(t);
        }

        void DrawRays(float t)
        {
            int n = rayCx.Length;
            var cx = new float[n]; var al = new float[n];
            for (int i = 0; i < n; i++)
            {
                al[i] = rayA[i] * (0.6f + 0.4f * Mathf.Sin(t * (0.35f + 0.13f * i) + i * 1.7f));   // 천천히 밝아졌다 어두워진다
                cx[i] = rayCx[i] + 6f * Mathf.Sin(t * 0.21f + i * 0.85f);                            // 좌우로 조금 떠돈다
            }
            for (int y = 0; y < RayH; y++)
            {
                float fade = Mathf.Pow((float)y / (RayH - 1), 0.9f);      // 위가 진하고 땅에 닿기 전에 사라진다
                float dx = (y - RayH) * RaySlope;                          // 이 행에서 줄기 중심이 왼쪽으로 밀린 양
                int row = y * RayW;
                for (int x = 0; x < RayW; x++)
                {
                    float a = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float d = x - (cx[i] + dx);
                        if (d < 0f) d = -d;
                        if (d < rayWd[i] * 0.5f) a += al[i];
                    }
                    a *= fade;
                    rayPx[row + x] = new Color32(255, 245, 190, (byte)(255f * Mathf.Min(0.45f, a)));
                }
            }
            rayTex.SetPixels32(rayPx); rayTex.Apply(false);
        }

        /// <summary>여닫이문 22×14: 반으로 갈라 바깥쪽 경첩에 축을 두고 가운데 층에 붙인다</summary>
        void BuildDoor()
        {
            var src = GunView.LoadArt("icon_book"); if (src == null || layer[1] == null) return;
            var tex = src.texture; int w = tex.width, h = tex.height, half = w / 2;
            doorL = Half(Sprite.Create(tex, new Rect(0, 0, half, h), new Vector2(0f, 0.5f), Ppu, 0, SpriteMeshType.FullRect), doorWorld + Vector3.left * (half / Ppu), "DoorL");
            doorR = Half(Sprite.Create(tex, new Rect(half, 0, w - half, h), new Vector2(1f, 0.5f), Ppu, 0, SpriteMeshType.FullRect), doorWorld + Vector3.right * (half / Ppu), "DoorR");
        }

        Transform Half(Sprite sp, Vector3 at, string name)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.sortingOrder = 13;   // 가운데 층(12) 위, 앞 층(14) 아래
            go.transform.position = at;
            go.transform.SetParent(layer[1], true);
            return go.transform;
        }

        // ---------- 버튼 ----------
        public void AddButton(string label, Vector2 pos, System.Action act)
        {
            var b = new Btn { Act = act };
            b.Panel = Ui.Panel("TitleBtn", canvas, pos, new Vector2(300f, 72f), nails: false);
            b.Label = Ui.Text("Label", b.Panel.transform, Vector2.zero, new Vector2(280f, 50f), 24, TextAnchor.MiddleCenter, Ui.Gold);
            b.Label.text = label;
            btns.Add(b);
        }

        public void SetButtonsVisible(bool v) { foreach (var b in btns) if (b.Panel != null) b.Panel.gameObject.SetActive(v); }
        public void ClearButtons() { foreach (var b in btns) if (b.Panel != null) Destroy(b.Panel.gameObject); btns.Clear(); }

        void Update()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current; if (mouse == null) return;
            var mp = mouse.position.ReadValue();
            float dt = Time.unscaledDeltaTime;
            TickRays(Time.unscaledTime);
            // 시차: 마우스 -1..1 → 층마다 반대쪽으로 도트 몇 칸. 용수철로 따라가고 자리는 도트 칸에 맞춘다
            if (!frozen)
            {
                var target = new Vector2(Mathf.Clamp(mp.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(mp.y / Screen.height * 2f - 1f, -1f, 1f));
                Ease.Spring(ref look.x, ref lookV.x, target.x, 60f, 12f, dt);
                Ease.Spring(ref look.y, ref lookV.y, target.y, 60f, 12f, dt);
                for (int i = 0; i < 3; i++)
                {
                    if (layer[i] == null) continue;
                    float px = Mathf.Round(-look.x * ampX[i]), py = Mathf.Round(-look.y * ampY[i]);
                    layer[i].localPosition = basePos[i] + new Vector2(px, py) / Ppu;
                }
            }
            // 버튼: 올리면 살짝 커지고, 누르면 납작했다 튕기며 실행 (Hud.Buttons와 같은 문법)
            var pt = new Vector2(mp.x - Screen.width * 0.5f, mp.y - Screen.height * 0.5f) * (1080f / Screen.height);
            foreach (var b in btns)
            {
                if (b.Panel == null || !b.Panel.gameObject.activeSelf) continue;
                var rt = b.Panel.rectTransform;
                bool over = Mathf.Abs(pt.x - rt.anchoredPosition.x) < rt.sizeDelta.x * 0.5f && Mathf.Abs(pt.y - rt.anchoredPosition.y) < rt.sizeDelta.y * 0.5f;
                float target = over ? 1.08f : 1f;
                if (over && mouse.leftButton.wasPressedThisFrame)
                {
                    b.S = 0.9f; b.SV = 6f; b.Rot = Random.Range(-4f, 4f);
                    Sfx.Play(Sfx.Click(), 0.5f, 1.2f);
                    b.Act?.Invoke();
                }
                Ease.Spring(ref b.S, ref b.SV, target, 400f, 16f, dt);
                Ease.Spring(ref b.Rot, ref b.RotV, 0f, 300f, 14f, dt);
                rt.localScale = Vector3.one * b.S;
                rt.localRotation = Quaternion.Euler(0f, 0f, b.Rot);
                b.Label.color = over ? Color.white : Ui.Gold;
            }
        }

        /// <summary>시작: 카메라가 문으로 줌인(0.9초) → 문이 양쪽으로 열림(0.3초) → 원형 페이드 닫힘. 끝나면 onDone</summary>
        public IEnumerator Enter(System.Action onDone)
        {
            frozen = true;
            SetButtonsVisible(false);
            var cam = Camera.main;
            Vector3 from = cam.transform.position, to = new Vector3(doorWorld.x, doorWorld.y, from.z);
            if (layer[1] != null) to += (Vector3)(Vector2)layer[1].localPosition - (Vector3)basePos[1];   // 시차로 밀린 만큼
            float size0 = cam.orthographicSize, size1 = 0.7f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.9f)
            {
                float k = t * t * (3f - 2f * t);                                       // 천천히 출발해 천천히 닿는다
                k = Mathf.Pow(k, 1.6f);                                                // 끝으로 갈수록 빨라지는 느낌 (줌인)
                cam.transform.position = Vector3.Lerp(from, to, k);
                cam.orthographicSize = Mathf.Lerp(size0, size1, k);
                yield return null;
            }
            cam.transform.position = to; cam.orthographicSize = size1;
            Sfx.Play(Sfx.Click(), 0.6f, 0.7f);
            // 문: 경첩 쪽으로 납작해지며 열린다 (정면에서 본 여닫이)
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
            {
                float sx = 1f - Ease.OutQuad(t);
                if (doorL != null) doorL.localScale = new Vector3(Mathf.Max(0.02f, sx), 1f, 1f);
                if (doorR != null) doorR.localScale = new Vector3(Mathf.Max(0.02f, sx), 1f, 1f);
                yield return null;
            }
            yield return Iris.Get().Close(0.45f);
            onDone?.Invoke();
        }
    }
}
