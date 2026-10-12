using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 스킬트리 그림 자리 (사용자 10-12: "이미지만 바꾸면 되도록"). 이름마다 한 칸.
    /// `Assets/Art/Tree/{이름}.png` 가 있으면 그걸 쓰고, 없으면 코드로 만든 도트 임시 그림.
    /// 전부 흰색 바탕이라 색은 코드가 입힌다 (계열 색). 크기는 그림 픽셀 × Ui.Px.
    ///
    /// 자리 목록 (픽셀 크기는 임시 그림 기준, 바꿔도 됨):
    ///   node_frame  20×20  마름모 테두리 (계열 색)       node_fill 20×20  마름모 속
    ///   node_glow   28×28  마름모 번짐 (살 수 있을 때 숨쉼)  node_lock 20×20  잠김 표시(선택)
    ///   link        6×2    선 한 조각 (가로로 반복)         link_dim  6×2    점선
    ///   bg_tile     32×32  배경 타일 (반복)                tip_panel 24×24  설명 상자 9슬라이스 (테두리 4px)
    ///   fx_spark    5×5    산 순간 튀는 조각               fx_ring   16×16  산 순간 퍼지는 링
    ///   icon_{노드id} 12×12  노드 아이콘 (없으면 글자)
    /// </summary>
    public static class TreeArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        public const float Ppu = 36f;

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var s)) return s;
            s = LoadFile(name) ?? Placeholder(name);
            cache[name] = s;
            return s;
        }

        /// <summary>그림이 실제로 있나 (아이콘 자리: 없으면 글자를 쓴다)</summary>
        public static bool Has(string name) => Get(name) != null;

        static Sprite LoadFile(string name)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Tree/{name}.png");
#else
            return Resources.Load<Sprite>("Art/Tree/" + name);
#endif
        }

        // ---------- 임시 도트 그림 (전부 흰색, 알파로 모양) ----------
        static Sprite Placeholder(string name)
        {
            switch (name)
            {
                case "node_frame": return Make(Diamond(20, 2, false), name);
                case "node_fill": return Make(Diamond(20, 0, true), name);
                case "node_glow": return Make(DiamondSoft(28), name);
                case "link": return Make(Solid(6, 2), name);
                case "link_dim": return Make(Dots(6, 2), name);
                case "bg_tile": return Make(Tile(32), name);
                case "tip_panel": return Make(Box(24, 2), name, border: 4);
                case "fx_spark": return Make(Plus(5), name);
                case "fx_ring": return Make(Ring(16), name);
                default: return null;   // icon_*: 없으면 글자
            }
        }

        static Sprite Make(Texture2D t, string name, int border = 0)
        {
            t.name = name; t.filterMode = FilterMode.Point; t.wrapMode = TextureWrapMode.Repeat; t.Apply();
            var b = border > 0 ? new Vector4(border, border, border, border) : Vector4.zero;
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect, b);
        }

        static Texture2D Blank(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 0);
            t.SetPixels32(px);
            return t;
        }

        static Texture2D Diamond(int n, int thick, bool fill)
        {
            var t = Blank(n, n); float c = (n - 1) * 0.5f, r = c;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                bool on = fill ? d <= r - 2.5f : (d <= r && d > r - thick - 0.5f);
                if (on) t.SetPixel(x, y, Color.white);
            }
            return t;
        }

        static Texture2D DiamondSoft(int n)
        {
            var t = Blank(n, n); float c = (n - 1) * 0.5f, r = c;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                if (d <= r) t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d / r) * 0.5f));
            }
            return t;
        }

        static Texture2D Solid(int w, int h) { var t = Blank(w, h); for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) t.SetPixel(x, y, Color.white); return t; }
        static Texture2D Dots(int w, int h) { var t = Blank(w, h); for (int y = 0; y < h; y++) for (int x = 0; x < w / 2; x++) t.SetPixel(x, y, Color.white); return t; }

        static Texture2D Tile(int n)
        {
            var t = Blank(n, n);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) t.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
            t.SetPixel(0, 0, new Color(1f, 1f, 1f, 0.82f));                       // 모서리 점
            t.SetPixel(n / 2, n / 2, new Color(1f, 1f, 1f, 0.9f));
            for (int i = 0; i < 3; i++) t.SetPixel((n / 4 + i * 7) % n, (n / 3 + i * 11) % n, new Color(1f, 1f, 1f, 0.94f));
            return t;
        }

        static Texture2D Box(int n, int edge)
        {
            var t = Blank(n, n);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                bool border = x < edge || y < edge || x >= n - edge || y >= n - edge;
                bool corner = (x < edge && y < edge) || (x < edge && y >= n - edge) || (x >= n - edge && y < edge) || (x >= n - edge && y >= n - edge);
                if (corner && (x == 0 || y == 0 || x == n - 1 || y == n - 1)) continue;   // 모서리 한 픽셀 깎기
                t.SetPixel(x, y, border ? Color.white : new Color(1f, 1f, 1f, 0.92f));
            }
            return t;
        }

        static Texture2D Plus(int n) { var t = Blank(n, n); int c = n / 2; for (int i = 0; i < n; i++) { t.SetPixel(i, c, Color.white); t.SetPixel(c, i, Color.white); } return t; }

        static Texture2D Ring(int n)
        {
            var t = Blank(n, n); float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                if (d <= c && d > c - 2f) t.SetPixel(x, y, Color.white);
            }
            return t;
        }
    }
}
