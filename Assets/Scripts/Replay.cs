using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>
    /// 데드아이 리플레이 (10-16 사용자, 슈퍼핫에서): 슬로우 속에서 표시하고 다다당 쏜 장면을, 끝난 뒤 실제 속도로 짧게 다시 보여 준다.
    /// 느릿느릿 한 일이 실제 속도로 보면 번개 같은 총잡이가 된다 → 슬로우가 강해질수록 리플레이 속 내가 더 말이 안 되게 빠르다.
    /// 기록: 데드아이가 있는 슬로우가 켜진 때부터 연발이 끝날 때까지, 깡통 그림의 자리·회전·살아 있음과 총 쏜 자리.
    /// 시계는 깡통의 시간(Time.deltaTime × Can.TimeScale)으로 센다 = 실제 속도. 재생 동안 게임은 멈춘다(timeScale 0),
    /// 진짜 깡통 그림은 숨기고 복제 그림을 움직인다. 큰 순간에만 (슈퍼핫 교훈: 매번 틀면 질린다)
    /// </summary>
    public class Replay : MonoBehaviour
    {
        public static Replay I { get; private set; }
        public static bool Playing { get; private set; }

        class Track { public Can Can; public GameObject Ghost; public readonly List<(float t, Vector3 p, Quaternion r, bool alive)> Keys = new List<(float, Vector3, Quaternion, bool)>(); }
        readonly List<Track> tracks = new List<Track>();
        readonly List<(float t, Vector2 at)> shots = new List<(float, Vector2)>();
        bool recording; float clock;

        public static Replay Make() { var r = new GameObject("Replay").AddComponent<Replay>(); I = r; Playing = false; return r; }

        /// <summary>데드아이 슬로우가 켜졌다: 기록 시작</summary>
        public void Begin()
        {
            Clear();
            recording = true; clock = 0f;
        }
        /// <summary>표시 없이 슬로우가 끝났다: 기록 버림</summary>
        public void Cancel() { recording = false; Clear(); }
        public void Shot(Vector2 at) { if (recording) shots.Add((clock, at)); }
        /// <summary>연발이 끝났다: 0.25초 더 기록하고 재생</summary>
        public void Finish() { if (recording) StartCoroutine(FinishCo()); }

        IEnumerator FinishCo()
        {
            yield return new WaitForSeconds(0.25f);
            recording = false;
            if (clock > 0.3f && shots.Count > 0) yield return Play();
            Clear();
        }

        void Clear()
        {
            foreach (var t in tracks) if (t.Ghost != null) Destroy(t.Ghost);
            tracks.Clear(); shots.Clear();
        }

        void LateUpdate()
        {
            if (!recording || Playing) return;
            clock += Time.deltaTime * Can.TimeScale;
            var g = Game.I; if (g == null) return;
            foreach (var c in g.Cans)
            {
                if (c == null || c.IsMenu || c.Visual == null) continue;
                if (tracks.Find(x => x.Can == c) != null) continue;
                if (c.Done) continue;
                tracks.Add(new Track { Can = c, Ghost = c.CloneVisual() });
            }
            foreach (var t in tracks)
            {
                bool alive = t.Can != null && !t.Can.Done && t.Can.Visual != null;
                if (alive) t.Keys.Add((clock, t.Can.Visual.position, t.Can.Visual.rotation, true));
                else if (t.Keys.Count > 0 && t.Keys[t.Keys.Count - 1].alive) t.Keys.Add((clock, t.Keys[t.Keys.Count - 1].p, t.Keys[t.Keys.Count - 1].r, false));
            }
        }

        IEnumerator Play()
        {
            Playing = true;
            var g = Game.I;
            float prevScale = Time.timeScale;
            Time.timeScale = 0f;
            bool gunWas = g.Gun.Enabled; g.Gun.Enabled = false;
            Letterbox.Force = true;
            foreach (var t in tracks) if (t.Can != null && !t.Can.Done) t.Can.HideVisual(true);
            yield return new WaitForSecondsRealtime(0.25f);
            Sfx.Play(Sfx.Whoosh(), 0.5f, 1.6f);
            float dur = clock; int nextShot = 0;
            var flashes = new List<(SpriteRenderer s, float until)>();
            for (float tp = 0f; tp <= dur; tp += Time.unscaledDeltaTime)
            {
                foreach (var t in tracks)
                {
                    if (t.Ghost == null || t.Keys.Count == 0) continue;
                    int i = t.Keys.FindLastIndex(k => k.t <= tp);
                    if (i < 0) { t.Ghost.SetActive(false); continue; }
                    var k0 = t.Keys[i];
                    bool was = t.Ghost.activeSelf;
                    if (!k0.alive)
                    {
                        if (was) { t.Ghost.SetActive(false); Sfx.Play(Sfx.Tang(), 0.6f, 0.6f); flashes.Add((Look.Quad("ReplayPop", k0.p, new Vector2(0.7f, 0.7f), new Color(1f, 1f, 1f, 0.8f), 21), Time.unscaledTime + 0.08f)); }
                        continue;
                    }
                    t.Ghost.SetActive(true);
                    if (i + 1 < t.Keys.Count && t.Keys[i + 1].alive)
                    {
                        var k1 = t.Keys[i + 1];
                        float u = Mathf.InverseLerp(k0.t, k1.t, tp);
                        t.Ghost.transform.SetPositionAndRotation(Vector3.Lerp(k0.p, k1.p, u), Quaternion.Slerp(k0.r, k1.r, u));
                    }
                    else t.Ghost.transform.SetPositionAndRotation(k0.p, k0.r);
                }
                // 총 쏜 자리: 하얀 섬광 + 탕
                while (nextShot < shots.Count && shots[nextShot].t <= tp)
                {
                    var s = Look.Quad("ReplayShot", shots[nextShot].at, new Vector2(0.35f, 0.35f), new Color(1f, 0.95f, 0.7f, 0.9f), 21);
                    s.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                    flashes.Add((s, Time.unscaledTime + 0.05f));
                    Sfx.Play(Sfx.Bang(), 0.6f, 1.1f);
                    Fx.Shake(0.25f);
                    nextShot++;
                }
                for (int f = flashes.Count - 1; f >= 0; f--) if (Time.unscaledTime > flashes[f].until) { if (flashes[f].s != null) Destroy(flashes[f].s.gameObject); flashes.RemoveAt(f); }
                yield return null;
            }
            foreach (var f in flashes) if (f.s != null) Destroy(f.s.gameObject);
            yield return new WaitForSecondsRealtime(0.3f);
            foreach (var t in tracks) { if (t.Ghost != null) t.Ghost.SetActive(false); if (t.Can != null && !t.Can.Done) t.Can.HideVisual(false); }
            Letterbox.Force = false;
            Time.timeScale = prevScale <= 0f ? 1f : prevScale;
            if (!g.MenuOpen) g.Gun.Enabled = gunWas;
            Playing = false;
        }
    }
}
