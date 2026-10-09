using System.Collections.Generic;
using UnityEngine;

namespace TinCan
{
    /// <summary>화면 위로 나간 깡통: 위 가장자리에 그 깡통 색 화살표로 자리를 보여 준다. 높이 올라갈수록 작아진다.</summary>
    public class OffscreenMarkers : MonoBehaviour
    {
        class Marker { public Transform Root; public SpriteRenderer Body, Tip; public Transform Bar; }
        readonly Dictionary<Can, Marker> markers = new Dictionary<Can, Marker>();
        readonly List<Can> dead = new List<Can>();
        Camera cam;

        void Start() { cam = Camera.main; }

        void LateUpdate()
        {
            if (Game.I == null) return;
            float top = cam.orthographicSize;
            foreach (var c in Game.I.Cans)
            {
                if (c == null) continue;
                bool above = c.transform.position.y > top + 0.2f;
                if (above && !markers.ContainsKey(c)) markers[c] = Make(c);
                if (!above && markers.TryGetValue(c, out var m)) { Destroy(m.Root.gameObject); markers.Remove(c); }
            }
            dead.Clear();
            foreach (var kv in markers)
            {
                var c = kv.Key; var m = kv.Value;
                if (c == null || c.Done) { Destroy(m.Root.gameObject); dead.Add(c); continue; }
                float over = c.transform.position.y - top;                     // 얼마나 위에 있나
                float s = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(over / 8f));     // 멀수록 작게
                m.Root.position = new Vector3(c.transform.position.x, top - 0.45f, 0f);
                m.Root.localScale = Vector3.one * s;
                m.Bar.localScale = new Vector3(0.06f, Mathf.Clamp(over * 0.08f, 0.1f, 0.5f), 1f);
                m.Bar.localPosition = new Vector3(0f, -0.3f - m.Bar.localScale.y * 0.5f, 0f);
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 10f);
                m.Body.color = new Color(c.Kind.Body.r, c.Kind.Body.g, c.Kind.Body.b, pulse);
            }
            foreach (var c in dead) markers.Remove(c);
        }

        Marker Make(Can c)
        {
            var m = new Marker();
            m.Root = new GameObject("Marker").transform;
            // 위를 가리키는 화살표: 마름모 + 줄기
            m.Tip = Look.Quad("Tip", new Vector2(0f, 0.08f), new Vector2(0.34f, 0.34f), new Color(1f, 1f, 1f, 0.95f), 60, m.Root);
            m.Tip.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            m.Body = Look.Quad("Body", new Vector2(0f, 0.08f), new Vector2(0.24f, 0.24f), c.Kind.Body, 61, m.Root);
            m.Body.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var bar = Look.Quad("Bar", new Vector2(0f, -0.35f), new Vector2(0.06f, 0.3f), new Color(1f, 1f, 1f, 0.8f), 60, m.Root);
            m.Bar = bar.transform;
            return m;
        }
    }
}
