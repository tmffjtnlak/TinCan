using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TinCan
{
    /// <summary>
    /// 복셀 총을 도트처럼 (사용자 10-11): 총만 따로 낮은 해상도로 그린 뒤 점 필터로 키워 화면에 붙인다.
    /// 화면 전체 도트화가 아니라 이 층(레이어 30)에 있는 것만. 한 도트 = PixelSize 화면 픽셀.
    /// </summary>
    public class PixelLayer : MonoBehaviour
    {
        public const int Layer = 30;
        public static int PixelSize = 4;          // 1 도트 = 화면 4px (720p 기준 320×180)
        static PixelLayer inst;
        Camera pixCam; RenderTexture rt; Material mat; MeshRenderer quad;
        int lastW, lastH, lastScale;

        public static void Ensure()
        {
            if (inst != null) return;
            var cam = Camera.main; if (cam == null) return;
            var go = new GameObject("PixelLayer");
            go.transform.SetParent(cam.transform, false);
            inst = go.AddComponent<PixelLayer>();
            inst.Build(cam);
        }

        /// <summary>이 오브젝트(와 자식)를 도트 층으로 보낸다</summary>
        public static void Mark(GameObject go)
        {
            Ensure();
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
        }

        void Build(Camera main)
        {
            main.cullingMask &= ~(1 << Layer);
            var cg = new GameObject("PixelCam"); cg.transform.SetParent(transform, false);
            pixCam = cg.AddComponent<Camera>();
            pixCam.orthographic = true; pixCam.cullingMask = 1 << Layer; pixCam.clearFlags = CameraClearFlags.SolidColor; pixCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            pixCam.depth = main.depth - 1; pixCam.nearClipPlane = main.nearClipPlane; pixCam.farClipPlane = main.farClipPlane;
            var data = pixCam.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false;
            mat = new Material(Shader.Find("TinCan/PixelLayer"));
            var qg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(qg.GetComponent<Collider>());
            qg.name = "PixelQuad"; qg.transform.SetParent(transform, false);
            qg.transform.localPosition = new Vector3(0f, 0f, 5f);   // 카메라 앞
            quad = qg.GetComponent<MeshRenderer>(); quad.sharedMaterial = mat; quad.sortingOrder = 45;
            quad.gameObject.layer = 0;
            Resize(main);
        }

        void Resize(Camera main)
        {
            int w = Mathf.Max(8, Screen.width / PixelSize), h = Mathf.Max(8, Screen.height / PixelSize);
            if (rt != null && w == lastW && h == lastH && PixelSize == lastScale) return;
            if (rt != null) rt.Release();
            rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            rt.Create();
            pixCam.targetTexture = rt; mat.mainTexture = rt;
            lastW = w; lastH = h; lastScale = PixelSize;
        }

        void LateUpdate()
        {
            var main = Camera.main; if (main == null) return;
            Resize(main);
            pixCam.orthographicSize = main.orthographicSize;
            float hgt = main.orthographicSize * 2f, wid = hgt * main.aspect;
            quad.transform.localScale = new Vector3(wid, hgt, 1f);
        }
    }
}
