using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 복셀 리볼버 (코드로 쌓은 시제품). 어느 각도든 보여 준다.
    /// 모델 좌표: 총열 = +Y(화면 위), 손잡이 = -Y, 옆면 = ±X, 카메라 쪽 = -Z.
    /// 원점(피벗) = 방아쇠울 (손가락이 걸리는 자리). 회전은 전부 이 점 둘레.
    /// 자세 = 기울기(위에서 살짝 보이게) × 눕힘(총열 축 둘레: 옆면이 보이게) × 돌림(손가락 축 둘레: 장전 회전).
    /// </summary>
    public class VoxelGun : MonoBehaviour
    {
        public float Tilt = 28f;     // X축: 윗면이 살짝 보이게
        public float Lay;            // Y축(총열 축): 0 = 뒷면, 90 = 옆면
        public float Spin;           // X축(손가락 축): 장전 회전
        public Transform Muzzle { get; private set; }
        public Transform Cylinder { get; private set; }   // 실린더 (장전 때 돈다)
        MeshFilter mf;

        public static VoxelGun Make(Transform parent)
        {
            var go = new GameObject("VoxelGun");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<VoxelGun>();
            g.Build();
            return g;
        }

        static Material mat;
        static Material Mat() => mat != null ? mat : (mat = new Material(Shader.Find("TinCan/VoxelUnlit")));

        /// <summary>리볼버 복셀 모델 (스태킹도 같은 모델을 쓴다)</summary>
        public static VoxelMesh RevolverModel()
        {
            var v = new VoxelMesh { Size = 0.045f };
            FillRevolver(v);
            return v;
        }

        void Build()
        {
            var v = RevolverModel();
            BuildFrom(v);
        }

        static void FillRevolver(VoxelMesh v)
        {
            var steel = new Color(0.3f, 0.29f, 0.34f);
            var steelDark = new Color(0.18f, 0.17f, 0.2f);
            var wood = new Color(0.5f, 0.3f, 0.17f);
            var brass = new Color(0.75f, 0.65f, 0.35f);
            // 격자: x 좌우(-3..3), y 아래위(-12..25), z 앞뒤(-5..3)
            // 손잡이: 아래로, 살짝 뒤로 기울어진 나무
            for (int y = -12; y <= -2; y++) { int back = (int)Mathf.Round((-2 - y) * 0.35f); v.Box(-2, y, -1 + back, 2, y, 2 + back, wood); }
            v.Box(-1, -12, 0, 1, -11, 3, steelDark);   // 손잡이 바닥 쇠
            // 몸통(프레임)
            v.Box(-2, -2, -2, 2, 4, 3, steel);
            v.Box(-3, 1, -1, 3, 3, 2, steel);
            // 방아쇠울·방아쇠 (원점 근처 아래)
            v.Box(-1, -5, -1, 1, -5, 2, steelDark);
            v.Box(-1, -4, -1, -1, -3, 2, steelDark); v.Box(1, -4, -1, 1, -3, 2, steelDark);
            v.Box(0, -4, 0, 0, -3, 0, brass);
            // 실린더: 총열 축(Y) 둘레 원기둥, 몸통 위
            v.CylinderY(0, 0, 4, 9, 3.2f, steelDark);
            for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2f; int cx = Mathf.RoundToInt(Mathf.Cos(a) * 2f), cz = Mathf.RoundToInt(Mathf.Sin(a) * 2f); v.Box(cx, 4, cz, cx, 9, cz, brass); }
            // 총열: 위로 길게, 가운데 구멍
            v.CylinderY(0, 0, 10, 24, 1.6f, steel);
            v.Box(0, 23, 0, 0, 24, 0, steelDark);
            v.Remove(0, 24, 0);
            // 가늠쇠·가늠자
            v.Box(0, 25, -1, 0, 25, -1, steelDark);
            v.Box(-1, 5, -3, 1, 5, -3, steelDark);
            // 공이치기: 몸통 뒤(카메라 쪽) 위
            v.Box(-1, 3, -4, 1, 5, -3, steelDark);
            v.Box(0, 5, -5, 0, 6, -4, steelDark);
        }

        void BuildFrom(VoxelMesh v)
        {
            // 피벗 = 방아쇠울 (0,-4,0)
            var mesh = v.Build(new Vector3(0f, -4f, 0f));
            mf = gameObject.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>(); mr.sharedMaterial = Mat(); mr.sortingOrder = 42;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            Muzzle = new GameObject("Muzzle").transform; Muzzle.SetParent(transform, false); Muzzle.localPosition = new Vector3(0f, (25 + 4) * v.Size, 0f);
            Cylinder = transform;
            PixelLayer.Mark(gameObject);   // 도트처럼 보이게: 낮은 해상도 층에서 그린다
            Apply();
        }

        public void Apply()
        {
            // 적용 순서: 먼저 손가락 축(X) 돌림 → 총열 축(Y) 눕힘 → 기울기(X)
            transform.localRotation = Quaternion.Euler(Tilt, 0f, 0f) * Quaternion.AngleAxis(Lay, Vector3.up) * Quaternion.AngleAxis(Spin, Vector3.right);
        }
    }
}
