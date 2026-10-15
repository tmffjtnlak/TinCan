using UnityEngine;

namespace TinCan
{
    /// <summary>그림 파일 없이 도형으로 그린다. 진짜 도트는 사용자가 그려서 교체.</summary>
    public static class Look
    {
        static Sprite square;

        /// <summary>1×1 흰 네모. 색은 SpriteRenderer.color, 크기는 localScale</summary>
        public static Sprite Square()
        {
            if (square != null) return square;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white); tex.Apply();
            square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return square;
        }

        public static SpriteRenderer Quad(string name, Vector2 pos, Vector2 size, Color color, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Square(); sr.color = color; sr.sortingOrder = order;
            return sr;
        }

        public static readonly Color Sky = new Color(0.93f, 0.62f, 0.38f);      // 저녁 하늘
        public static readonly Color Sand = new Color(0.78f, 0.6f, 0.38f);
        public static readonly Color Wood = new Color(0.42f, 0.27f, 0.16f);
        public static readonly Color WoodLight = new Color(0.58f, 0.4f, 0.24f);
        public static readonly Color Dark = new Color(0.16f, 0.1f, 0.08f);
        public static readonly Color Tin = new Color(0.72f, 0.74f, 0.76f);
        public static readonly Color TinDark = new Color(0.45f, 0.47f, 0.5f);
        public static readonly Color Label = new Color(0.85f, 0.25f, 0.2f);

        // 사용자 10-13 최종: 화면 480×270(1도트 = 1080p 4px). 인게임 그림 bg_range.png 480×270이 화면을 꽉 채운다
        // (사용자 320×180 그림을 양옆 거울 80칸 + 위 벽 45칸 + 아래 벽돌 45칸으로 늘린 임시본).
        // 위아래 띠(레터박스)는 평소엔 없고 우클릭 연출로만 (Letterbox.cs, 사용자: '기본이 이러니 어색하다').
        // 아래 좌표는 가운데 480×180 부분 기준: x = (열 - 240) / 27, y = (90 - 행) / 27.  선반 윗면 121행·x 182~287, 바닥선 147행
        public const float PlayHalfW = 240f / 27f, PlayHalfH = 90f / 27f;   // 그림 반폭 8.89(= 화면 반폭), 반높이 3.33
        public static readonly Color Bar = new Color(0.15f, 0.13f, 0.25f);   // 우클릭 레터박스 띠 색 = 도트 외곽선 남색
        // 10-16 사용자 배경 3판: 뒷바 두 단 선반 + 낮아진 카운터 (480×270 기준 카운터 윗면 189~194행, 앞면 196~234행). 깡통은 윗면 가운데(191행)에 선다
        public const float ShelfY = (135f - 191f) / 27f - 0.11f;     // 소환지역: 충돌 상자 가운데 (윗면 = +0.11 = 191행)
        /// <summary>카운터 뒤쪽 모서리 (188행). 술집 주인은 이 아래로 숨는다</summary>
        public const float CounterBackY = (135f - 188f) / 27f;
        /// <summary>뒷바 선반 두 단의 윗면 (진열장 물건이 서는 높이): 위 단 139행(127~352열), 아래 단 174행(121~362열)</summary>
        public const float BackShelfTopY = (135f - 139f) / 27f, BackShelfLowY = (135f - 174f) / 27f;
        // 낙하지역: 깡통 아랫면이 여기 닿으면 정산. 사용자 10-13: 총 상점 버튼 아랫선 높이
        // (Hud 버튼 가운데 -386, 높이 72 → 아랫선 -422 / 1080p 기준, 108px = 1 월드 → -3.907)
        public const float DropY = -422f / 108f;
        // 그림 속 선반 양 끝. 10-14 사용자 긴 선반 그림: 윗면 외곽선 103~375열 → x = (103-240)/27 … (376-240)/27 (예전 짧은 선반 -2.15~1.74)
        public const float ShelfLeft = (100f - 240f) / 27f, ShelfRight = (378f - 240f) / 27f;   // 191행에서 윗면 97~381열, 양끝 3칸 안쪽
        public const float CeilingY = 4.75f;                         // 천장: 화면 위 끝 바로 아래 (위 띠까지 날아간다)

