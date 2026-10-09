using System.Collections;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 화면 아래에 보이는 총 (덕헌트식). 마우스를 좌우로 따라오고, 포인터 쪽으로 기울고,
    /// 쏘면 뒤로 튀며 총구 섬광, 장전하면 내려가 실린더가 돌고 탄피를 뱉고 올라온다.
    /// 지금은 도형. 그 자리에 사용자 그림이 들어간다 (부품: 몸통·총열·실린더·손잡이·손).
    /// </summary>
    public class GunView : MonoBehaviour
    {
        const float RestY = -3.35f;         // 총이 머무는 높이 (낙하지역 위)
        const float FollowLerp = 14f;

        Transform root, pivot, barrelTip, cylinder;
        SpriteRenderer[] parts;
        Camera cam;
        float recoil, dip;
        bool reloading;
        Color tint = new Color(0.25f, 0.24f, 0.28f);

        public Vector2 Muzzle => barrelTip.position;

        public void Build()
        {
            cam = Camera.main;
            root = new GameObject("GunView").transform;
            root.position = new Vector3(0f, RestY, 0f);
            pivot = new GameObject("Pivot").transform; pivot.SetParent(root, false);
            var dark = new Color(0.16f, 0.15f, 0.18f);
            var wood = new Color(0.45f, 0.28f, 0.16f);
            var skin = new Color(0.95f, 0.78f, 0.64f);
            // 총은 위(+Y)를 겨눈다. pivot을 돌려서 조준.
            parts = new[]
            {
                Look.Quad("Hand", new Vector2(0.05f, -0.55f), new Vector2(0.55f, 0.6f), skin, 40, pivot),
                Look.Quad("Grip", new Vector2(0.08f, -0.35f), new Vector2(0.28f, 0.55f), wood, 41, pivot),
                Look.Quad("Frame", new Vector2(0f, 0.05f), new Vector2(0.42f, 0.42f), tint, 42, pivot),
                Look.Quad("Barrel", new Vector2(-0.04f, 0.7f), new Vector2(0.16f, 1.0f), tint, 42, pivot),
                Look.Quad("Sight", new Vector2(-0.04f, 1.22f), new Vector2(0.06f, 0.1f), dark, 43, pivot),
                Look.Quad("Trigger", new Vector2(0.1f, -0.1f), new Vector2(0.08f, 0.16f), dark, 43, pivot),
            };
            cylinder = new GameObject("Cylinder").transform; cylinder.SetParent(pivot, false); cylinder.localPosition = new Vector2(-0.02f, 0.12f);
            Look.Quad("Cyl", Vector2.zero, new Vector2(0.3f, 0.3f), dark, 44, cylinder);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                Look.Quad("Chamber", new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.09f, new Vector2(0.05f, 0.05f), new Color(0.6f, 0.55f, 0.4f), 45, cylinder);
            }
            barrelTip = new GameObject("Tip").transform; barrelTip.SetParent(pivot, false); barrelTip.localPosition = new Vector2(-0.04f, 1.22f);
            root.localScale = Vector3.one * 1.35f;
        }

        public void SetVisible(bool v) => root.gameObject.SetActive(v);

        /// <summary>쏘았다: 뒤로 튀고 총구 섬광 + 총알 궤적 한 프레임</summary>
        public void Fire(Vector2 aim, float kick)
        {
            recoil = 1f * kick;
            StartCoroutine(Flash(aim));
        }

        IEnumerator Flash(Vector2 aim)
        {
            var tip = (Vector2)barrelTip.position;
            var f = Look.Quad("Muzzle", tip, new Vector2(0.45f, 0.45f), new Color(1f, 0.95f, 0.6f, 0.95f), 48);
            f.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            var d = aim - tip;
            var tracer = Look.Quad("Tracer", (tip + aim) * 0.5f, new Vector2(d.magnitude, 0.03f), new Color(1f, 0.95f, 0.7f, 0.6f), 47);
            tracer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            yield return null; yield return null;
            Destroy(f.gameObject); Destroy(tracer.gameObject);
        }

        /// <summary>장전: 내려가서 실린더 돌리고 탄피 뱉고 올라온다. seconds 동안</summary>
        public void Reload(float seconds, int shells) => StartCoroutine(ReloadCo(seconds, shells));

        IEnumerator ReloadCo(float seconds, int shells)
        {
            reloading = true;
            float t = 0f;
            bool spat = false;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = t / seconds;
                dip = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.9f;            // 내려갔다 올라온다
                cylinder.localRotation = Quaternion.Euler(0f, 0f, k * 720f);   // 실린더 돈다
                if (!spat && k > 0.25f) { spat = true; Shells(Mathf.Min(shells, 6)); }
                yield return null;
            }
            dip = 0f; cylinder.localRotation = Quaternion.identity;
            reloading = false;
        }

        void Shells(int n)
        {
            for (int i = 0; i < n; i++)
            {
                var s = Look.Quad("Shell", (Vector2)cylinder.position, new Vector2(0.06f, 0.12f), new Color(0.85f, 0.7f, 0.3f), 46);
                var rb = s.gameObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 2f;
                rb.linearVelocity = new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(2.5f, 4.5f));
                rb.angularVelocity = Random.Range(-600f, 600f);
                Destroy(s.gameObject, 1.2f);
            }
        }

        public void Tick(Vector2 aim, bool enabled)
        {
            root.gameObject.SetActive(enabled);
            if (!enabled) return;
            // 좌우 따라오기 (조금 늦게), 아래에 머문다
            float half = cam.orthographicSize * cam.aspect - 1.2f;
            float tx = Mathf.Clamp(aim.x * 0.55f, -half, half);
            var p = root.position;
            p.x = Mathf.Lerp(p.x, tx, 1f - Mathf.Exp(-Time.deltaTime * FollowLerp));
            p.y = RestY - dip;
            root.position = p;
            // 포인터 쪽으로 기운다
            var d = aim - (Vector2)pivot.position;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
            ang = Mathf.Clamp(ang, -55f, 55f);
            recoil = Mathf.Max(0f, recoil - Time.deltaTime * 7f);
            pivot.localRotation = Quaternion.Euler(0f, 0f, ang - recoil * 18f * Mathf.Sign(ang == 0f ? 1f : -ang));
            pivot.localPosition = new Vector2(0f, -recoil * 0.25f);
        }
    }
}
