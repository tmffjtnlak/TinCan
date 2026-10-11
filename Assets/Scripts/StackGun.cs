using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 스프라이트 스태킹 총 (사용자 10-11): 총열 방향으로 썬 단면 도트 스프라이트를 어긋나게 쌓아 입체로 보인다.
    /// 눕힘(Lay) = 단면마다 같은 각도로 회전 (뒷면 ↔ 옆면). 돌림(Spin) = 쌓인 덩어리를 손가락 축으로 2D 회전.
    /// 기울기(Tilt) = 단면 간격 (크면 옆에서 보는 느낌, 작으면 총열을 들여다보는 느낌).
    /// 피벗 = 방아쇠울 (y = -4 단면 자리).
    /// </summary>
    public class StackGun : MonoBehaviour
    {
        public float Lay, Spin, Tilt = 55f;
        public Transform Muzzle { get; private set; }
        readonly List<SpriteRenderer> slices = new List<SpriteRenderer>();
        readonly List<int> ys = new List<int>();
        float size;
        const int PivotY = -4;
        int baseOrder = 42;

        public static StackGun Make(Transform parent, int order = 42)
        {
            var go = new GameObject("StackGun");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<StackGun>();
            g.baseOrder = order;
            g.Build(VoxelGun.RevolverModel());
            return g;
        }

        void Build(VoxelMesh v)
        {
            size = v.Size;
            foreach (var (y, sp) in v.SlicesY())
            {
                var sgo = new GameObject($"Slice{y}");
                sgo.transform.SetParent(transform, false);
                var sr = sgo.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                slices.Add(sr); ys.Add(y);
            }
            Muzzle = new GameObject("Muzzle").transform; Muzzle.SetParent(transform, false);
            Apply();
        }

        public void Apply()
        {
            // 총열(+Y)을 따라 단면이 위로 쌓인다. 간격 = 한 칸 × sin(기울기): 기울기 90 = 완전 옆에서(단면이 한 칸씩), 0 = 총열 정면
            float step = size * Mathf.Sin(Tilt * Mathf.Deg2Rad);
            int maxY = int.MinValue;
            for (int i = 0; i < slices.Count; i++)
            {
                int y = ys[i];
                var sr = slices[i];
                sr.transform.localPosition = new Vector3(0f, (y - PivotY) * step, 0f);
                sr.transform.localRotation = Quaternion.Euler(0f, 0f, Lay);          // 눕힘: 단면 회전
                // 단면 안의 세로(z)는 기울기에 따라 눌린다 (정면에 가까울수록 단면이 통째로 보이고, 옆이면 얇은 띠)
                sr.transform.localScale = new Vector3(1f, Mathf.Max(0.08f, Mathf.Cos(Tilt * Mathf.Deg2Rad) * 1.0f + 0.15f), 1f);
                sr.sortingOrder = baseOrder - (y - PivotY);                           // 손잡이(아래)가 앞, 총구(위)가 뒤
                if (y > maxY) maxY = y;
            }
            Muzzle.localPosition = new Vector3(0f, (maxY - PivotY) * step, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, Spin);               // 돌림: 덩어리째
        }
    }
}
