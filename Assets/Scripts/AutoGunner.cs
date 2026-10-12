using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 자동 사격 (사용자 10-12 2판): 선반 옆 기계 대신 **화면 아래에 총들이 등장해서 쏜다.** 스킬트리에서 총 종류를 뚫을수록 아래에 총이 늘어나
    /// 마을 난투 느낌. 총마다 조준점이 **부드럽게 따라가다** 맞물리면 쏘고, 3초 넘게 못 쏘면 그 자리에 그냥 쏜다.
    /// 총마다 따라가는 성격이 다르다: 리볼버 = 빠르고 넓게 · 레버액션 = 중간 · 샷건 = 느리지만 원 안 전부.
    /// 값은 플레이어 총의 절반. 그림은 플레이어 총과 같은 옆모습(gun_{id}_side), 등장은 아래에서 용수철로.
    /// </summary>
    public class AutoGunner : MonoBehaviour
    {
        class AutoGun
        {
            public GunKind Kind; public Transform Root, Side; public SpriteRenderer Sr;
            public float HomeX, Y = -7f, YV, Rot, Kick, KickV, Cooldown, Track;
            public Transform Reticle; public SpriteRenderer[] Parts; public Vector2 Aim; public Can Target;
            public float AimSpeed, Interval, Radius; public bool HitAll; public Color Tint;
        }
        readonly List<AutoGun> guns = new List<AutoGun>();
        const float BottomY = -3.3f;        // 선반 아래, 모래 위
        const float ForceAfter = 3f;        // 이만큼 쫓고도 못 쏘면 그냥 쏜다 (사용자 10-12)
        public float RateScale = 1f;        // 스킬 "빠른 사격기": 간격 배수

        public static AutoGunner Make() => new GameObject("AutoGunner").AddComponent<AutoGunner>();

        /// <summary>총을 하나 더 세운다 (스킬 노드가 부른다). 자리는 가운데를 비우고 좌우로 번갈아</summary>
        public void Add(GunKind kind)
        {
            int i = guns.Count;
            float x = (i % 2 == 0 ? -1f : 1f) * (3.2f + (i / 2) * 2.6f);
            var g = new AutoGun { Kind = kind, HomeX = x };
            switch (kind.Kin)
            {
                case GunKind.Family.Revolver: g.AimSpeed = 12f; g.Interval = 1.1f; g.Radius = 0.35f; g.Tint = new Color(0.55f, 1f, 0.6f); break;      // 빠르고 넓게
                case GunKind.Family.Shotgun: g.AimSpeed = 4f; g.Interval = 2.4f; g.Radius = 0.9f; g.HitAll = true; g.Tint = new Color(1f, 0.6f, 0.4f); break;   // 느리지만 전부
                case GunKind.Family.Lever: g.AimSpeed = 7f; g.Interval = 0.8f; g.Radius = 0.3f; g.Tint = new Color(1f, 0.9f, 0.4f); break;        // 중간, 꾸준히
                default: g.AimSpeed = 6f; g.Interval = 1.5f; g.Radius = 0.3f; g.Tint = Color.white; break;
            }
            // 그림: 플레이어 총과 같은 옆모습. 뿌리(자리) → side(조준 회전)
            g.Root = new GameObject("AutoGun_" + kind.Id).transform; g.Root.SetParent(transform, false);
            g.Root.position = new Vector3(x, -7f, 0f);
            g.Side = new GameObject("Side").transform; g.Side.SetParent(g.Root, false);
            var sp = GunView.LoadArt($"gun_{kind.Id}_side") ?? GunView.LoadArt("gun_revolver_side");
            g.Sr = g.Side.gameObject.AddComponent<SpriteRenderer>(); g.Sr.sprite = sp; g.Sr.sortingOrder = 30;
            // 조준점: 색 꺾쇠 넷 (총마다 색)
            g.Reticle = new GameObject("Reticle").transform; g.Reticle.SetParent(transform, false);
            var c = g.Tint; c.a = 0.9f;
            g.Parts = new[]
            {
                Look.Quad("TL1", new Vector2(-0.28f, 0.22f), new Vector2(0.18f, 0.04f), c, 55, g.Reticle), Look.Quad("TL2", new Vector2(-0.22f, 0.28f), new Vector2(0.04f, 0.18f), c, 55, g.Reticle),
                Look.Quad("TR1", new Vector2(0.28f, 0.22f), new Vector2(0.18f, 0.04f), c, 55, g.Reticle), Look.Quad("TR2", new Vector2(0.22f, 0.28f), new Vector2(0.04f, 0.18f), c, 55, g.Reticle),
                Look.Quad("BL1", new Vector2(-0.28f, -0.22f), new Vector2(0.18f, 0.04f), c, 55, g.Reticle), Look.Quad("BL2", new Vector2(-0.22f, -0.28f), new Vector2(0.04f, 0.18f), c, 55, g.Reticle),
                Look.Quad("BR1", new Vector2(0.28f, -0.22f), new Vector2(0.18f, 0.04f), c, 55, g.Reticle), Look.Quad("BR2", new Vector2(0.22f, -0.28f), new Vector2(0.04f, 0.18f), c, 55, g.Reticle),
            };
            g.Reticle.gameObject.SetActive(false);
            g.Aim = new Vector2(x, 1f);
            g.YV = 18f;   // 아래에서 튀어 올라온다
            guns.Add(g);
            Sfx.Play(Sfx.Reload(), 0.7f, 1.1f);
            Fx.Dust(new Vector2(x, BottomY - 0.6f), 6, 1.2f);
            Game.I?.Hud.Float($"{kind.Name} 합류", new Vector2(x, BottomY + 1.6f), 5);
        }

        public int Count => guns.Count;

        static bool OnScreen(Vector3 world)
        {
            var v = Camera.main.WorldToViewportPoint(world);
            return v.x > 0.03f && v.x < 0.97f && v.y > 0.05f && v.y < 0.97f;
        }

        /// <summary>총마다 다른 고르기: 리볼버 = 아무거나 가까운 것, 샷건 = 가장 낮은(곧 떨어질) 것, 레버 = 지금 조준점에서 가까운 것</summary>
        Can Pick(AutoGun g)
        {
            Can best = null; float bestScore = float.MaxValue;
            foreach (var c in Game.I.Cans)
            {
                if (c == null || !c.Airborne || c.Done || c.IsMenu) continue;
                if (!OnScreen(c.transform.position)) continue;
                float score = g.Kind.Kin == GunKind.Family.Shotgun ? c.transform.position.y : Vector2.Distance(g.Aim, c.transform.position);
                if (score < bestScore) { bestScore = score; best = c; }
            }
            return best;
        }

        public void Tick(float dt)
        {
            bool play = Game.I != null && Game.I.State == GameState.Play && !Game.I.MenuOpen;
            foreach (var g in guns)
            {
                // 자리: 아래에서 용수철로 올라와 서 있는다
                Ease.Spring(ref g.Y, ref g.YV, BottomY, 160f, 12f, dt);
                Ease.Spring(ref g.Kick, ref g.KickV, 0f, 500f, 16f, dt);
                g.Root.position = new Vector3(g.HomeX, g.Y, 0f);
                g.Cooldown = Mathf.Max(0f, g.Cooldown - dt);
                if (!play) { g.Reticle.gameObject.SetActive(false); continue; }
                // 목표
                if (g.Target == null || g.Target.Done || !g.Target.Airborne || !OnScreen(g.Target.transform.position))
                {
                    g.Target = g.Cooldown <= 0f ? Pick(g) : null;
                    g.Track = 0f;
                }
                bool show = g.Target != null;
                g.Reticle.gameObject.SetActive(show);
                Vector2 goal = show ? (Vector2)g.Target.transform.position : new Vector2(g.HomeX, 1.5f);
                // 조준점이 부드럽게 따라간다 (총마다 속도 다름). 오래 쫓을수록 빨라진다
                float speed = g.AimSpeed * (1f + Mathf.Clamp01(g.Track / ForceAfter) * 1.5f);
                g.Aim = Vector2.Lerp(g.Aim, goal, 1f - Mathf.Exp(-dt * speed));
                g.Reticle.position = g.Aim;
                g.Reticle.localRotation = Quaternion.Euler(0f, 0f, Time.time * 90f);
                float d = Vector2.Distance(g.Aim, goal);
                g.Reticle.localScale = Vector3.one * (1f + Mathf.Clamp01(d) * 0.8f);
                foreach (var p in g.Parts) p.color = d < 0.25f ? new Color(1f, 0.95f, 0.5f, 1f) : new Color(g.Tint.r, g.Tint.g, g.Tint.b, 0.9f);
                // 총이 조준점을 향한다 (왼쪽이면 뒤집기), 반동은 총열 반대로
                var dir = g.Aim - (Vector2)g.Root.position;
                float deg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                g.Rot = Mathf.LerpAngle(g.Rot, deg, 1f - Mathf.Exp(-dt * 10f));
                bool left = Mathf.Cos(g.Rot * Mathf.Deg2Rad) < 0f;
                g.Side.localRotation = Quaternion.Euler(0f, 0f, g.Rot);
                g.Side.localScale = new Vector3(1f, left ? -1f : 1f, 1f);
                g.Side.localPosition = -new Vector3(Mathf.Cos(g.Rot * Mathf.Deg2Rad), Mathf.Sin(g.Rot * Mathf.Deg2Rad), 0f) * g.Kick * 0.35f;
                if (!show) continue;
                g.Track += dt;
                // 맞물리면 쏜다. 3초 넘게 쫓았으면 그냥 쏜다 (빗나갈 수도)
                if (g.Cooldown <= 0f && (d < 0.25f || g.Track >= ForceAfter)) Fire(g);
            }
        }

        void Fire(AutoGun g)
        {
            g.Cooldown = g.Interval * RateScale;
            g.Kick = 1f; g.KickV = 0f;
            var at = g.Aim;
            g.Track = 0f;
            // 판정: 리볼버·레버는 조준점에서 가장 가까운 하나, 샷건은 원 안 전부
            Can best = null; float bestD = float.MaxValue; int hits = 0;
            foreach (var col in Physics2D.OverlapCircleAll(at, Mathf.Max(g.Radius, 0.3f)))
            {
                var can = col.GetComponent<Can>();
                if (can == null || can.Done || can.IsMenu || !can.Airborne) continue;
                if (g.HitAll) { int e = can.Hit(Vector2.Lerp(at, can.transform.position, 0.7f), 0.5f * g.Kind.ValueScale, g.Kind.BounceScale); Game.I.CanHit(can, e, at); hits++; continue; }
                float dd = ((Vector2)can.transform.position - at).sqrMagnitude;
                if (dd < bestD) { bestD = dd; best = can; }
            }
            if (best != null) { int e = best.Hit(at, 0.5f * g.Kind.ValueScale, g.Kind.BounceScale); Game.I.CanHit(best, e, at); hits++; }
            g.Target = null;
            // 총구 섬광 + 궤적 (총열 끝에서 조준점까지)
            var dir = new Vector2(Mathf.Cos(g.Rot * Mathf.Deg2Rad), Mathf.Sin(g.Rot * Mathf.Deg2Rad));
            var tip = (Vector2)g.Root.position + dir * 1.6f;
            var dv = at - tip;
            var tracer = Look.Quad("Tracer", (tip + at) * 0.5f, new Vector2(dv.magnitude, 0.035f), new Color(1f, 0.95f, 0.7f, 0.7f), 47);
            tracer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dv.y, dv.x) * Mathf.Rad2Deg);
            Destroy(tracer.gameObject, 0.05f);
            var f = Look.Quad("Flash", tip, Vector2.one * (0.35f + g.Radius * 0.3f), new Color(1f, 0.95f, 0.6f, 0.9f), 48);
            f.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            Destroy(f.gameObject, 0.05f);
            Fx.Shake(0.08f);
            Sfx.Play(Sfx.Bang(), 0.4f, g.Kind.Pitch * 1.3f);
            if (hits > 0) Fx.HitMark(at);
        }
    }
}
