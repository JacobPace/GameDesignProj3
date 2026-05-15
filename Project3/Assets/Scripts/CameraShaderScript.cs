    using UnityEngine;
    using UnityEngine.Rendering.Universal;

    public class CameraShaderScript : MonoBehaviour
    {
        public UniversalRendererData rendererData;
        public string featureName = "VideoTapeShader";

        private ScriptableRendererFeature targetFeature;

        void Start()
        {
            foreach (var feature in rendererData.rendererFeatures)
            {
                if (feature.name == featureName)
                {
                    targetFeature = feature;
                    break;
                }
            }

            if (targetFeature != null)
                targetFeature.SetActive(false);
        }

        public void EnableEffect()
        {
            if (targetFeature != null)
                targetFeature.SetActive(true);
        }

        public void DisableEffect()
        {
            if (targetFeature != null)
                targetFeature.SetActive(false);
        }
    }