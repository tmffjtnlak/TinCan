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
    ///   fx_spark    5×5    산 순간 튀는 조각(+)            fx_ring   16×16  산 순간 퍼지는 링
    ///   fx_dot      2×2    살 수 있는 노드 주변에 떠오르는 점   cursor_corner 7×7  올린 노드 네 귀퉁이 꺾쇠 (왼쪽 위 기준)
    ///   node_bevel  30×30  테두리 안쪽 한 줄 (입체)        bg_far    64×64  먼 배경 타일 (느리게 흐름)
    ///   icon_{노드id} 14×14  노드 아이콘. 없으면 계열 아이콘 icon_reload / icon_shelf / icon_money / icon_auto / icon_time / icon_root
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
                // Shelldiver 비율 (10-12 측정, 1080p 기준): 마름모 폭 ≈ 126px, 테두리 ≈ 7px, 아이콘 ≈ 56px, 노드 간격 ≈ 195px, 도트 ≈ 4~5px
                // → 트리 도트 4px: 마름모 30칸, 테두리 2칸, 아이콘 14칸
                case "node_frame": return Make(Diamond(30, 2, false), name);
                case "node_fill": return Make(Diamond(30, 0, true), name);
                case "node_bevel": return Make(DiamondInset(30, 4), name);
                case "node_glow": return Make(DiamondSoft(40), name);
                case "bg_far": return Make(FarTile(64), name);
                case "icon_reload": return Make(IconBullet(14), name);
                case "icon_shelf": return Make(IconShelf(14), name);
                case "icon_money": return Make(IconCoin(14), name);
                case "icon_auto": return Make(IconGear(14), name);
                case "icon_time": return Make(IconClock(14), name);
                case "icon_root": return Make(IconStar(14), name);
                case "link": return Make(Solid(6, 2), name);
                case "link_dim": return Make(Dots(6, 2), name);
                case "bg_tile": return Make(Tile(32), name);
                case "tip_panel": return Make(Box(24, 2), name, border: 4);
                case "fx_spark": return Make(Plus(5), name);
                case "fx_dot": return Make(Solid(2, 2), name);
                case "fx_puff": return Make(Puff(12), name);
                case "fx_ring": return Make(Ring(16), name);
                case "cursor_corner": return Make(Corner(7), name);
                case "tumbleweed": return Make(Tumbleweed(16), name);
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

        /// <summary>안쪽 한 줄 (테두리에서 inset만큼 들어간 마름모 선 1픽셀) — 베벨 느낌</summary>
        static Texture2D DiamondInset(int n, int inset)
        {
            var t = Blank(n, n); float c = (n - 1) * 0.5f, r = c - inset;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                if (d <= r && d > r - 1.5f) t.SetPixel(x, y, Color.white);
            }
            return t;
        }

        /// <summary>먼 배경 타일: 큰 희미한 마름모 윤곽 하나 + 점 둘 (반복되면 돌무늬처럼)</summary>
        static Texture2D FarTile(int n)
        {
            var t = Blank(n, n); float c = (n - 1) * 0.5f, r = n * 0.3f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                if (d <= r && d > r - 2f) t.SetPixel(x, y, new Color(1f, 1f, 1f, 0.5f));
                else if (d <= r - 2f) t.SetPixel(x, y, new Color(1f, 1f, 1f, 0.18f));
            }
            t.SetPixel(4, n - 6, new Color(1f, 1f, 1f, 0.6f)); t.SetPixel(n - 7, 5, new Color(1f, 1f, 1f, 0.6f));
            return t;
        }

        // ---------- 임시 아이콘 (계열별, 14×14, 흰색). 노드별 그림(icon_{id})이 오면 그걸 쓴다 ----------
        static void Px(Texture2D t, int x, int y, float a = 1f) { if (x >= 0 && y >= 0 && x < t.width && y < t.height) t.SetPixel(x, y, new Color(1f, 1f, 1f, a)); }
        static void Rect(Texture2D t, int x0, int y0, int w, int h, float a = 1f) { for (int y = y0; y < y0 + h; y++) for (int x = x0; x < x0 + w; x++) Px(t, x, y, a); }

        static Texture2D IconBullet(int n)   // 장전: 기울어진 탄 하나 + 작은 탄
        {
            var t = Blank(n, n);
            Rect(t, 3, 5, 7, 4); Px(t, 10, 6); Px(t, 10, 7); Px(t, 11, 6); Px(t, 11, 7); Px(t, 12, 7);
            Rect(t, 2, 5, 1, 4, 0.6f);
            Rect(t, 4, 1, 4, 2, 0.8f); Px(t, 8, 1, 0.8f);
            return t;
        }
        static Texture2D IconShelf(int n)    // 선반: 가로 판 + 깡통 둘
        {
            var t = Blank(n, n);
            Rect(t, 1, 4, 12, 2); Rect(t, 2, 2, 2, 2, 0.7f); Rect(t, 10, 2, 2, 2, 0.7f);
            Rect(t, 3, 6, 3, 5); Rect(t, 8, 6, 3, 5); Rect(t, 3, 8, 3, 1, 0.5f); Rect(t, 8, 8, 3, 1, 0.5f);
            return t;
        }
        static Texture2D IconCoin(int n)     // 돈: 동전 (원 + $ 느낌의 세로줄)
        {
            var t = Blank(n, n); float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)); if (d <= 6f) Px(t, x, y, d > 4.5f ? 1f : 0.45f); }
            Rect(t, 6, 3, 2, 8); Rect(t, 4, 4, 5, 1); Rect(t, 5, 9, 5, 1); Rect(t, 4, 6, 1, 1); Rect(t, 9, 7, 1, 1);
            return t;
        }
        static Texture2D IconGear(int n)     // 자동: 톱니
        {
            var t = Blank(n, n); float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)); if (d <= 4.5f && d > 2f) Px(t, x, y); }
            Rect(t, 6, 0, 2, 3); Rect(t, 6, 11, 2, 3); Rect(t, 0, 6, 3, 2); Rect(t, 11, 6, 3, 2);
            Rect(t, 2, 2, 2, 2); Rect(t, 10, 2, 2, 2); Rect(t, 2, 10, 2, 2); Rect(t, 10, 10, 2, 2);
            return t;
        }
        static Texture2D IconClock(int n)    // 시간: 시계
        {
            var t = Blank(n, n); float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)); if (d <= 6f && d > 4.8f) Px(t, x, y); }
            Rect(t, 6, 6, 2, 2); Rect(t, 6, 7, 1, 4); Rect(t, 7, 7, 3, 1);
            return t;
        }
        static Texture2D IconStar(int n)     // 뿌리: 별
        {
            var t = Blank(n, n); int c = n / 2;
            for (int i = 0; i < n; i++) { Px(t, i, c, 0.8f); Px(t, c, i, 0.8f); }
            for (int i = -3; i <= 3; i++) { Px(t, c + i, c + i); Px(t, c + i, c - i); }
            Rect(t, c - 1, c - 1, 3, 3);
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

        /// <summary>선택 꺾쇠 (왼쪽 위 모서리 ㄱ자, 2픽셀 두께). 네 귀퉁이는 코드가 돌려 쓴다</summary>
        static Texture2D Corner(int n)
        {
            var t = Blank(n, n);
            for (int i = 0; i < n; i++) for (int k = 0; k < 2; k++) { t.SetPixel(i, n - 1 - k, Color.white); t.SetPixel(k, n - 1 - i, Color.white); }
            return t;
        }

        /// <summary>흰 뭉게구름 조각: 울퉁불퉁한 둥근 덩어리 (Shelldiver 구매 순간의 흰 구름). 여러 개 겹쳐 쓴다</summary>
        static Texture2D Puff(int n)
        {
            var t = Blank(n, n); float c = (n - 1) * 0.5f;
            var rng = new System.Random(7);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float r = c - 0.5f + (float)(rng.NextDouble() * 1.6 - 0.8);
                if (d <= r) t.SetPixel(x, y, Color.white);
            }
            return t;
        }

        /// <summary>회전초: 성긴 둥근 덤불 (게이지 대신 굴러간다). 흰색, 코드가 마른 풀색을 입힌다</summary>
        static Texture2D Tumbleweed(int n)
        {
            var t = Blank(n, n); float c = (n - 1) * 0.5f;
            var rng = new System.Random(11);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                if (d > c) continue;
                bool ring = d > c - 1.6f;
                bool inner = rng.NextDouble() < 0.38;
                if (ring || inner) t.SetPixel(x, y, new Color(1f, 1f, 1f, ring ? 1f : 0.75f));
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
