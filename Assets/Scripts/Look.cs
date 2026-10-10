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

        // 화면 세 구역 (카메라 세로 반지름 5 기준)
        public const float ShelfY = -1.6f;      // 소환지역: 깡통이 올라가는 선반
        public const float DropY = -4.3f;       // 낙하지역: 여기 닿으면 정산
        public const float ShelfLeft = -6.5f, ShelfRight = 6.5f;
        public const float CeilingY = 4.75f;    // 천장: 깡통은 이 아래에서만 논다 (화면 위 끝 바로 아래)

        /// <summary>
        /// 사격장: 위 = 타격지역(하늘), 가운데 = 소환지역(긴 선반), 아래 = 낙하지역(모래).
        /// 왼쪽 구석에 술집 벽 한 조각만 남겨 서부 티를 낸다.
        /// </summary>
        public static void BuildRange()
        {
            Camera.main.backgroundColor = Sky;
            // 낙하지역: 모래 + 어두운 띠
            Quad("DropZone", new Vector2(0f, -4.9f), new Vector2(40f, 2.2f), Sand, -10);
            Quad("DropEdge", new Vector2(0f, -3.8f), new Vector2(40f, 0.05f), new Color(0.55f, 0.38f, 0.22f), -9);
            Quad("DropTint", new Vector2(0f, -4.9f), new Vector2(40f, 2.2f), new Color(0.3f, 0.15f, 0.05f, 0.18f), -8);
            // 소환지역: 긴 선반 + 기둥
            var shelf = new GameObject("Shelf").transform;
            Quad("Rail", new Vector2(0f, ShelfY), new Vector2(ShelfRight - ShelfLeft, 0.22f), WoodLight, -5, shelf);
            Quad("RailShadow", new Vector2(0f, ShelfY - 0.16f), new Vector2(ShelfRight - ShelfLeft, 0.1f), Wood, -5, shelf);
            for (float x = ShelfLeft + 0.5f; x <= ShelfRight - 0.5f; x += 1.5f)
                Quad("Post", new Vector2(x, (ShelfY + DropY) * 0.5f + 0.2f), new Vector2(0.22f, ShelfY - DropY - 0.4f), Wood, -6, shelf);
            var rail = new GameObject("RailCollider"); rail.transform.position = new Vector3(0f, ShelfY, 0f);
            var rc = rail.AddComponent<BoxCollider2D>(); rc.size = new Vector2(ShelfRight - ShelfLeft, 0.22f);
            rail.AddComponent<Shelf>();
            // 타격지역: 하늘. 맨 위에 들보 — 천장. 넘치면 튕겨 돌아온다
            Quad("Beam", new Vector2(0f, CeilingY + 0.14f), new Vector2(40f, 0.22f), Wood, -4);
            Quad("BeamLight", new Vector2(0f, CeilingY + 0.2f), new Vector2(40f, 0.06f), WoodLight, -3);
            var ceil = new GameObject("Ceiling"); ceil.transform.position = new Vector3(0f, CeilingY + 0.4f, 0f);
            var cc = ceil.AddComponent<BoxCollider2D>(); cc.size = new Vector2(40f, 0.5f); cc.sharedMaterial = new PhysicsMaterial2D("ceil") { bounciness = 0.5f, friction = 0.1f };
            ceil.AddComponent<Wall>();
            // 낙하 감지
            var drop = new GameObject("DropCollider"); drop.transform.position = new Vector3(0f, DropY - 0.3f, 0f);
            var dc = drop.AddComponent<BoxCollider2D>(); dc.size = new Vector2(40f, 0.6f); dc.isTrigger = true;
            drop.AddComponent<DropZone>();
            // 양옆 벽: 화면 끝. 깡통이 튕겨 돌아온다 (사용자 10-10)
            float halfW = Camera.main.orthographicSize * Camera.main.aspect;
            var bouncy = new PhysicsMaterial2D("wall") { bounciness = 0.75f, friction = 0.1f };
            foreach (float sx in new[] { -1f, 1f })
            {
                var w = new GameObject(sx < 0 ? "WallL" : "WallR"); w.transform.position = new Vector3(sx * (halfW + 0.25f), 0f, 0f);
                var wc = w.AddComponent<BoxCollider2D>(); wc.size = new Vector2(0.5f, 40f); wc.sharedMaterial = bouncy;
                w.AddComponent<Wall>();
                // 벽 표시: 가장자리 기둥
                Quad("WallPost", new Vector2(sx * (halfW - 0.12f), 0.5f), new Vector2(0.24f, 9f), Wood, -4);
                Quad("WallPostLight", new Vector2(sx * (halfW - 0.2f), 0.5f), new Vector2(0.06f, 9f), WoodLight, -3);
            }
            // 서부 티: 왼쪽 위 술집 간판 조각
            var sign = new GameObject("Saloon").transform; sign.position = new Vector3(7.0f, 3.4f, 0f);
            Quad("Board", Vector2.zero, new Vector2(3.6f, 1.0f), WoodLight, -7, sign);
            Quad("Text", Vector2.zero, new Vector2(2.6f, 0.26f), Dark, -6, sign);
            Quad("Chain1", new Vector2(-1.4f, 0.9f), new Vector2(0.06f, 0.9f), Dark, -7, sign);
            Quad("Chain2", new Vector2(1.4f, 0.9f), new Vector2(0.06f, 0.9f), Dark, -7, sign);
        }
    }

    /// <summary>선반 표식: 깡통이 올라가 있는 곳. 띄워진 깡통은 이걸 통과해 떨어진다</summary>
    public class Shelf : MonoBehaviour { }

    /// <summary>낙하지역 표식: 깡통이 여기 닿으면 정산</summary>
    public class DropZone : MonoBehaviour { }

    /// <summary>옆 벽 표식: 깡통이 튕긴다</summary>
    public class Wall : MonoBehaviour { }
}
