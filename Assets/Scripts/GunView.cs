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

        Transform root, pivot, body, hand, barrelTip, cylinder, cylinderSwing, hammer, pump;
        Camera cam;
        float recoil, dip, swing, hammerBack, pumpBack, bob;
        Vector2 lagVel, lagPos;
        bool reloading;
        GunKind kind = GunKind.Revolver;

        public Vector2 Muzzle => vox != null ? (Vector2)vox.Muzzle.position : (Vector2)barrelTip.position;

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
            // 손 (손잡이를 감싼 주먹) — 총을 돌릴 때 손은 안 돈다. 손가락 하나에 총이 걸려 도는 그림
            hand = new GameObject("Hand").transform; hand.SetParent(pivot, false);
            Look.Quad("Palm", new Vector2(0.06f, -0.62f), new Vector2(0.6f, 0.62f), skin, 40, hand);
            Look.Quad("Thumb", new Vector2(-0.22f, -0.42f), new Vector2(0.18f, 0.3f), skinDark, 41, hand);
            Look.Quad("Knuckle1", new Vector2(0.3f, -0.5f), new Vector2(0.14f, 0.12f), skinDark, 41, hand);
            Look.Quad("Knuckle2", new Vector2(0.3f, -0.68f), new Vector2(0.14f, 0.12f), skinDark, 41, hand);
            Look.Quad("Finger", new Vector2(0.12f, -0.14f), new Vector2(0.12f, 0.2f), skin, 45, hand);   // 방아쇠에 건 손가락 (총 위에 보인다)
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
            // 옆면 (장전 때만 보인다): 총열이 오른쪽, 손잡이가 아래. 손가락(방아쇠울)이 원점 근처
            side = new GameObject("Side").transform; side.SetParent(pivot, false);
            side.localPosition = new Vector2(0.12f, -0.1f);
            Look.Quad("SBarrel", new Vector2(0.62f, 0.34f), new Vector2(1.0f, 0.16f), steel, 42, side);
            Look.Quad("SBarrelLight", new Vector2(0.62f, 0.38f), new Vector2(0.95f, 0.05f), steelLight, 43, side);
            Look.Quad("SFrame", new Vector2(0.05f, 0.26f), new Vector2(0.5f, 0.38f), steel, 42, side);
            Look.Quad("SCyl", new Vector2(0.08f, 0.28f), new Vector2(0.34f, 0.34f), dark, 44, side);
            Look.Quad("SCylDot1", new Vector2(0.0f, 0.28f), new Vector2(0.06f, 0.06f), new Color(0.65f, 0.58f, 0.4f), 45, side);
            Look.Quad("SCylDot2", new Vector2(0.16f, 0.28f), new Vector2(0.06f, 0.06f), new Color(0.65f, 0.58f, 0.4f), 45, side);
            Look.Quad("SHammer", new Vector2(-0.24f, 0.46f), new Vector2(0.08f, 0.16f), dark, 43, side);
            var sGrip = Look.Quad("SGrip", new Vector2(-0.2f, -0.16f), new Vector2(0.26f, 0.5f), wood, 42, side);
            sGrip.transform.localRotation = Quaternion.Euler(0f, 0f, 22f);
            Look.Quad("STriggerGuard", new Vector2(0.08f, -0.02f), new Vector2(0.22f, 0.06f), dark, 43, side);
            Look.Quad("STrigger", new Vector2(0.06f, 0.04f), new Vector2(0.05f, 0.12f), dark, 44, side);
            side.gameObject.SetActive(false);
            root.localScale = Vector3.one * 1.35f;
            // 복셀 총 (시제품): 도형 총 대신. 손가락 축 자리에 놓는다
            if (UseVoxel)
            {
                vox = VoxelGun.Make(pivot);
                vox.transform.localPosition = new Vector3(0.12f, -0.1f, 0f);
                body.gameObject.SetActive(false);
            }
        }

        public bool UseVoxel = true;
        VoxelGun vox;

        Transform side;
        float sideShow;     // 0 = 뒷면만, 1 = 옆면만 (사이는 납작하게 넘어가는 중)
        float sideSpin;     // 옆면이 돈 각도
        float handTilt;     // 장전 때 손 기울기

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
            var tip = Muzzle;
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

        /// <summary>장전: 총잡이식 — 손가락에 걸고 총을 촤라라락 돌린다. 돌면서 탄피가 튀고 실린더가 찬다. 마지막에 탁 잡는다</summary>
        public void Reload(float seconds, int shells) => StartCoroutine(ReloadCo(seconds, shells));

        float twirl;   // 총이 돈 각도 (도)

        /// <summary>
        /// 1) 손을 기울이며 총이 납작해졌다 옆면으로 펴진다 (뒷면 → 옆면)
        /// 2) 옆면이 손가락에 걸려 원을 그리며 돈다. 돌면서 탄피
        /// 3) 옆면이 납작해졌다 뒷면으로 펴지고 손이 제자리. 탁.
        /// </summary>
        IEnumerator ReloadCo(float seconds, int shells)
        {
            reloading = true;
            float flip = Mathf.Min(0.12f, seconds * 0.15f);
            float spinTime = Mathf.Max(0.2f, seconds - flip * 2f);
            int spins = spinTime > 0.6f ? 3 : 2;
            // 1) 뒷면 → 옆면
            for (float t = 0f; t < 1f && reloading; t += Time.deltaTime / flip) { sideShow = t; handTilt = t; yield return null; }
            sideShow = 1f; handTilt = 1f;
            // 2) 돈다
            Sfx.Play(Sfx.Twirl(), 0.7f, Mathf.Clamp(0.7f / spinTime, 0.6f, 2.5f));
            float s = 0f; bool spat = false;
            while (s < spinTime && reloading)
            {
                s += Time.deltaTime;
                float k = Mathf.Clamp01(s / spinTime);
                float e = 1f - Mathf.Pow(1f - k, 2.4f);                // 빠르게 돌다 느려진다
                sideSpin = -360f * spins * e;
                if (!spat && k > 0.2f) { spat = true; Shells(Mathf.Min(shells, 6)); }
                yield return null;
            }
            // 3) 옆면 → 뒷면
            for (float t = 1f; t > 0f && reloading; t -= Time.deltaTime / flip) { sideShow = t; handTilt = t; yield return null; }
            if (reloading) Snap();
            reloading = false;
        }

        /// <summary>저스트 장전 성공: 즉시 탁 잡는다</summary>
        public void ReloadSnap() { reloading = false; Snap(); }

        void Snap()
        {
            dip = 0f; swing = 0f; twirl = 0f; sideShow = 0f; sideSpin = 0f; handTilt = 0f; cylinder.localRotation = Quaternion.identity; body.localScale = Vector3.one;
            recoil = Mathf.Max(recoil, 0.55f);   // 탁 잡는 출렁임
            hammerBack = 0.8f;
        }

        void Shells(int n)
        {
            for (int i = 0; i < n; i++)
            {
                var s = Look.Quad("Shell", vox != null ? (Vector2)vox.transform.position + Vector2.up * 0.4f : (Vector2)cylinder.position, new Vector2(0.06f, 0.13f), new Color(0.85f, 0.7f, 0.3f), 46);
                var rb = s.gameObject.AddComponent<Rigidbody2D>();
                rb.gravityScale = 2f;
                // 돌던 방향으로 튕겨 나간다
                var tangent = (Vector2)(body.rotation * Vector2.right) * -1f;
                rb.linearVelocity = tangent * Random.Range(2f, 4f) + new Vector2(Random.Range(-1f, 1f), Random.Range(2f, 4f));
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
            if (vox != null)
            {
                // 복셀: 눕힘 = 옆면 보이기(0→90), 돌림 = 장전 회전, 반동은 총열이 위로 들린다
                vox.Lay = sideShow * 90f;
                vox.Spin = sideSpin;
                vox.Tilt = 28f - recoil * 14f;
                vox.Apply();
                vox.transform.localPosition = new Vector3(0.12f + lagPos.x, -0.1f - recoil * 0.3f, 0f);
                side.gameObject.SetActive(false);
                hand.localPosition = new Vector3(lagPos.x, -recoil * 0.3f + handTilt * 0.06f, 0f);
                hand.localRotation = Quaternion.Euler(0f, 0f, swing * 30f - handTilt * 28f);
                return;
            }
            // 뒷면: sideShow가 0.5까지는 가로로 납작해지며 사라진다. 옆면: 0.5부터 펴진다
            float backW = Mathf.Clamp01(1f - sideShow * 2f);
            float sideW = Mathf.Clamp01(sideShow * 2f - 1f);
            body.gameObject.SetActive(backW > 0.01f);
            body.localRotation = Quaternion.Euler(0f, 0f, swing * 30f);
            body.localScale = new Vector3(Mathf.Max(0.02f, backW), 1f, 1f);
            body.localPosition = new Vector3(lagPos.x, -recoil * 0.3f, 0f);
            // 옆면: 손가락(원점)을 축으로 돈다. 뒷면과 손은 돌지 않는다
            side.gameObject.SetActive(sideW > 0.01f);
            side.localRotation = Quaternion.Euler(0f, 0f, sideSpin);
            side.localScale = new Vector3(Mathf.Max(0.02f, sideW), 1f, 1f);
            side.localPosition = new Vector3(0.12f + lagPos.x, -0.1f - recoil * 0.3f, 0f);
            // 손: 장전 때 기울어지며(총을 옆으로 눕히는 느낌) 살짝 들린다
            hand.localPosition = new Vector3(lagPos.x, -recoil * 0.3f + handTilt * 0.06f, 0f);
            hand.localRotation = Quaternion.Euler(0f, 0f, swing * 30f - handTilt * 28f);
            cylinderSwing.localPosition = new Vector3(-0.02f - swing, 0.12f, 0f);
            hammer.localRotation = Quaternion.Euler(0f, 0f, -hammerBack * 45f);
            pump.localPosition = new Vector3(-0.04f, 0.55f - pumpBack, 0f);
        }
    }
}
