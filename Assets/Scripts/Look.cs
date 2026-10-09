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

        /// <summary>술집 앞: 하늘, 모래 바닥, 술집 정면, 울타리</summary>
        public static void BuildSaloon()
        {
            Camera.main.backgroundColor = Sky;
            Quad("Ground", new Vector2(0f, -5.5f), new Vector2(40f, 3f), Sand, -10);
            Quad("GroundLine", new Vector2(0f, -4.02f), new Vector2(40f, 0.06f), new Color(0.6f, 0.44f, 0.26f), -9);
            // 술집 정면 (왼쪽)
            var saloon = new GameObject("Saloon").transform; saloon.position = new Vector3(-5.5f, 0f, 0f);
            Quad("Wall", new Vector2(0f, -0.5f), new Vector2(8f, 7f), Wood, -8, saloon);
            Quad("Roof", new Vector2(0f, 3.2f), new Vector2(8.6f, 0.5f), Dark, -7, saloon);
            Quad("Sign", new Vector2(0f, 2.2f), new Vector2(5f, 1.1f), WoodLight, -7, saloon);
            Quad("SignText", new Vector2(0f, 2.2f), new Vector2(3.6f, 0.3f), Dark, -6, saloon);
            Quad("Door", new Vector2(0f, -2.4f), new Vector2(1.6f, 3.2f), Dark, -7, saloon);
            Quad("DoorL", new Vector2(-0.42f, -2.0f), new Vector2(0.7f, 1.6f), WoodLight, -6, saloon);
            Quad("DoorR", new Vector2(0.42f, -2.0f), new Vector2(0.7f, 1.6f), WoodLight, -6, saloon);
            Quad("WinL", new Vector2(-2.6f, -0.8f), new Vector2(1.4f, 1.6f), new Color(0.95f, 0.85f, 0.5f), -7, saloon);
            Quad("WinR", new Vector2(2.6f, -0.8f), new Vector2(1.4f, 1.6f), new Color(0.95f, 0.85f, 0.5f), -7, saloon);
            Quad("Porch", new Vector2(0f, -4.0f), new Vector2(9f, 0.3f), WoodLight, -6, saloon);
            // 울타리 (오른쪽): 깡통이 올라가는 자리
            var fence = new GameObject("Fence").transform; fence.position = new Vector3(4.5f, 0f, 0f);
            Quad("Rail", new Vector2(0f, -2.3f), new Vector2(7f, 0.22f), WoodLight, -5, fence);
            Quad("Rail2", new Vector2(0f, -3.1f), new Vector2(7f, 0.18f), WoodLight, -5, fence);
            for (int i = -3; i <= 3; i++) Quad($"Post{i}", new Vector2(i * 1.1f, -3.1f), new Vector2(0.22f, 1.9f), Wood, -6, fence);
            var rail = new GameObject("RailCollider"); rail.transform.position = new Vector3(4.5f, -2.3f, 0f);
            var rc = rail.AddComponent<BoxCollider2D>(); rc.size = new Vector2(7f, 0.22f);
            rail.layer = 0; rail.tag = "Untagged";
            var ground = new GameObject("GroundCollider"); ground.transform.position = new Vector3(0f, -4.15f, 0f);
            var gc = ground.AddComponent<BoxCollider2D>(); gc.size = new Vector2(40f, 0.3f);
            ground.AddComponent<Ground>();
        }
    }

    /// <summary>바닥 표식: 깡통이 여기 닿으면 떨어진 것</summary>
    public class Ground : MonoBehaviour { }
}
