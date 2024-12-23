using UnityEngine;
using UnityEngine.Rendering;

namespace LynxRP
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public class LynxRenderPipelineCamera : MonoBehaviour
    {
        [SerializeField]
        CameraSettings settings = default;

        ProfilingSampler sampler;
        
        public ProfilingSampler Sampler => sampler ??= new(GetComponent<Camera>().name);

        public CameraSettings Settings => settings ??= new();

        #if UNITY_EDITOR || DEVELOPMENT_BUILD
            void OnEnable() => sampler = null;
        #endif
    }
}
