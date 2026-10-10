using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>
    /// 시험용 총 뷰어 (F3). 복셀 총을 화면 가운데 크게 놓고 어느 각도든 돌려 본다.
    /// 좌드래그 = 눕힘(Lay)·기울기(Tilt), 우드래그 = 손가락 축 돌림(Spin), 휠 = 크기, R = 초기화, F3 = 닫기.
    /// </summary>
    public class GunViewer : MonoBehaviour
    {
        VoxelGun gun;
        Image veil; Text info;
        float lay, tilt = 28f, spin, zoom = 2.6f;
        Vector2 lastMouse;
        public bool Open { get; private set; }

        public static GunViewer Make(Transform canvas)
        {
            var v = new GameObject("GunViewer").AddComponent<GunViewer>();
            v.veil = Ui.Image("ViewerVeil", canvas, Vector2.zero, new Vector2(4000f, 3000f), new Color(0.08f, 0.1f, 0.16f, 1f));
            v.info = Ui.Text("ViewerInfo", v.veil.transform, new Vector2(0f, -440f), new Vector2(1600f, 90f), 22, TextAnchor.MiddleCenter, Ui.Ink);
            v.veil.gameObject.SetActive(false);
            return v;
        }

        public void Toggle()
        {
            Open = !Open;
            veil.gameObject.SetActive(Open);
            if (Open)
            {
                if (gun == null) { gun = VoxelGun.Make(transform); foreach (var r in gun.GetComponentsInChildren<Renderer>()) r.sortingOrder = 300; }
                gun.transform.position = new Vector3(0f, -0.6f, 0f);
                gun.gameObject.SetActive(true);
                if (Game.I != null) Game.I.Gun.Enabled = false;
            }
            else
            {
                if (gun != null) gun.gameObject.SetActive(false);
                if (Game.I != null && !Game.I.MenuOpen) Game.I.Gun.Enabled = true;
            }
        }

        void Update()
        {
            if (!Open || gun == null) return;
            var m = Mouse.current; var kb = Keyboard.current;
            if (m != null)
            {
                var pos = m.position.ReadValue();
                var d = pos - lastMouse; lastMouse = pos;
                if (m.leftButton.isPressed) { lay += d.x * 0.5f; tilt = Mathf.Clamp(tilt - d.y * 0.5f, -90f, 90f); }
                if (m.rightButton.isPressed) { spin += d.x * 0.5f; }
                float w = m.scroll.ReadValue().y; if (Mathf.Abs(w) > 0.01f) zoom = Mathf.Clamp(zoom * (w > 0 ? 1.1f : 0.9f), 0.8f, 6f);
            }
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed) lay -= 90f * Time.unscaledDeltaTime;
                if (kb.rightArrowKey.isPressed) lay += 90f * Time.unscaledDeltaTime;
                if (kb.upArrowKey.isPressed) tilt += 60f * Time.unscaledDeltaTime;
                if (kb.downArrowKey.isPressed) tilt -= 60f * Time.unscaledDeltaTime;
                if (kb.qKey.isPressed) spin -= 120f * Time.unscaledDeltaTime;
                if (kb.eKey.isPressed) spin += 120f * Time.unscaledDeltaTime;
                if (kb.rKey.wasPressedThisFrame) { lay = 0f; tilt = 28f; spin = 0f; zoom = 2.6f; }
            }
            gun.Lay = lay; gun.Tilt = tilt; gun.Spin = spin; gun.Apply();
            gun.transform.localScale = Vector3.one * zoom;
            info.text = $"기울기 {tilt:0}°  ·  눕힘 {lay:0}°  ·  돌림 {spin:0}°  ·  크기 ×{zoom:0.0}\n좌드래그·방향키 = 기울기/눕힘   우드래그·Q/E = 돌림   휠 = 크기   R = 초기화   F3 = 닫기";
        }
    }
}