        /// <summary>
        /// 사격장: 사용자 그림(bg_range) 창 + 보이지 않는 충돌(선반·천장·양옆 벽·낙하 감지).
        /// 그림이 없으면 예전 도형 사격장을 그린다 (자리는 같은 상수).
        /// </summary>
        public static void BuildRange()
        {
            var bg = GunView.LoadArt("bg_range");
            Camera.main.backgroundColor = Sky;
            if (bg != null)
            {
                var g = new GameObject("RangeArt");
                var sr = g.AddComponent<SpriteRenderer>(); sr.sprite = bg; sr.sortingOrder = -2;
                g.transform.position = Vector3.zero;
            }
            else
            {
                // 예전 도형 사격장 (그림이 없을 때만)
                Quad("DropZone", new Vector2(0f, DropY - 0.6f), new Vector2(PlayHalfW * 2f, 1.4f), Sand, -10);
                var shelf = new GameObject("Shelf").transform;
                Quad("Rail", new Vector2((ShelfLeft + ShelfRight) * 0.5f, ShelfY), new Vector2(ShelfRight - ShelfLeft, 0.22f), WoodLight, -5, shelf);
                Quad("Beam", new Vector2(0f, CeilingY + 0.14f), new Vector2(PlayHalfW * 2f, 0.22f), Wood, -4);
            }
            var rail = new GameObject("RailCollider"); rail.transform.position = new Vector3((ShelfLeft + ShelfRight) * 0.5f, ShelfY, 0f);
            var rc = rail.AddComponent<BoxCollider2D>(); rc.size = new Vector2(ShelfRight - ShelfLeft, 0.22f);
            rail.AddComponent<Shelf>();
            // 천장: 넘치면 튕겨 돌아온다
            var ceil = new GameObject("Ceiling"); ceil.transform.position = new Vector3(0f, CeilingY + 0.4f, 0f);
            var cc = ceil.AddComponent<BoxCollider2D>(); cc.size = new Vector2(40f, 0.5f); cc.sharedMaterial = new PhysicsMaterial2D("ceil") { bounciness = 0.5f, friction = 0.1f };
            ceil.AddComponent<Wall>();
            // 낙하 감지
            var drop = new GameObject("DropCollider"); drop.transform.position = new Vector3(0f, DropY - 0.3f, 0f);
            var dc = drop.AddComponent<BoxCollider2D>(); dc.size = new Vector2(40f, 0.6f); dc.isTrigger = true;
            drop.AddComponent<DropZone>();
            // 양옆 벽: 화면 끝. 깡통이 튕겨 돌아온다 (사용자 10-10)
            var bouncy = new PhysicsMaterial2D("wall") { bounciness = 0.75f, friction = 0.1f };
            foreach (float sx in new[] { -1f, 1f })
            {
                var w = new GameObject(sx < 0 ? "WallL" : "WallR"); w.transform.position = new Vector3(sx * (PlayHalfW + 0.25f), 0f, 0f);
                var wc = w.AddComponent<BoxCollider2D>(); wc.size = new Vector2(0.5f, 40f); wc.sharedMaterial = bouncy;
                w.AddComponent<Wall>();
            }
        }
    }
    /// <summary>선반 표식: 깡통이 올라가 있는 곳. 띄워진 깡통은 이걸 통과해 떨어진다</summary>
    public class Shelf : MonoBehaviour { }

    /// <summary>낙하지역 표식: 깡통이 여기 닿으면 정산</summary>
    public class DropZone : MonoBehaviour { }

    /// <summary>옆 벽 표식: 깡통이 튕긴다</summary>
    public class Wall : MonoBehaviour { }
}
