using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TinCan
{
    /// <summary>
    /// 손에 든 총. 종류(GunKind)가 쏘는 방식을 정하고, 스킬이 숫자를 보탠다.
    /// 마우스 조준, 좌클릭 사격. 다 쓰면 잠깐 뒤 자동 장전 (R로 즉시).
    /// </summary>
    public class Gun : MonoBehaviour
    {
        public GunKind Kind { get; private set; } = GunKind.Revolver;
        public int Capacity => Kind.Capacity + CapacityBonus;
        public int CapacityBonus { get; set; }
        public int Ammo { get; private set; }
        public bool Reloading { get; private set; }
        /// <summary>스킬 "빠른 장전": 장전 시간 배수 (1 = 그대로)</summary>
        public float ReloadScale { get; set; } = 1f;
        public float ReloadSeconds => Kind.ReloadSeconds * ReloadScale;
        public int Shots { get; private set; }
        public int Hits { get; private set; }
        /// <summary>스킬트리가 열려 있으면 꺼진다</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>"연속 장전": 공중 연속 N번마다 즉시 장전. 0 = 없음</summary>
        public int ComboReloadEvery { get; set; } = 0;

        Transform crosshair, ring;
        Camera cam;
        float kickLeft, nextFire;
        Vector3 camHome;

        public void Build()
        {
            cam = Camera.main; camHome = cam.transform.position;
            Cursor.visible = false;
            crosshair = new GameObject("Crosshair").transform;
            var c = new Color(1f, 1f, 1f, 0.9f);
            Look.Quad("N", new Vector2(0f, 0.22f), new Vector2(0.05f, 0.16f), c, 50, crosshair);
            Look.Quad("S", new Vector2(0f, -0.22f), new Vector2(0.05f, 0.16f), c, 50, crosshair);
            Look.Quad("E", new Vector2(0.22f, 0f), new Vector2(0.16f, 0.05f), c, 50, crosshair);
            Look.Quad("W", new Vector2(-0.22f, 0f), new Vector2(0.16f, 0.05f), c, 50, crosshair);
            Look.Quad("Dot", Vector2.zero, new Vector2(0.05f, 0.05f), c, 50, crosshair);
            // 판정 원 테두리 (샷건처럼 넓은 총일 때 보인다): 점 16개
            ring = new GameObject("Ring").transform; ring.SetParent(crosshair, false);
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2f;
                Look.Quad("R", new Vector2(Mathf.Cos(a), Mathf.Sin(a)), new Vector2(0.05f, 0.05f), new Color(1f, 1f, 1f, 0.45f), 50, ring);
            }
            Equip(GunKind.Revolver);
        }

        public void Equip(GunKind kind)
        {
            Kind = kind;
            Ammo = Capacity; Reloading = false;
            ring.localScale = Vector3.one * kind.AimRadius;
            ring.gameObject.SetActive(kind.AimRadius > 0.4f);
        }

        public void OnComboHit(int combo)
        {
            if (ComboReloadEvery > 0 && combo > 0 && combo % ComboReloadEvery == 0 && Ammo < Capacity)
            {
                Reloading = false;
                Ammo = Capacity;
                Sfx.Play(Sfx.Reload(), 0.7f, 2.4f);
            }
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
            if (mouse != null)
            {
                bool want = Kind.Auto ? mouse.leftButton.isPressed : mouse.leftButton.wasPressedThisFrame;
                if (want && Time.time >= nextFire) { Fire(aim); nextFire = Time.time + Kind.FireInterval; }
            }

            float spread = 1f + kickLeft * 6f * Mathf.Min(1f, Kind.Kick);
            crosshair.localScale = Vector3.one * spread;
            if (kickLeft > 0f)
            {
                kickLeft = Mathf.Max(0f, kickLeft - Time.unscaledDeltaTime * 5f);
                cam.transform.position = camHome + (Vector3)Random.insideUnitCircle * kickLeft * 0.12f * Kind.Kick;
                if (kickLeft <= 0f) cam.transform.position = camHome;
            }
        }

        void Fire(Vector2 aim)
        {
            if (Reloading) return;
            if (Ammo <= 0) { if (!Kind.Auto) Sfx.Play(Sfx.Click(), 0.7f, 1f); return; }
            Ammo--; Shots++;
            kickLeft = 1f;
            Sfx.Play(Sfx.Bang(), Kind.Auto ? 0.5f : 0.9f, Kind.Pitch * Random.Range(0.95f, 1.08f));
            StartCoroutine(MuzzleFlash(aim));
            // 판정: 원 안의 깡통. 리볼버는 가장 가까운 하나, 샷건은 전부
            Can best = null; float bestD = float.MaxValue; bool any = false;
            foreach (var col in Physics2D.OverlapCircleAll(aim, Kind.AimRadius))
            {
                var can = col.GetComponent<Can>();
                if (can == null || can.Done) continue;
                if (Kind.HitAll) { HitOne(can, aim); any = true; continue; }
                float d = ((Vector2)can.transform.position - aim).sqrMagnitude;
                if (d < bestD) { bestD = d; best = can; }
            }
            if (best != null) { HitOne(best, aim); any = true; }
            if (any) Hits++;
            if (Ammo == 0) StartCoroutine(AutoReload());
        }

        void HitOne(Can can, Vector2 aim)
        {
            // 샷건은 깡통마다 맞은 자리가 다르다: 깡통 쪽으로 살짝 당긴 점
            Vector2 at = Kind.HitAll ? Vector2.Lerp(aim, can.transform.position, 0.7f) : aim;
            int earned = can.Hit(at, Kind.ValueScale, Kind.BounceScale);
            Game.I?.CanHit(can, earned, at);
        }

        IEnumerator MuzzleFlash(Vector2 at)
        {
            var f = Look.Quad("Flash", at, Vector2.one * (0.5f + Kind.AimRadius * 0.5f), new Color(1f, 0.95f, 0.6f, 0.9f), 49);
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
