using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 복셀 덩어리 → 메시. 보이는 면만 네모로 만들고, 면 방향에 따라 명암을 정점 색에 굽는다.
    /// (위 밝게, 앞뒤 보통, 옆 어둡게, 아래 더 어둡게) 빛 없이도 입체로 읽힌다.
    /// </summary>
    public class VoxelMesh
    {
        readonly Dictionary<Vector3Int, Color> vox = new Dictionary<Vector3Int, Color>();
        public float Size = 0.06f;   // 복셀 한 칸 (유닛)

        public void Set(int x, int y, int z, Color c) => vox[new Vector3Int(x, y, z)] = c;
        public void Box(int x0, int y0, int z0, int x1, int y1, int z1, Color c)
        {
            for (int x = Mathf.Min(x0, x1); x <= Mathf.Max(x0, x1); x++)
                for (int y = Mathf.Min(y0, y1); y <= Mathf.Max(y0, y1); y++)
                    for (int z = Mathf.Min(z0, z1); z <= Mathf.Max(z0, z1); z++) Set(x, y, z, c);
        }
        /// <summary>Y축 둘레 원기둥 (실린더·총열)</summary>
        public void CylinderY(int cx, int cz, int y0, int y1, float r, Color c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = Mathf.FloorToInt(cx - r); x <= Mathf.CeilToInt(cx + r); x++)
                    for (int z = Mathf.FloorToInt(cz - r); z <= Mathf.CeilToInt(cz + r); z++)
                        if ((x - cx) * (x - cx) + (z - cz) * (z - cz) <= r * r + 0.3f) Set(x, y, z, c);
        }
        /// <summary>X축 둘레 원기둥 (옆으로 누운 실린더)</summary>
        public void CylinderX(int cy, int cz, int x0, int x1, float r, Color c)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = Mathf.FloorToInt(cy - r); y <= Mathf.CeilToInt(cy + r); y++)
                    for (int z = Mathf.FloorToInt(cz - r); z <= Mathf.CeilToInt(cz + r); z++)
                        if ((y - cy) * (y - cy) + (z - cz) * (z - cz) <= r * r + 0.3f) Set(x, y, z, c);
        }
        public void Remove(int x, int y, int z) => vox.Remove(new Vector3Int(x, y, z));

        static readonly Vector3Int[] Dirs = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right, new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1) };
        static readonly float[] Shade = { 1.0f, 0.5f, 0.68f, 0.68f, 0.82f, 0.9f };

        public Mesh Build(Vector3 pivotVoxel)
        {
            var verts = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            foreach (var kv in vox)
            {
                var p = kv.Key; var c = kv.Value;
                for (int d = 0; d < 6; d++)
                {
                    if (vox.ContainsKey(p + Dirs[d])) continue;
                    AddFace(verts, cols, tris, (Vector3)p - pivotVoxel, Dirs[d], c * Shade[d]);
                }
            }
            var m = new Mesh();
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            for (int i = 0; i < verts.Count; i++) verts[i] = (verts[i]) * Size;
            m.SetVertices(verts); m.SetColors(cols); m.SetTriangles(tris, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static void AddFace(List<Vector3> v, List<Color> c, List<int> t, Vector3 p, Vector3Int n, Color col)
        {
            // 면의 네 모서리: 법선 n에 수직인 두 축
            Vector3 nn = n;
            Vector3 u = Mathf.Abs(nn.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 w = Vector3.Cross(nn, u).normalized; u = Vector3.Cross(w, nn).normalized;
            Vector3 center = p + Vector3.one * 0.5f + nn * 0.5f;
            int i0 = v.Count;
            v.Add(center + (-u - w) * 0.5f); v.Add(center + (u - w) * 0.5f); v.Add(center + (u + w) * 0.5f); v.Add(center + (-u + w) * 0.5f);
            col.a = 1f; c.Add(col); c.Add(col); c.Add(col); c.Add(col);
            // 바깥쪽을 보도록 감기 (법선 방향)
            Vector3 cross = Vector3.Cross(v[i0 + 1] - v[i0], v[i0 + 2] - v[i0]);
            if (Vector3.Dot(cross, nn) > 0f) { t.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 }); }
            else { t.AddRange(new[] { i0, i0 + 1, i0 + 2, i0, i0 + 2, i0 + 3 }); }
        }
    }
}
