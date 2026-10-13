using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 시작 화면 (사용자 10-13). 2판 = Shelldiver 분석(Docs/Ref-Shelldiver-Title.md) 적용:
    /// 상자 없는 흰 글자 메뉴 + ◀, 먼 층 회색, 술집 오른쪽·왼쪽 하늘 비움, 로고 오른쪽 위, 움직임 적게,
    /// 클릭 자리에서 0.2초 딱딱한 원형, 조작 안내·버전·도트 커서, 메뉴 스태거.
    /// 그림은 술집 한 장을 3띠(뒤 하늘·산 / 가운데 울타리·술집 / 앞 모래)로 잘라 쓴다. 레이어별 그림이 오면 교체.
    /// </summary>
    public class Title : MonoBehaviour
    {
        const float Ppu = 27f;
        // 띠 경계 (원본 아래쪽부터 센 픽셀 행). 겹침 8줄: 세로 시차로 틈이 안 보이게
        static readonly int[] cutLo = { 196, 56, 0 }, cutHi = { 270, 204, 64 };   // 뒤, 가운데, 앞
        static readonly float[] ampX = { 1f, 2f, 4f }, ampY = { 0f, 1f, 2f };     // 시차: 분석대로 절반 (마우스 끝까지 갔을 때 도트 수)
        static readonly float[] shiftX = { 40f, 100f, 100f };                     // 술집을 오른쪽으로 — 왼쪽 하늘을 메뉴 자리로 비운다 (도트)
        readonly Transform[] layer = new Transform[3];
        readonly Vector2[] basePos = new Vector2[3];
        Vector2 look, lookV;
        Transform doorL, doorR;
        static readonly Vector3 doorArt = new Vector3(0f, (135f - 165f) / Ppu, 0f);   // 입구 여닫이문 자리 (그림 x 229~251, 위에서 158~172행)
        bool frozen;

        // ---------- 메뉴: 글자 + ◀ (상자 없음) ----------
        public static readonly Color Ink = new Color(0.97f, 0.97f, 0.97f), Line = new Color(0.15f, 0.13f, 0.25f);
        class Btn { public Text Label; public RectTransform Rect; public System.Action Act; public float X, Y; }
        readonly List<Btn> btns = new List<Btn>();
        Image pointer; int hovered = -1;
        Transform canvas;
        public const float MenuX = -860f, MenuTop = -30f, MenuStep = 72f;   // 왼쪽 3%, 울타리 아래 빈 모래, 줄 간격 18도트

        public static Title Make(Transform canvas)
        {
            var t = new GameObject("Title").AddComponent<Title>();
            t.canvas = canvas;
            t.BuildLayers();
            t.BuildDoor();
            t.BuildRays();
            t.BuildPointer();
            t.BuildFurniture();
            PixelCursor.Apply();
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
                var sp = Sprite.Create(tex, new Rect(0f, lo, tex.width, hi - lo), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
                var go = new GameObject($"Layer{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp; sr.sortingOrder = 10 + i * 2;             // 사격장 도형(-10~-3) 전부 덮는다
                sr.drawMode = SpriteDrawMode.Tiled; sr.tileMode = SpriteTileMode.Continuous;
                sr.size = new Vector2(tex.width * 3f / Ppu, (hi - lo) / Ppu);
                basePos[i] = new Vector2(shiftX[i] / Ppu, ((lo + hi) * 0.5f - tex.height * 0.5f) / Ppu);
                go.transform.localPosition = basePos[i];
                layer[i] = go.transform;
                // 세 띠 모두 같은 재질: 아지랑이(지평선 띠만, 앞 띠는 0) + 회색화(원본 행 기준 경사라 띠 경계에 줄이 없다)
                if (hazeShader != null)
                {
                    var m = new Material(hazeShader);
                    m.SetVector("_TexSize", new Vector4(tex.width, tex.height, 0f, 0f));
                    m.SetVector("_Band", new Vector4(204f / tex.height, 236f / tex.height, 0f, 0f));
                    m.SetFloat("_Amp", i == 2 ? 0f : 1.2f); m.SetFloat("_Rate", 6f);
                    m.SetFloat("_Desat", 0.6f);
                    m.SetVector("_DesatBand", new Vector4(150f / tex.height, 240f / tex.height, 0f, 0f));   // 울타리 위부터 하늘 쪽으로 점점 회색
                    m.SetColor("_Tint", new Color(0.84f, 0.9f, 0.97f));
                    sr.material = m;
                }
            }
        }
        static Shader hazeShader => Shader.Find("TinCan/Haze");

        void BuildDoor()
        {
            var src = GunView.LoadArt("icon_book"); if (src == null || layer[1] == null) return;
            var tex = src.texture; int w = tex.width, h = tex.height, half = w / 2;
            var at = doorArt + Vector3.right * basePos[1].x;   // 술집이 오른쪽으로 밀린 만큼
            doorL = Half(Sprite.Create(tex, new Rect(0, 0, half, h), new Vector2(0f, 0.5f), Ppu, 0, SpriteMeshType.FullRect), at + Vector3.left * (half / Ppu), "DoorL");
            doorR = Half(Sprite.Create(tex, new Rect(half, 0, w - half, h), new Vector2(1f, 0.5f), Ppu, 0, SpriteMeshType.FullRect), at + Vector3.right * (half / Ppu), "DoorR");
        }

        Transform Half(Sprite sp, Vector3 at, string name)
        {
            var go = new GameObject(name);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.sortingOrder = 13;
            go.transform.position = at;
            go.transform.SetParent(layer[1], true);
            return go.transform;
        }

        Vector2 DoorCenter => doorL != null && doorR != null ? (Vector2)(doorL.position + doorR.position) * 0.5f : (Vector2)(doorArt + (Vector3)basePos[1]);

        // ---------- 글자 도우미: 흰 몸통 + 남색 1도트 외곽선 (그림자 없음) ----------
        public static Text Label(string name, Transform parent, Vector2 pos, Vector2 size, int fontSize, TextAnchor anchor, Color? color = null)
        {
            var t = Ui.Text(name, parent, pos, size, fontSize, anchor, color ?? Ink, shadow: false);
            float p = Mathf.Clamp(t.fontSize / 12f, 1f, Ui.Px);   // 외곽선 = 한 도트. 작은 글자는 글꼴 도트, 큰 글자도 게임 도트(4px)를 넘지 않는다 (글자 속 틈이 메워지지 않게)
            foreach (var d in new[] { new Vector2(p, p), new Vector2(p, 0f), new Vector2(0f, p) })
            {
                var o = t.gameObject.AddComponent<Outline>(); o.effectColor = Line; o.effectDistance = d; o.useGraphicAlpha = true;
            }
            return t;
        }

        /// <summary>◀ 6×9도트: 흰 속 + 남색 테두리</summary>
        void BuildPointer()
        {
            int w = 6, h = 9;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int d = Mathf.Abs(y - 4);                       // 왼쪽 꼭짓점이 가운데 줄
                    bool inside = x >= d;
                    bool edge = x == d || x == w - 1 || y == 0 || y == h - 1;
                    tex.SetPixel(x, y, !inside ? Color.clear : edge ? Line : Ink);
                }
            tex.Apply();
            var sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f);
            pointer = Ui.Image("Pointer", canvas, Vector2.zero, new Vector2(w * Ui.Px, h * Ui.Px), Color.white);
            pointer.sprite = sp; pointer.gameObject.SetActive(false);
        }

        public void AddButton(string label, System.Action act)
        {
            int i = btns.Count;
            var b = new Btn { Act = act, X = MenuX, Y = MenuTop - MenuStep * i };
            b.Label = Label("TitleBtn", canvas, new Vector2(b.X, b.Y), new Vector2(400f, 56f), 36, TextAnchor.MiddleLeft);
            b.Label.text = label;
            b.Rect = b.Label.rectTransform;
            b.Rect.pivot = new Vector2(0f, 0.5f); b.Rect.anchoredPosition = new Vector2(b.X, b.Y);
            b.Label.gameObject.SetActive(false);
            btns.Add(b);
        }

        /// <summary>항목이 위에서 아래로 하나씩 (0.07초 간격)</summary>
        public void ShowButtons() => StartCoroutine(ShowCo());
        IEnumerator ShowCo()
        {
            foreach (var b in btns)
            {
                if (b.Label != null) b.Label.gameObject.SetActive(true);
                yield return new WaitForSecondsRealtime(0.07f);
            }
        }
        public void SetButtonsVisible(bool v) { foreach (var b in btns) if (b.Label != null) b.Label.gameObject.SetActive(v); if (!v && pointer != null) pointer.gameObject.SetActive(false); }
        public void ClearButtons() { foreach (var b in btns) if (b.Label != null) Destroy(b.Label.gameObject); btns.Clear(); if (pointer != null) pointer.gameObject.SetActive(false); }

        // ---------- 가구: 오른쪽 아래 조작 안내, 왼쪽 아래 버전 ----------
        void BuildFurniture()
        {
            var fur = new GameObject("Furniture", typeof(RectTransform)).transform; fur.SetParent(canvas, false);
            furniture = fur.gameObject;
            float p = Ui.Px;
            Hint(fur, "선택", "클릭", new Vector2(760f, -440f));
            Hint(fur, "뒤로", "ESC", new Vector2(760f, -496f));
            var ver = Label("Version", fur, new Vector2(-860f, -510f), new Vector2(400f, 30f), 24, TextAnchor.MiddleLeft, new Color(0.97f, 0.97f, 0.97f, 0.8f));
            ver.rectTransform.pivot = new Vector2(0f, 0.5f); ver.rectTransform.anchoredPosition = new Vector2(-860f, -510f);
            ver.text = "v0.1  2026-10-13";
        }

        GameObject furniture;
        void OnDestroy() { if (furniture != null) Destroy(furniture); if (pointer != null) Destroy(pointer.gameObject); }

        /// <summary>"선택 [클릭]": 글자 + 키캡 (남색 상자에 흰 글자)</summary>
        void Hint(Transform parent, string what, string key, Vector2 at)
        {
            var t = Label("Hint", parent, at, new Vector2(120f, 40f), 24, TextAnchor.MiddleRight);
            t.rectTransform.pivot = new Vector2(1f, 0.5f); t.rectTransform.anchoredPosition = at;
            t.text = what;
            float p = Ui.Px;
            var cap = Ui.Image("Cap", parent, at + new Vector2(p * 3f, 0f), new Vector2(key.Length * 24f + p * 4f, 10f * p), Line);
            cap.rectTransform.pivot = new Vector2(0f, 0.5f); cap.rectTransform.anchoredPosition = at + new Vector2(p * 3f, 0f);
            var capIn = Ui.Image("CapIn", cap.transform, new Vector2(0f, p * 0.5f), cap.rectTransform.sizeDelta - new Vector2(p * 2f, p * 3f), new Color(0.32f, 0.3f, 0.44f));
            var k = Ui.Text("Key", cap.transform, new Vector2(0f, p * 0.5f), cap.rectTransform.sizeDelta, 24, TextAnchor.MiddleCenter, Ink, shadow: false);
            k.text = key;
        }

        void Update()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current; if (mouse == null) return;
            var mp = mouse.position.ReadValue();
            float dt = Time.unscaledDeltaTime;
            TickRays(Time.unscaledTime);
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
            // 메뉴: 올리면 ◀만 붙는다. 글자는 안 변한다. 누르면 그 자리에서 실행
            var pt = new Vector2(mp.x - Screen.width * 0.5f, mp.y - Screen.height * 0.5f) * (1080f / Screen.height);
            hovered = -1;
            for (int i = 0; i < btns.Count; i++)
            {
                var b = btns[i];
                if (b.Label == null || !b.Label.gameObject.activeSelf) continue;
                float w = b.Label.preferredWidth + Ui.Px * 8f;
                bool over = pt.x >= b.X - Ui.Px * 2f && pt.x <= b.X + w && Mathf.Abs(pt.y - b.Y) < MenuStep * 0.5f;
                if (!over) continue;
                hovered = i;
                if (mouse.leftButton.wasPressedThisFrame) { Sfx.Play(Sfx.Click(), 0.5f, 1.2f); lastClick = pt; b.Act?.Invoke(); }
                break;
            }
            if (pointer != null)
            {
                pointer.gameObject.SetActive(hovered >= 0 && !frozen);
                if (hovered >= 0) pointer.rectTransform.anchoredPosition = new Vector2(btns[hovered].X + btns[hovered].Label.preferredWidth + Ui.Px * 5f, btns[hovered].Y);
            }
        }
        Vector2 lastClick;

        /// <summary>시작: 클릭한 자리에서 원형이 0.2초에 딱 닫힌다 (Shelldiver 분석). 문 줌인은 그 전에 0.3초만</summary>
        public IEnumerator Enter(System.Action onDone)
        {
            frozen = true;
            SetButtonsVisible(false);
            if (furniture != null) furniture.SetActive(false);
            var cam = Camera.main;
            // 문으로 짧게 당긴다 (0.3초, 깊이 2.3배) — 문이 양쪽으로 열리고
            Vector3 from = cam.transform.position; Vector2 door = DoorCenter;
            Vector3 to = new Vector3(door.x, door.y, from.z);
            float size0 = cam.orthographicSize, size1 = 2.2f;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.3f)
            {
                float k = Ease.OutQuad(t);
                cam.transform.position = Vector3.Lerp(from, to, k);
                cam.orthographicSize = Mathf.Lerp(size0, size1, k);
                yield return null;
            }
            cam.transform.position = to; cam.orthographicSize = size1;
            Sfx.Play(Sfx.Click(), 0.6f, 0.7f);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.12f)
            {
                float sx = Mathf.Max(0.02f, 1f - t);
                if (doorL != null) doorL.localScale = new Vector3(sx, 1f, 1f);
                if (doorR != null) doorR.localScale = new Vector3(sx, 1f, 1f);
                yield return null;
            }
            // 원형: 문 자리(= 카메라 중심)에서 0.2초
            var iris = Iris.Get(); iris.Offset = Vector2.zero;
            yield return iris.Close(0.2f);
            onDone?.Invoke();
        }

        // ---------- 빛내림: 분석대로 적게·넓게·부드럽게·희미하게 (3줄기, 밝기 ≤ 8%) ----------
        const int RayW = 480, RayH = 270;
        Texture2D rayTex; Color32[] rayPx; SpriteRenderer raySr; float rayClock;
        static readonly float[] rayCx = { 250f, 360f, 470f }, rayWd = { 44f, 26f, 60f }, rayA = { 0.07f, 0.05f, 0.08f };
        const float RaySlope = 0.404f;

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
            if (rayTex == null || t - rayClock < 1f / 8f) return;
            rayClock = t;
            DrawRays(t);
        }

        void DrawRays(float t)
        {
            int n = rayCx.Length;
            var cx = new float[n]; var al = new float[n];
            for (int i = 0; i < n; i++)
            {
                al[i] = rayA[i] * (0.7f + 0.3f * Mathf.Sin(t * (0.2f + 0.07f * i) + i * 1.7f));
                cx[i] = rayCx[i] + 5f * Mathf.Sin(t * 0.12f + i * 0.85f);
            }
            for (int y = 0; y < RayH; y++)
            {
                float fade = Mathf.Pow((float)y / (RayH - 1), 1.2f);
                float dx = (y - RayH) * RaySlope;
                int row = y * RayW;
                for (int x = 0; x < RayW; x++)
                {
                    float a = 0f;
                    for (int i = 0; i < n; i++)
                    {
                        float d = Mathf.Abs(x - (cx[i] + dx)) / (rayWd[i] * 0.5f);
                        if (d < 1f) a += al[i] * (1f - d * d);      // 가장자리 부드럽게
                    }
                    a *= fade;
                    rayPx[row + x] = new Color32(255, 248, 215, (byte)(255f * Mathf.Min(0.2f, a)));
                }
            }
            rayTex.SetPixels32(rayPx); rayTex.Apply(false);
        }
    }

    /// <summary>도트 커서: 8×12 흰 화살표 + 남색 테두리를 4배로 (소프트웨어 커서라 크기 자유)</summary>
    public static class PixelCursor
    {
        static Texture2D tex;
        static readonly string[] Art =
        {
            "X.......",
            "XX......",
            "XWX.....",
            "XWWX....",
            "XWWWX...",
            "XWWWWX..",
            "XWWWWWX.",
            "XWWWWWWX",
            "XWWXXXX.",
            "XWX.....",
            "XX......",
            "X.......",
        };
        public static void Apply()
        {
            if (tex == null)
            {
                int s = Mathf.RoundToInt(Ui.Px), w = 8 * s, h = Art.Length * s;
                tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        char c = Art[(h - 1 - y) / s][x / s];   // 위 줄이 화살표 꼭대기, 텍스처 y는 아래가 0
                        tex.SetPixel(x, y, c == 'W' ? Title.Ink : c == 'X' ? Title.Line : Color.clear);
                    }
                tex.Apply();
            }
            Cursor.SetCursor(tex, Vector2.zero, CursorMode.ForceSoftware);
        }
    }
}
