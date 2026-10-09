using UnityEngine;

namespace TinCan
{
    /// <summary>씬에 이것 하나만 있으면 된다. 카메라를 잡고 Game을 만든다.</summary>
    public class Bootstrap : MonoBehaviour
    {
        void Awake()
        {
            var cam = Camera.main;
            if (cam == null) { var cg = new GameObject("Main Camera") { tag = "MainCamera" }; cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>(); }
            cam.orthographic = true; cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            Application.targetFrameRate = 120;
            new GameObject("Game").AddComponent<Game>();
        }
    }
}
