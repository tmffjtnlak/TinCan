using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TinCan
{
    /// <summary>쏴서 누르는 버튼 (설정 화면용). 작은 나무 표지판. 맞으면 흔들리고 눌린다.</summary>
    public class ShootButton : MonoBehaviour
    {
        public System.Action OnShot;
        Transform visual;
        Text label;
        System.Func<string> text;

        public static ShootButton Make(string name, Vector2 pos, Vector2 size, System.Func<string> text, System.Action onShot, Transform canvas)
        {
            var go = new GameObject("Btn_" + name);
            go.transform.position = pos;
            var b = go.AddComponent<ShootButton>();
            b.OnShot = onShot; b.text = text;
            var col = go.AddComponent<BoxCollider2D>(); col.size = size; col.isTrigger = true;
            b.visual = new GameObject("Visual").transform; b.visual.SetParent(go.transform, false);
            Look.Quad("Edge", Vector2.zero, size, Look.WoodLight, 30, b.visual);
            Look.Quad("Fill", Vector2.zero, size - new Vector2(0.08f, 0.08f), Look.Wood, 31, b.visual);
            Look.Quad("NailL", new Vector2(-size.x * 0.5f + 0.12f, size.y * 0.5f - 0.12f), new Vector2(0.06f, 0.06f), new Color(0.75f, 0.7f, 0.6f), 32, b.visual);
            Look.Quad("NailR", new Vector2(size.x * 0.5f - 0.12f, size.y * 0.5f - 0.12f), new Vector2(0.06f, 0.06f), new Color(0.75f, 0.7f, 0.6f), 32, b.visual);
            b.label = Ui.Text("Label", canvas, Vector2.zero, new Vector2(400f, 60f), 26, TextAnchor.MiddleCenter, Ui.Ink);
            return b;
        }

        /// <summary>그림 없는 과녁 (10-16: 벽의 수배 포스터처럼 그림은 따로 있고 맞는 자리만 필요할 때)</summary>
        public static ShootButton Bare(string name, System.Action onShot)
        {
            var go = new GameObject("Target_" + name);
            var b = go.AddComponent<ShootButton>();
            b.OnShot = onShot;
            var col = go.AddComponent<BoxCollider2D>(); col.isTrigger = true;
            b.visual = new GameObject("Visual").transform; b.visual.SetParent(go.transform, false);
            return b;
        }

        public void Shot()
        {
            OnShot?.Invoke();
            Sfx.Play(Sfx.Tang(), 0.5f, 1.3f);
            StopAllCoroutines(); StartCoroutine(Wobble());
        }

        IEnumerator Wobble()
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime * 3f)
            {
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 4f) * 8f * (1f - t));
                visual.localScale = Vector3.one * (1f - Mathf.Sin(t * Mathf.PI) * 0.12f);
                yield return null;
            }
            visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
        }

        void LateUpdate()
        {
            if (label == null) return;
            label.text = text();
            var sp = Camera.main.WorldToScreenPoint(transform.position);
            ((RectTransform)label.transform).anchoredPosition = new Vector2(sp.x - Screen.width * 0.5f, sp.y - Screen.height * 0.5f) * (1080f / Screen.height);
        }

        void OnDestroy() { if (label != null) Destroy(label.gameObject); }
    }
}
