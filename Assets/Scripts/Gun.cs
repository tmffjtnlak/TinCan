using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinCan
{
    /// <summary>
    /// 리볼버. 마우스 조준, 좌클릭 사격. 6발, 다 쓰면 잠깐 뒤 자동 장전 (R로 즉시).
    /// 맞추기 판정은 조준점 주변 작은 원 (조금 너그럽게).
    /// </summary>
    public class Gun : MonoBehaviour
    {
        public int Capacity = 6;
        public int Ammo { get; private set; }
        public bool Reloading { get; private set; }
        public float ReloadSeconds = 0.9f;
        public float AimRadius = 0.22f;
        public int Shots { get; private set; }
        public int Hits { get; private set; }
        /// <summary>상점이 열려 있으면 꺼진다</summary>
        public bool Enabled { get; set; } = true;

        Transform crosshair;
        SpriteRenderer[] crossParts;
        Camera cam;
        float kickLeft;
        Vector3 camHome;

        public void Build()
        {
            cam = Camera.main; camHome = cam.transform.position;
            Ammo = Capacity;
            Cursor.visible = false;
            crosshair = new GameObject("Crosshair").transform;
            var c = new Color(1f, 1f, 1f, 0.9f);
            crossParts = new[]
            {
                Look.Quad("N", new Vector2(0f, 0.22f), new Vector2(0.05f, 0.16f), c, 50, crosshair),
                Look.Quad("S", new Vector2(0f, -0.22f), new Vector2(0.05f, 0.16f), c, 50, crosshair),
                Look.Quad("E", new Vector2(0.22f, 0f), new Vector2(0.16f, 0.05f), c, 50, crosshair),
                Look.Quad("W", new Vector2(-0.22f, 0f), new Vector2(0.16f, 0.05f), c, 50, crosshair),
                Look.Quad("Dot", Vector2.zero, new Vector2(0.05f, 0.05f), c, 50, crosshair),
            };
        }

        Vector2 MouseWorld()
        {
            var m = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            var w = cam.ScreenToWorldPoint(new Vector3(m.x, m.y, 10f));
            return new Vector2(w.x, w.y);
        }

        void Update()
        {
            var aim = MouseWorld();
            crosshair.position = aim;
            crosshair.gameObject.SetActive(Enabled);
            Cursor.visible = !Enabled;
            if (!Enabled) return;
            var mouse = Mouse.current; var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && !Reloading && Ammo < Capacity) StartCoroutine(Reload());
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) Fire(aim);

            // 조준점: 쏘면 벌어졌다 모인다
            float spread = 1f + kickLeft * 6f;
            crosshair.localScale = Vector3.one * spread;
            if (kickLeft > 0f)
            {
                kickLeft = Mathf.Max(0f, kickLeft - Time.unscaledDeltaTime * 5f);
                cam.transform.position = camHome + (Vector3)Random.insideUnitCircle * kickLeft * 0.12f;
                if (kickLeft <= 0f) cam.transform.position = camHome;
            }
        }

        void Fire(Vector2 aim)
        {
            if (Reloading) return;
            if (Ammo <= 0) { Sfx.Play(Sfx.Click(), 0.7f, 1f); return; }
            Ammo--; Shots++;
            kickLeft = 1f;
            Sfx.Play(Sfx.Bang(), 0.9f, Random.Range(0.95f, 1.08f));
            StartCoroutine(MuzzleFlash(aim));
            // 판정: 조준점 주변 원 안의 깡통 중 가장 가까운 것
            Can best = null; float bestD = float.MaxValue;
            foreach (var col in Physics2D.OverlapCircleAll(aim, AimRadius))
            {
                var can = col.GetComponent<Can>();
                if (can == null) continue;
                float d = ((Vector2)can.transform.position - aim).sqrMagnitude;
                if (d < bestD) { bestD = d; best = can; }
            }
            if (best != null)
            {
                Hits++;
                int earned = best.Hit(aim);
                Game.I?.CanHit(best, earned, aim);
            }
            if (Ammo == 0) StartCoroutine(AutoReload());
        }

        IEnumerator MuzzleFlash(Vector2 at)
        {
            var f = Look.Quad("Flash", at, new Vector2(0.5f, 0.5f), new Color(1f, 0.95f, 0.6f, 0.9f), 49);
            f.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            yield return null; yield return null;
            Destroy(f.gameObject);
        }

        IEnumerator AutoReload()
        {
            yield return new WaitForSeconds(0.35f);
            if (Ammo == 0 && !Reloading) yield return Reload();
        }

        public IEnumerator Reload()
        {
            Reloading = true;
            Sfx.Play(Sfx.Reload(), 0.8f, 1f);
            yield return new WaitForSeconds(ReloadSeconds);
            Ammo = Capacity;
            Reloading = false;
        }
    }
}
