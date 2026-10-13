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
        const float Ppu = 27f;   // 480×270
        // 띠 경계 (원본 아래쪽부터 센 픽셀 행). 겹침 8줄: 세로 시차로 틈이 안 보이게
        static readonly int[] cutLo = { 150, 0, 0 }, cutHi = { 270, 158, 12 };   // 뒤(하늘·산, 물탱크 위), 가운데(모래·울타리·술집), 앞(맨 아래 모래 12줄). 480×270 그림 (10-13 최종: 술집 오른쪽 아래)
        static readonly float[] ampX = { 1f, 2f, 4f }, ampY = { 0f, 1f, 2f };     // 시차: 분석대로 절반 (마우스 끝까지 갔을 때 도트 수)
        static readonly float[] shiftX = { 0f, 0f, 0f };                          // 320×180 그림은 술집이 이미 오른쪽에 있어 밀지 않는다
        readonly Transform[] layer = new Transform[3];
        readonly Vector2[] basePos = new Vector2[3];
        Vector2 look, lookV;
        Transform doorL, doorR;
        static readonly Vector3 doorArt = new Vector3(130f / Ppu, -78f / Ppu, 0f);    // 입구 여닫이문 자리: 그림 가운데(240,135)에서 오른쪽 130, 아래 78 (문 가운데 = 그림 (370, 213))
        bool frozen;

        // ---------- 메뉴: 글자 + ◀ (상자 없음) ----------
        public static readonly Color Ink = new Color(0.97f, 0.97f, 0.97f), Line = new Color(0.15f, 0.13f, 0.25f);
        // 용수철 상태 (사용자 10-13 "용수철로 부드럽게"): 가로 밀림 ox, 크기 s, 기울기 rot, 투명도 a
        class Btn
        {
            public Text Label; public RectTransform Rect; public System.Action Act; public float X, Y;
            public System.Func<string> Txt;            // 값이 바뀌는 항목 (설정)
            public System.Action<int> Change;          // 값 항목: -1 줄임 / +1 늘림
            public bool Header; public float AlphaMul = 1f;
            public bool Shown; public float Ox = -60f, OxV, S = 0.6f, SV, Rot, RotV, A;
        }
        readonly List<Btn> btns = new List<Btn>();
        Image pointer; int hovered = -1, lastHovered = -1;
        float pX, pXV, pY, pYV, pS, pSV;   // ◀ 용수철: 위치(항목 사이를 미끄러짐), 크기(톡 튀어나옴)
        bool pPlaced;
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
                    m.SetVector("_Band", new Vector4(150f / tex.height, 180f / tex.height, 0f, 0f));   // 산 밑동 지평선 띠 (물탱크 위)
                    m.SetFloat("_Amp", i == 2 ? 0f : 1.2f); m.SetFloat("_Rate", 6f);
                    m.SetFloat("_Desat", 0.6f);
                    m.SetVector("_DesatBand", new Vector4(110f / tex.height, 210f / tex.height, 0f, 0f));   // 울타리 위부터 하늘 쪽으로 점점 회색
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

        public void AddButton(string label, System.Action act) => Add(label, 36).Act = act;

        /// <summary>값 항목 (설정): 글자는 매 프레임 text()로, 누르면 change(-1|+1)</summary>
        public void AddOption(System.Func<string> text, System.Action<int> change)
        {
            var b = Add(text(), 36); b.Txt = text; b.Change = change;
        }

        /// <summary>메뉴 위 제목줄 (누를 수 없음, 작고 흐리게)</summary>
        public void AddHeader(string text)
        {
            var b = Add(text, 24, header: true);
            b.AlphaMul = 0.7f;
        }

        Btn Add(string label, int size, bool header = false)
        {
            int i = 0; foreach (var x in btns) if (!x.Header) i++;
            var b = new Btn { X = MenuX, Y = header ? MenuTop + MenuStep * 0.85f : MenuTop - MenuStep * i, Header = header };
            b.Label = Label("TitleBtn", canvas, new Vector2(b.X, b.Y), new Vector2(600f, 56f), size, TextAnchor.MiddleLeft);
            b.Label.text = label;
            b.Rect = b.Label.rectTransform;
            b.Rect.pivot = new Vector2(0f, 0.5f); b.Rect.anchoredPosition = new Vector2(b.X, b.Y);
            b.Label.gameObject.SetActive(false);
            btns.Add(b);
            return b;
        }

        /// <summary>항목이 위에서 아래로 하나씩 (0.06초 간격): 왼쪽에서 작게 → 용수철로 튕기며 제자리·제 크기</summary>
        public void ShowButtons() => StartCoroutine(StaggerCo(true));
        /// <summary>v=false: 위에서부터 차례로 왼쪽으로 빠지며 흐려진다 (용수철). 다 흐려지면 꺼진다</summary>
        public void SetButtonsVisible(bool v) => StartCoroutine(StaggerCo(v));
        IEnumerator StaggerCo(bool v)
        {
            foreach (var b in btns)
            {
                if (b.Label == null) continue;
                if (v && !b.Shown) { b.Ox = -60f; b.OxV = 0f; b.S = 0.6f; b.SV = 0f; b.Rot = -8f; b.RotV = 0f; b.A = 0f; b.Label.gameObject.SetActive(true); }
                b.Shown = v;
                if (!v) { b.OxV -= 300f; }   // 빠질 땐 왼쪽으로 툭 밀어 준다
                yield return new WaitForSecondsRealtime(v ? 0.06f : 0.03f);
            }
        }
        public void ClearButtons() { foreach (var b in btns) if (b.Label != null) Destroy(b.Label.gameObject); btns.Clear(); if (pointer != null) pointer.gameObject.SetActive(false); hovered = lastHovered = -1; pPlaced = false; pS = 0f; pressing = false; pressed = -1; }

        /// <summary>매 프레임: 항목·◀ 용수철 (stiffness, damping은 Hud 버튼과 같은 결)</summary>
        void TickMenu(float dt)
        {
            for (int i = 0; i < btns.Count; i++)
            {
                var b = btns[i]; if (b.Label == null) continue;
                bool hov = i == hovered && b.Shown;
                float tx = b.Shown ? (hov ? Ui.Px * 3f : 0f) : -70f;      // 올리면 오른쪽으로 3도트
                float ts = b.Shown ? (hov ? 1.1f : 1f) : 0.7f;
                Ease.Spring(ref b.Ox, ref b.OxV, tx, 320f, 15f, dt);
                Ease.Spring(ref b.S, ref b.SV, ts, 420f, 13f, dt);         // 덜 감쇠: 살짝 넘쳤다 자리
                Ease.Spring(ref b.Rot, ref b.RotV, 0f, 300f, 12f, dt);
                b.A = Mathf.MoveTowards(b.A, b.Shown ? 1f : 0f, dt / (b.Shown ? 0.12f : 0.15f));
                if (b.Txt != null) b.Label.text = b.Txt();
                b.Rect.anchoredPosition = new Vector2(b.X + Mathf.Round(b.Ox / Ui.Px) * Ui.Px, b.Y);   // 위치는 도트 칸에
                b.Rect.localScale = Vector3.one * b.S;
                b.Rect.localRotation = Quaternion.Euler(0f, 0f, b.Rot);
                var c = b.Label.color; c.a = b.A * b.AlphaMul; b.Label.color = c;
                if (!b.Shown && b.A <= 0.01f && b.Label.gameObject.activeSelf) b.Label.gameObject.SetActive(false);
            }
            // ◀: 올린 항목 끝으로 미끄러진다. 처음 나타날 땐 그 자리에서 톡 튀어나오고, 손을 떼면 쏙 들어간다
            if (pointer == null) return;
            bool on = hovered >= 0 && !frozen;
            if (on)
            {
                var b = btns[hovered];
                float tx = b.X + Mathf.Round(b.Ox / Ui.Px) * Ui.Px + b.Label.preferredWidth * b.S + Ui.Px * 5f, ty = b.Y;
                if (!pPlaced) { pX = tx + Ui.Px * 4f; pY = ty; pXV = pYV = 0f; pPlaced = true; }
                Ease.Spring(ref pX, ref pXV, tx, 500f, 22f, dt);
                Ease.Spring(ref pY, ref pYV, ty, 500f, 20f, dt);
            }
            Ease.Spring(ref pS, ref pSV, on ? 1f : 0f, 600f, 16f, dt);
            if (!on && pS < 0.05f) pPlaced = false;
            pointer.gameObject.SetActive(pS > 0.05f);
            // 가만히 있을 땐 1도트 숨쉬기 (왼쪽을 가리키며 까딱)
            float bob = on ? Mathf.Round(Mathf.Sin(Time.unscaledTime * 6f)) * Ui.Px * 0.5f : 0f;
            pointer.rectTransform.anchoredPosition = new Vector2(Mathf.Round((pX + bob) / Ui.Px) * Ui.Px, Mathf.Round(pY / Ui.Px) * Ui.Px);
            pointer.rectTransform.localScale = Vector3.one * Mathf.Max(0f, pS);
        }

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
            // 메뉴: 올리면 오른쪽으로 밀리며 살짝 커지고 ◀가 따라붙는다. 누르면 납작했다 튕기고, 그 모습이 보이도록 0.1초 뒤 실행
            var pt = new Vector2(mp.x - Screen.width * 0.5f, mp.y - Screen.height * 0.5f) * (1080f / Screen.height);
            hovered = -1;
            if (!frozen && !pressing)
                for (int i = 0; i < btns.Count; i++)
                {
                    var b = btns[i];
                    if (b.Label == null || b.Header || !b.Shown || b.A < 0.5f) continue;
                    float w = b.Label.preferredWidth + Ui.Px * 10f;
                    bool over = pt.x >= b.X - Ui.Px * 2f && pt.x <= b.X + w && Mathf.Abs(pt.y - b.Y) < MenuStep * 0.5f;
                    if (!over) continue;
                    hovered = i;
                    if (hovered != lastHovered) { Sfx.Play(Sfx.Click(), 0.15f, 2.2f); b.SV += 4f; }   // 올라탈 때 작게 똑
                    // 값 항목: 글자 왼쪽 반 = 줄임, 오른쪽 반 = 늘림, 우클릭 = 줄임
                    int dir = pt.x < b.X + b.Label.preferredWidth * 0.5f ? -1 : 1;
                    if (mouse.leftButton.wasPressedThisFrame) StartCoroutine(Press(b, dir));
                    else if (b.Change != null && mouse.rightButton.wasPressedThisFrame) StartCoroutine(Press(b, -1));
                    break;
                }
            if (pressing && pressed >= 0) hovered = pressed;
            lastHovered = hovered;
            TickMenu(dt);
        }
        bool pressing; int pressed = -1;

        IEnumerator Press(Btn b, int dir)
        {
            Sfx.Play(Sfx.Click(), 0.5f, dir < 0 && b.Change != null ? 1.0f : 1.2f);
            b.S = 0.86f; b.SV = 0f; b.Rot = Random.Range(-5f, 5f); b.OxV += 200f * (b.Change != null ? dir : 1);   // 납작 + 살짝 비틀림 + 누른 쪽으로 툭
            pSV -= 8f;                                                             // ◀도 움찔
            if (b.Change != null) { b.Change(dir); yield break; }                  // 값 항목은 바로 바뀐다 (연달아 누를 수 있게)
            pressing = true; pressed = btns.IndexOf(b);
            yield return new WaitForSecondsRealtime(0.1f);                         // 누른 모습이 보이게 0.1초 뒤 실행
            pressing = false; pressed = -1;
            b.Act?.Invoke();
        }

        /// <summary>시작: 클릭한 자리에서 원형이 0.2초에 딱 닫힌다 (Shelldiver 분석). 문 줌인은 그 전에 0.3초만</summary>
        public IEnumerator Enter(System.Action onDone)
        {
            frozen = true;
            SetButtonsVisible(false);
            if (furniture != null) furniture.SetActive(false);
            var cam = Camera.main;
            // 사용자 10-13: 고정 속도 lerp가 부자연스럽다 → 한 시간축 위에 겹치는 트윈으로.
            //  0.00~0.12 예비 동작: 살짝 물러난다 (크기 ×1.04, OutQuad)
            //  0.12~0.80 밀고 들어감: 위치 InOutCubic, 크기는 로그 공간 InOutCubic (줌 속도가 일정하게 느껴진다)
            //  0.48~     문: 용수철로 휙 열렸다 앞뒤로 흔들림 (여닫이문)
            //  0.62~0.96 원형: 천천히 출발해 빨라지며 닫힘 (InCubic), 그동안 카메라는 계속 아주 조금 더 들어간다
            Vector3 from = cam.transform.position; Vector2 door = DoorCenter;
            Vector3 to = new Vector3(door.x, door.y, from.z);
            float size0 = cam.orthographicSize, sizeBack = size0 * 1.04f, size1 = 2.0f, sizeEnd = 1.75f;
            const float tBack = 0.12f, tPush = 0.80f, tDoor = 0.48f, tIris = 0.62f, irisLen = 0.34f;
            float dL = 1f, vL = 0f, dR = 1f, vR = 0f;
            bool doorOpened = false, irisStarted = false, irisDone = false;
            var iris = Iris.Get(); iris.Offset = Vector2.zero;
            float time = 0f;
            while (!irisDone)
            {
                float dt = Time.unscaledDeltaTime; time += dt;
                // 카메라
                if (time < tBack)
                {
                    float k = Ease.OutQuad(time / tBack);
                    cam.orthographicSize = Mathf.Lerp(size0, sizeBack, k);
                }
                else if (time < tPush)
                {
                    float k = Ease.InOutCubic((time - tBack) / (tPush - tBack));
                    cam.transform.position = Vector3.Lerp(from, to, k);
                    cam.orthographicSize = Ease.LerpLog(sizeBack, size1, k);
                }
                else
                {
                    // 멈추지 않고 아주 천천히 더 들어간다 (끝나는 느낌 대신 빨려 들어가는 느낌)
                    float k = Ease.OutQuad(Mathf.Clamp01((time - tPush) / 0.4f));
                    cam.transform.position = to;
                    cam.orthographicSize = Ease.LerpLog(size1, sizeEnd, k);
                }
                // 문: 용수철. 목표 0.15(거의 옆면) — 덜 감쇠해서 지나쳐 반대로 넘어갔다(음수 = 반대쪽으로 열림) 돌아오며 흔들린다
                if (time >= tDoor)
                {
                    if (!doorOpened) { doorOpened = true; vL = vR = -14f; Sfx.Play(Sfx.Click(), 0.6f, 0.7f); }
                    Ease.Spring(ref dL, ref vL, 0.15f, 220f, 7f, dt);
                    Ease.Spring(ref dR, ref vR, 0.15f, 200f, 6.5f, dt);   // 좌우가 살짝 다르게 (같이 움직이면 기계 같다)
                    if (doorL != null) doorL.localScale = new Vector3(Snap(dL), 1f, 1f);
                    if (doorR != null) doorR.localScale = new Vector3(Snap(dR), 1f, 1f);
                }
                // 원형
                if (time >= tIris && !irisStarted)
                {
                    irisStarted = true;
                    StartCoroutine(RunIris(iris, irisLen, () => irisDone = true));
                }
                yield return null;
            }
            onDone?.Invoke();
        }

        IEnumerator RunIris(Iris iris, float len, System.Action done)
        {
            yield return iris.Close(len, Ease.InCubic);
            done();
        }

        /// <summary>문 폭을 도트 단위로 (11도트 반쪽 문이 1도트씩 줄고 는다) — 형태는 도트, 움직임은 용수철</summary>
        static float Snap(float s) { float w = Mathf.Round(s * 11f); if (Mathf.Abs(w) < 1f) w = Mathf.Sign(s == 0f ? 1f : s); return w / 11f; }

        // ---------- 빛내림 (사용자 10-13): 술집 쪽으로 3줄기, 왼쪽 위 → 오른쪽 아래, 도트 스타일 ----------
        // 도트 스타일 = 가장자리 딱 끊김 + 가운데 심지 한 단 더 밝게 + 길이 방향 밝기 4단 계단 + 숨쉬기도 단으로만 바뀜
        const int RayW = 480, RayH = 270;
        Texture2D rayTex; Color32[] rayPx; SpriteRenderer raySr; float rayClock;
        readonly float[] rowA = new float[RayW];
        // 술집 = 그림 x 275~405도트(오른쪽 100 밀린 뒤), 아래에서 60~200행.
        // 위 끝 x(rayCx)는 사선(22°)을 거꾸로 따라 올라간 자리: 120행에서 각각 지붕 왼쪽·간판·물탱크 쪽에 닿는다
        // 줄기마다 기준점(rayHit: RayPivot행에서 닿는 x)을 축으로 각도가 다르다. 각도 = 22° + rayTilt ± 천천히 흔들림
        static readonly float[] rayHit = { 330f, 375f, 430f }, rayTilt = { -6f, 1f, 6f }, rayWd = { 12f, 7f, 16f }, rayA = { 0.14f, 0.1f, 0.16f };   // 위 끝 폭. 술집 높이(120행)에선 약 2배
        const float RayFoot = 0f, RayFull = 122f;   // 이 행(아래에서) 밑으로는 사라지고, RayFull 위로는 최대 밝기 — 술집 발밑까지만 내리쬔다
        const float RayAngle = 22f, RaySway = 2.5f;  // 기준 각도, 시간에 따라 각 줄기가 ±이만큼 천천히 흔들림
        const float RayPivot = 72f;                  // 이 행(술집 간판 높이)을 축으로 돌아서, 각도가 바뀌어도 술집은 계속 비춘다
        const float RaySpread = 2.2f;                 // 사다리꼴: 바닥에서 폭이 위 끝의 (1 + 이 값)배

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
            if (rayTex == null || t - rayClock < 1f / 12f) return;
            rayClock = t;
            DrawRays(t);
        }

        void DrawRays(float t)
        {
            int n = rayHit.Length;
            var hit = new float[n]; var al = new float[n]; var slope = new float[n];
            for (int i = 0; i < n; i++)
            {
                al[i] = rayA[i] * (0.7f + 0.3f * Mathf.Sin(t * (0.2f + 0.07f * i) + i * 1.7f));
                hit[i] = rayHit[i] + 3f * Mathf.Sin(t * 0.12f + i * 0.85f);
                // 각도: 줄기마다 다르고(±6°), 서로 다른 느린 주기로 흔들린다 (두 사인을 섞어 규칙적으로 안 보이게)
                float sway = RaySway * (0.65f * Mathf.Sin(t * (0.13f + 0.05f * i) + i * 2.1f) + 0.35f * Mathf.Sin(t * 0.31f + i));
                slope[i] = Mathf.Tan((RayAngle + rayTilt[i] + sway) * Mathf.Deg2Rad);
            }
            // 사용자 10-13 정정: 형태만 도트(가장자리 딱 끊김·계단), 밝기 변화는 부드럽게, 아래로 갈수록 넓어지는 사다리꼴
            for (int y = 0; y < RayH; y++)
            {
                float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((y - RayFoot) / (RayFull - RayFoot)));   // 길이 방향 부드럽게
                float down = (RayH - 1 - y) / (float)RayH;               // 0 = 위 끝, 1 = 바닥
                int row = y * RayW;
                System.Array.Clear(rowA, 0, RayW);
                if (fade > 0f)
                    for (int i = 0; i < n; i++)
                    {
                        // 이 행에서 줄기 중심: 축(RayPivot행의 hit)에서 내려갈수록 오른쪽. 정수 도트 → 가장자리 계단
                        float c = Mathf.Round(hit[i] + (RayPivot - y) * slope[i]);
                        float half = Mathf.Round(rayWd[i] * 0.5f * (1f + RaySpread * down));   // 사다리꼴: 반폭이 정수 도트로 커진다
                        if (half < 1f) half = 1f;
                        int x0 = Mathf.Max(0, (int)(c - half)), x1 = Mathf.Min(RayW - 1, (int)(c + half));
                        for (int x = x0; x <= x1; x++)
                        {
                            float d = Mathf.Abs(x - c) / half;
                            if (d < 1f) rowA[x] += al[i] * (1f - 0.6f * d * d);   // 가장자리는 딱 끊기고, 속은 가운데가 부드럽게 밝다
                        }
                    }
                for (int x = 0; x < RayW; x++)
                    rayPx[row + x] = new Color32(255, 248, 215, (byte)(255f * Mathf.Min(0.35f, rowA[x] * fade)));
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
