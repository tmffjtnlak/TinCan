using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 자동 사격기 (스킬). 선반 오른쪽 끝에 서 있는 기계.
    /// 화면 안에 보이는 공중 깡통 하나를 고르고, 초록 조준점이 그 깡통으로 **움직여 가서**, 맞물리면 쏜다 (값 절반).
    /// 사용자 10-10: 보이는 영역에서만, 조준이 실제로 등장하고 움직이면서 사격.
    /// </summary>
    public class AutoTurret : MonoBehaviour
    {
        public float Interval = 2f;         // 쏜 뒤 다음 조준까지
        const float AimSpeed = 9f;          // 조준점이 따라가는 속도
        const float LockDist = 0.25f;       // 이 안에 들어오면 발사

        Transform body, barrel, reticle;
        SpriteRenderer[] reticleParts;
        Can target;
        Vector2 aim;
        float cooldown;
        bool armed;

        public static AutoTurret Make()
        {
            var go = new GameObject("AutoTurret");
            var t = go.AddComponent<AutoTurret>();
            t.Build();
            return t;
        }

        void Build()
        {
            // 기계: 선반 오른쪽 끝 위에 작은 상자 + 짧은 총열
            body = new GameObject("Body").transform; body.SetParent(transform, false);
            body.position = new Vector3(Look.ShelfRight + 0.55f, Look.ShelfY + 0.45f, 0f);
            var dark = new Color(0.2f, 0.2f, 0.24f); var steel = new Color(0.38f, 0.38f, 0.44f); var lamp = new Color(0.4f, 1f, 0.5f);
            Look.Quad("Base", new Vector2(0f, -0.2f), new Vector2(0.5f, 0.3f), dark, 3, body);
            Look.Quad("Leg", new Vector2(0f, -0.4f), new Vector2(0.14f, 0.2f), dark, 2, body);
            Look.Quad("Head", new Vector2(0f, 0.05f), new Vector2(0.36f, 0.3f), steel, 4, body);
            Look.Quad("Lamp", new Vector2(0.1f, 0.1f), new Vector2(0.08f, 0.08f), lamp, 5, body);
            barrel = new GameObject("Barrel").transform; barrel.SetParent(body, false); barrel.localPosition = new Vector2(0f, 0.05f);
            Look.Quad("Tube", new Vector2(0f, 0.25f), new Vector2(0.09f, 0.5f), dark, 5, barrel);
            // 조준점: 초록 꺾쇠 넷
            reticle = new GameObject("Reticle").transform; reticle.SetParent(transform, false);
            var c = new Color(0.4f, 1f, 0.5f, 0.9f);
            reticleParts = new[]
            {
                Look.Quad("TL1", new Vector2(-0.28f, 0.22f), new Vector2(0.18f, 0.04f), c, 55, reticle), Look.Quad("TL2", new Vector2(-0.22f, 0.28f), new Vector2(0.04f, 0.18f), c, 55, reticle),
                Look.Quad("TR1", new Vector2(0.28f, 0.22f), new Vector2(0.18f, 0.04f), c, 55, reticle), Look.Quad("TR2", new Vector2(0.22f, 0.28f), new Vector2(0.04f, 0.18f), c, 55, reticle),
                Look.Quad("BL1", new Vector2(-0.28f, -0.22f), new Vector2(0.18f, 0.04f), c, 55, reticle), Look.Quad("BL2", new Vector2(-0.22f, -0.28f), new Vector2(0.04f, 0.18f), c, 55, reticle),
                Look.Quad("BR1", new Vector2(0.28f, -0.22f), new Vector2(0.18f, 0.04f), c, 55, reticle), Look.Quad("BR2", new Vector2(0.22f, -0.28f), new Vector2(0.04f, 0.18f), c, 55, reticle),
            };
            reticle.gameObject.SetActive(false);
            aim = body.position + Vector3.up * 2f;
        }

        static bool OnScreen(Vector3 world)
        {
            var v = Camera.main.WorldToViewportPoint(world);
            return v.x > 0.03f && v.x < 0.97f && v.y > 0.05f && v.y < 0.97f;
        }

        Can Pick()
        {
            Can best = null; float bestY = float.MaxValue;
            foreach (var c in Game.I.Cans)
            {
                if (c == null || !c.Airborne || c.Done || c.IsMenu) continue;
                if (!OnScreen(c.transform.position)) continue;
                float y = c.transform.position.y;      // 가장 낮은(곧 떨어질) 깡통부터
                if (y < bestY) { bestY = y; best = c; }
            }
            return best;
        }

        public void Tick(float dt)
        {
            if (Game.I == null || Game.I.State != GameState.Play) { reticle.gameObject.SetActive(false); return; }
            cooldown = Mathf.Max(0f, cooldown - dt);
            // 목표 고르기/갱신: 없어졌거나 화면 밖이면 다시
            if (target == null || target.Done || !target.Airborne || !OnScreen(target.transform.position)) target = cooldown <= 0f ? Pick() : null;
            bool show = target != null;
            reticle.gameObject.SetActive(show);
            if (!show) { armed = false; return; }
            // 조준점이 목표로 움직인다 (목표가 움직이니 계속 따라간다)
            Vector2 goal = target.transform.position;
            aim = Vector2.Lerp(aim, goal, 1f - Mathf.Exp(-dt * AimSpeed));
            reticle.position = aim;
            reticle.localRotation = Quaternion.Euler(0f, 0f, Time.time * 90f);
            float d = Vector2.Distance(aim, goal);
            float s = 1f + Mathf.Clamp01(d) * 0.8f;                 // 멀면 크게, 맞물리면 작게
            reticle.localScale = Vector3.one * s;
            foreach (var p in reticleParts) p.color = d < LockDist ? new Color(1f, 0.95f, 0.5f, 1f) : new Color(0.4f, 1f, 0.5f, 0.9f);
            // 총열이 목표를 향한다
            var dir = goal - (Vector2)barrel.position;
            barrel.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
            if (d < LockDist && cooldown <= 0f) Fire(goal);
        }

        void Fire(Vector2 at)
        {
            var c = target;
            cooldown = Interval;
            target = null;
            if (c == null) return;
            int earned = c.Hit(at, 0.5f);
            Game.I.CanHit(c, earned, at);
            // 궤적 + 섬광 (기계에서 깡통까지)
            var tip = (Vector2)barrel.position + (Vector2)(barrel.up * 0.5f);
            var dv = at - tip;
            var tracer = Look.Quad("Tracer", (tip + at) * 0.5f, new Vector2(dv.magnitude, 0.035f), new Color(0.6f, 1f, 0.7f, 0.7f), 47);
            tracer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dv.y, dv.x) * Mathf.Rad2Deg);
            Destroy(tracer.gameObject, 0.05f);
            var f = Look.Quad("Flash", tip, Vector2.one * 0.3f, new Color(0.7f, 1f, 0.8f, 0.9f), 48);
            f.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            Destroy(f.gameObject, 0.05f);
            Sfx.Play(Sfx.Bang(), 0.35f, 1.5f);
        }
    }
}
