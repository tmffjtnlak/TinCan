using System.Collections;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 원형 페이드: 검은 화면 가운데 구멍이 줄어들어 닫히고, 다시 커지며 열린다.
    /// 2판 (사용자 10-13 "게임 도트에 맞춰서"): 화면을 덮는 사각형 하나 + Iris 셰이더. 화면을 480×270 도트 칸으로 나눠
    /// 칸 단위로 원 안/밖을 정하므로, 카메라가 줌해도 원의 계단은 늘 게임 도트 한 칸이다. 가장자리는 딱 끊김.
    /// </summary>
    public class Iris : MonoBehaviour
    {
        static Iris inst;
        const float Rows = 270f;                 // 화면 세로 도트 수 (480×270)
        MeshRenderer quad; Material mat;
        float radius = 9999f;                    // 도트 단위

        /// <summary>구멍 중심을 카메라 중심에서 이만큼(월드 단위) 옮긴다. Open이 끝나면 0으로</summary>
        public Vector2 Offset;

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
            mat = new Material(Shader.Find("TinCan/Iris"));
            mat.SetFloat("_Rows", Rows);
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = "IrisQuad"; q.transform.SetParent(transform, false);
            quad = q.GetComponent<MeshRenderer>(); quad.sharedMaterial = mat; quad.sortingOrder = 200;
            SetRadius(9999f);
            gameObject.SetActive(false);
        }

        /// <summary>화면을 다 덮는 반지름 (도트) — 구멍 중심이 옮겨진 만큼 더</summary>
        float OpenRadius()
        {
            var c = Camera.main; float aspect = c != null ? c.aspect : 16f / 9f;
            float halfDiag = 0.5f * Mathf.Sqrt(Rows * Rows * aspect * aspect + Rows * Rows);
            float off = c != null ? Offset.magnitude / (c.orthographicSize * 2f) * Rows : 0f;
            return halfDiag + off + 2f;
        }

        void SetRadius(float r) { radius = r; if (mat != null) mat.SetFloat("_Radius", r); }

        /// <summary>k = 0 닫힘 … 1 열림 (옛 호출용)</summary>
        void SetOpen(float k) => SetRadius(k <= 0.001f ? -1f : Mathf.Lerp(0f, OpenRadius(), k * k));

        void LateUpdate()
        {
            var c = Camera.main; if (c == null) return;
            // 카메라 앞을 꽉 덮는다 (줌으로 크기가 바뀌어도 매 프레임 맞춤)
            float h = c.orthographicSize * 2f, w = h * c.aspect;
            transform.position = new Vector3(c.transform.position.x, c.transform.position.y, 0f);
            quad.transform.localScale = new Vector3(w * 1.02f, h * 1.02f, 1f);
            var vp = c.WorldToViewportPoint(c.transform.position + (Vector3)Offset);
            mat.SetVector("_Center", new Vector4(vp.x, vp.y, 0f, 0f));
        }

        public IEnumerator Close(float seconds) => Close(seconds, null);

        /// <summary>닫힘. 곡선을 주면 반지름을 로그 공간에서 그 곡선대로 줄인다 (크기는 곱으로 변해야 일정하게 느껴진다)</summary>
        public IEnumerator Close(float seconds, System.Func<float, float> ease)
        {
            gameObject.SetActive(true);
            float r0 = OpenRadius();
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
            {
                if (ease == null) SetOpen(1f - t);
                else SetRadius(Ease.LerpLog(r0, 0.8f, ease(t)));
                LateUpdate();
                yield return null;
            }
            SetRadius(-1f);   // 완전히 검정
        }

        /// <summary>열림: 한 칸에서 시작해 확 퍼졌다 느려지며 화면 끝에 닿는다 (로그 공간 OutCubic)</summary>
        public IEnumerator Open(float seconds)
        {
            gameObject.SetActive(true);
            float r1 = OpenRadius();
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / seconds)
            {
                SetRadius(Ease.LerpLog(0.8f, r1, Ease.OutCubic(t)));
                LateUpdate();
                yield return null;
            }
            SetRadius(9999f);
            Offset = Vector2.zero;
            gameObject.SetActive(false);
        }

        /// <summary>닫힌 채로 시작 (씬이 새로 열렸을 때)</summary>
        public void StartClosed() { gameObject.SetActive(true); SetRadius(-1f); }
    }
}
