using System.Collections;
using UnityEngine;

namespace TinCan
{
    /// <summary>원형 페이드: 검은 판 가운데 구멍이 줄어들어 닫히고, 다시 커지며 열린다. 카메라를 따라다닌다.</summary>
    public class Iris : MonoBehaviour
    {
        static Iris inst;
        SpriteRenderer hole, solid;
        const float OpenScale = 60f, ClosedScale = 0.5f;

        public static Iris Get()
        {
            if (inst != null) return inst;
            var go = new GameObject("Iris");
            inst = go.AddComponent<Iris>();
            inst.Build();
            return inst;
        }

        void Build()
        {
            // 1024×1024 검정, 가운데 반지름 8px 투명 구멍 (가장자리 부드럽게). PPU 32 → 32유닛 폭, 구멍 0.5유닛
            int s = 1024, r = 8;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[s * s];
            int cx = s / 2, cy = s / 2;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    byte a = (byte)(d < r ? 0 : 255);   // 가장자리 딱딱하게 (Shelldiver 분석 10-13: 페더 없음)
                    px[y * s + x] = new Color32(0, 0, 0, a);
                }
            tex.SetPixels32(px); tex.Apply(false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 32f);
            hole = new GameObject("Hole").AddComponent<SpriteRenderer>();
            hole.transform.SetParent(transform, false);
            hole.sprite = sprite; hole.sortingOrder = 200;
            solid = Look.Quad("Solid", Vector2.zero, new Vector2(100f, 100f), Color.black, 199, transform);
            SetOpen(1f);
            gameObject.SetActive(false);
        }

        /// <summary>k = 0 닫힘 … 1 열림</summary>
        void SetOpen(float k)
        {
            float sc = Mathf.Lerp(ClosedScale, OpenScale, k * k);   // 닫힐 때 빨리 좁아진다
            hole.transform.localScale = Vector3.one * sc;
            hole.gameObject.SetActive(k > 0.02f);
            solid.gameObject.SetActive(k <= 0.02f);
        }

        /// <summary>구멍 중심을 카메라 중심에서 이만큼 옮긴다 (클릭한 자리에서 닫히게). Open/Close가 끝나면 0으로</summary>
        public Vector2 Offset;

        void LateUpdate()
        {
            var c = Camera.main; if (c == null) return;
            transform.position = new Vector3(c.transform.position.x + Offset.x, c.transform.position.y + Offset.y, 0f);
        }

        public IEnumerator Close(float seconds) => Close(seconds, null);

        /// <summary>곡선을 주면 구멍 크기를 로그 공간에서 그 곡선대로 줄인다 (크기는 곱으로 변해야 일정하게 느껴진다)</summary>
        public IEnumerator Close(float seconds, System.Func<float, float> ease)
        {
            gameObject.SetActive(true);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
            {
                if (ease == null) SetOpen(1f - t);
                else SetScale(Ease.LerpLog(OpenScale * 0.5f, ClosedScale * 0.6f, ease(t)));
                yield return null;
            }
            SetOpen(0f);
        }

        void SetScale(float sc)
        {
            hole.transform.localScale = Vector3.one * sc;
            hole.gameObject.SetActive(true);
            solid.gameObject.SetActive(false);
        }

        public IEnumerator Open(float seconds)
        {
            gameObject.SetActive(true);
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds) { SetOpen(t); yield return null; }
            SetOpen(1f);
            Offset = Vector2.zero;
            gameObject.SetActive(false);
        }

        /// <summary>닫힌 채로 시작 (씬이 새로 열렸을 때)</summary>
        public void StartClosed() { gameObject.SetActive(true); SetOpen(0f); }
    }
}
