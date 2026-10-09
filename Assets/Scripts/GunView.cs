using System.Collections;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 화면 아래에 보이는 총 (덕헌트식). 마우스를 좌우로 따라오고, 포인터 쪽으로 기울고, 손이 늦게 따라와 흔들린다.
    /// 쏘면: 총열 방향으로 뒤로 튀고 위로 꺾이고, 공이치기가 젖혀졌다 돌아오고, 총구 섬광 + 연기.
    /// 장전: 실린더가 옆으로 젖혀져 나오고, 탄피가 튀고, 실린더가 돌고, 탁 닫히며 살짝 출렁인다.
    /// 샷건: 쏘고 나면 펌프가 뒤로 당겨졌다 돌아온다.
    /// 지금은 도형. 그 자리에 사용자 그림이 들어간다.
    /// </summary>
    public class GunView : MonoBehaviour
    {
        const float RestY = -3.35f;
        const float FollowLerp = 14f;

        Transform root, pivot, body, barrelTip, cylinder, cylinderSwing, hammer, pump;
        Camera cam;
        float recoil, dip, swing, hammerBack, pumpBack, bob;
        Vector2 lagVel, lagPos;
        bool reloading;
        GunKind kind = GunKind.Revolver;

        public Vector2 Muzzle => barrelTip.position;

        public void Build()
        {
            cam = Camera.main;
            root = new GameObject("GunView").transform;
            root.position = new Vector3(0f, RestY, 0f);
            pivot = new GameObject("Pivot").transform; pivot.SetParent(root, false);
            body = new GameObject("Body").transform; body.SetParent(pivot, false);
            var dark = new Color(0.16f, 0.15f, 0.18f);
            var steel = new Color(0.27f, 0.26f, 0.31f);
            var steelLight = new Color(0.42f, 0.41f, 0.47f);
            var wood = new Color(0.45f, 0.28f, 0.16f);
            var woodLight = new Color(0.58f, 0.38f, 0.22f);
            var skin = new Color(0.95f, 0.78f, 0.64f);
            var skinDark = new Color(0.8f, 0.62f, 0.5f);
            // 손 (손잡이를 감싼 주먹)
            Look.Quad("Hand", new Vector2(0.06f, -0.62f), new Vector2(0.6f, 0.62f), skin, 40, body);
            Look.Quad("Thumb", new Vector2(-0.22f, -0.42f), new Vector2(0.18f, 0.3f), skinDark, 41, body);
            Look.Quad("Knuckle1", new Vector2(0.3f, -0.5f), new Vector2(0.14f, 0.12f), skinDark, 41, body);
            Look.Quad("Knuckle2", new Vector2(0.3f, -0.68f), new Vector2(0.14f, 0.12f), skinDark, 41, body);
            // 손잡이·몸통·총열
            Look.Quad("Grip", new Vector2(0.1f, -0.36f), new Vector2(0.3f, 0.6f), wood, 42, body);
            Look.Quad("GripLine", new Vector2(0.1f, -0.36f), new Vector2(0.06f, 0.5f), woodLight, 43, body);
            Look.Quad("Frame", new Vector2(0f, 0.05f), new Vector2(0.46f, 0.44f), steel, 42, body);
            Look.Quad("FrameTop", new Vector2(0f, 0.2f), new Vector2(0.46f, 0.1f), steelLight, 43, body);
            Look.Quad("Barrel", new Vector2(-0.04f, 0.72f), new Vector2(0.18f, 1.05f), steel, 42, body);
            Look.Quad("BarrelLight", new Vector2(-0.1f, 0.72f), new Vector2(0.05f, 1.0f), steelLight, 43, body);
            Look.Quad("Sight", new Vector2(-0.04f, 1.26f), new Vector2(0.06f, 0.1f), dark, 43, body);
            Look.Quad("TriggerGuard", new Vector2(0.14f, -0.12f), new Vector2(0.22f, 0.06f), dark, 43, body);
            Look.Quad("Trigger", new Vector2(0.12f, -0.1f), new Vector2(0.06f, 0.16f), dark, 44, body);
            // 공이치기 (뒤로 젖혀진다)
            hammer = new GameObject("Hammer").transform; hammer.SetParent(body, false); hammer.localPosition = new Vector2(0.22f, 0.2f);
            Look.Quad("HammerBit", new Vector2(0f, 0.08f), new Vector2(0.08f, 0.18f), dark, 44, hammer);
            // 실린더 (장전 때 옆으로 젖혀져 나온다)
            cylinderSwing = new GameObject("CylinderSwing").transform; cylinderSwing.SetParent(body, false); cylinderSwing.localPosition = new Vector2(-0.02f, 0.12f);
            cylinder = new GameObject("Cylinder").transform; cylinder.SetParent(cylinderSwing, false);
            Look.Quad("Cyl", Vector2.zero, new Vector2(0.32f, 0.32f), dark, 45, cylinder);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                Look.Quad("Chamber", new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.1f, new Vector2(0.05f, 0.05f), new Color(0.65f, 0.58f, 0.4f), 46, cylinder);
            }
            // 펌프 (샷건만 보인다)
            pump = new GameObject("Pump").transform; pump.SetParent(body, false); pump.localPosition = new Vector2(-0.04f, 0.55f);
            Look.Quad("PumpBit", Vector2.zero, new Vector2(0.26f, 0.3f), wood, 44, pump);
            pump.gameObject.SetActive(false);
            barrelTip = new GameObject("Tip").transform; barrelTip.SetParent(body, false); barrelTip.localPosition = new Vector2(-0.04f, 1.26f);
            root.localScale = Vector3.one * 1.35f;
        }

        public void SetKind(GunKind k)
        {
            kind = k;
            pump.gameObject.SetActive(k.HitAll);          // 샷건이면 펌프
            cylinderSwing.gameObject.SetActive(!k.HitAll);
            hammer.gameObject.SetActive(!k.HitAll);
        }

        /// <summary>쏘았다: 뒤로 튀고, 공이치기, 총구 섬광 + 연기 + 궤적</summary>
        public void Fire(Vector2 aim, float kick)
        {
            recoil = Mathf.Min(1.6f, recoil + 1f * kick);
            hammerBack = 1f;
            if (kind.HitAll) StartCoroutine(Pump());
            StartCoroutine(Flash(aim, kick));
        }

        IEnumerator Pump()
        {
            yield return new WaitForSeconds(0.08f);
            for (float t = 0f; t < 1f; t += Time.deltaTime * 6f) { pumpBack = Mathf.Sin(t * Mathf.PI) * 0.22f; yield return null; }
            pumpBack = 0f;
            Sfx.Play(Sfx.Click(), 0.6f, 0.7f);
        }

        IEnumerator Flash(Vector2 aim, float kick)
        {
            var tip = (Vector2)barrelTip.position;
            var dir = (aim - tip).normalized;
            float size = 0.4f + kick * 0.25f;
            var f = Look.Quad("Muzzle", tip + dir * 0.15f, new Vector2(size, size), new Color(1f, 0.95f, 0.6f, 0.95f), 48);
            f.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            var f2 = Look.Quad("Muzzle2", tip + dir * 0.3f, new Vector2(size * 1.6f, size * 0.5f), new Color(1f, 0.8f, 0.3f, 0.8f), 47);
            f2.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            var d = aim - tip;
            var tracer = Look.Quad("Tracer", (tip + aim) * 0.5f, new Vector2(d.magnitude, 0.03f), new Color(1f, 0.95f, 0.7f, 0.55f), 47);
            tracer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            // 연기: 작은 회색 조각 셋이 위로 흩어지며 사라진다
            for (int i = 0; i < 3; i++) StartCoroutine(Smoke(tip + dir * 0.2f + Random.insideUnitCircle * 0.08f, dir));
            yield return null; yield return null;
            Destroy(f.gameObject); Destroy(f2.gameObject); Destroy(tracer.gameObject);
        }

        IEnumerator Smoke(Vector2 at, Vector2 dir)
        {
            var s = Look.Quad("Smoke", at, Vector2.one * 0.12f, new Color(0.9f, 0.9f, 0.9f, 0.5f), 46);
            var vel = dir * Random.Range(0.6f, 1.2f) + Vector2.up * 0.8f + Random.insideUnitCircle * 0.3f;
            for (float t = 0f; t < 1f; t += Time.deltaTime * 2.2f)
            {
                s.transform.position += (Vector3)(vel * Time.deltaTime);
                s.transform.localScale = Vector3.one * (0.12f + t * 0.3f);
                s.color = new Color(0.9f, 0.9f, 0.9f, 0.5f * (1f - t));
                yield return null;
            }
            Destroy(s.gameObject);
        }

        /// <summary>장전: 실린더 젖혀 빼고 → 탄피 → 실린더 돌고 → 탁 닫힘. seconds 동안</summary>
        public void Reload(float seconds, int shells) => StartCoroutine(ReloadCo(seconds, shells));

        IEnumerator ReloadCo(float seconds, int shells)
        {
            reloading = true;
            float t = 0f; bool spat = false;
            while (t < seconds && reloading)
            {
                t += Time.deltaTime;
                float k = t / seconds;
                dip = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI) * 0.7f;                 // 내려갔다 올라온다
                swing = Mathf.Sin(Mathf.Clamp01(k * 1.15f) * Mathf.PI) * 0.42f;      // 실린더가 옆으로 나왔다 들어간다
                cylinder.localRotation = Quaternion.Euler(0f, 0f, k * 900f);
                if (!spat && k > 0.2f) { spat = true; Shells(Mathf.Min(shells, 6)); }
                yield return null;
            }
            if (reloading) Snap();
            reloading = false;
        }

        /// <summary>저스트 장전 성공: 즉시 닫힌다</summary>
        public void ReloadSnap() { reloading = false; Snap(); }

        void Snap()
        {
            dip = 0f; swing = 0f; cylinder.localRotation = Quaternion.identity;
            recoil = Mathf.Max(recoil, 0.45f);   // 탁 닫히는 출렁임
            hammerBack = 0.6f;
        }

        void Shells(int n)
        {
            for (int i = 0; i < n; i++)
            {
                var s = Look.Quad("Shell", (Vector2)cylinder.position, new Vector2(0.06f, 0.13f), new Color(0.85f, 0.7f, 0.3f), 46);
                var rb = s.gameObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 2f;
                rb.linearVelocity = new Vector2(Random.Range(-2f, 0.5f), Random.Range(2.5f, 4.5f));
                rb.angularVelocity = Random.Range(-700f, 700f);
                StartCoroutine(ShellFade(s));
            }
        }

        IEnumerator ShellFade(SpriteRenderer s)
        {
            yield return new WaitForSeconds(0.7f);
            for (float t = 0f; t < 1f; t += Time.deltaTime * 3f) { if (s == null) yield break; s.color = new Color(0.85f, 0.7f, 0.3f, 1f - t); yield return null; }
            if (s != null) Destroy(s.gameObject);
        }

        public void Tick(Vector2 aim, bool enabled)
        {
            root.gameObject.SetActive(enabled);
            if (!enabled) return;
            float dt = Time.deltaTime;
            // 좌우 따라오기 (조금 늦게)
            float half = cam.orthographicSize * cam.aspect - 1.2f;
            float tx = Mathf.Clamp(aim.x * 0.55f, -half, half);
            var p = root.position;
            float prevX = p.x;
            p.x = Mathf.Lerp(p.x, tx, 1f - Mathf.Exp(-dt * FollowLerp));
            bob += dt * 2f;
            p.y = RestY - dip + Mathf.Sin(bob) * 0.02f;
            root.position = p;
            // 손이 늦게 따라와 흔들린다 (속도 반대쪽으로 끌림)
            float vx = (p.x - prevX) / Mathf.Max(dt, 0.0001f);
            var lagTarget = new Vector2(-vx * 0.02f, 0f);
            lagPos = Vector2.SmoothDamp(lagPos, lagTarget, ref lagVel, 0.1f);
            // 포인터 쪽으로 기운다 (한계 40도)
            var d = aim - (Vector2)pivot.position;
            float ang = Mathf.Clamp(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f, -40f, 40f);
            recoil = Mathf.Max(0f, recoil - dt * 6f);
            hammerBack = Mathf.Max(0f, hammerBack - dt * 5f);
            float lean = -Mathf.Sign(ang == 0f ? 1f : ang);
            pivot.localRotation = Quaternion.Euler(0f, 0f, ang + recoil * 14f * lean);
            // 몸통: 총열 방향으로 뒤로 밀린다 + 손 흔들림 + 장전 때 옆으로 기움
            body.localPosition = new Vector3(lagPos.x, -recoil * 0.3f, 0f);
            body.localRotation = Quaternion.Euler(0f, 0f, swing * 30f);
            cylinderSwing.localPosition = new Vector3(-0.02f - swing, 0.12f, 0f);
            hammer.localRotation = Quaternion.Euler(0f, 0f, -hammerBack * 45f);
            pump.localPosition = new Vector3(-0.04f, 0.55f - pumpBack, 0f);
        }
    }
}
