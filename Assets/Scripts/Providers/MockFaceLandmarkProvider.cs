using UnityEngine;
using BiometricAuth.Data;
using BiometricAuth.Interfaces;

namespace BiometricAuth.Providers
{
    public class MockFaceLandmarkProvider : IFaceLandmarkProvider
    {
        private WebCamTexture webcam;
        private const int LandmarkCount = 68;

        public void Initialize(WebCamTexture texture)
        {
            webcam = texture;
        }

        public void Dispose()
        {
            // No resources reserved in mock provider.
        }

        public bool TryGetObservation(out FaceObservation observation)
        {
            observation = new FaceObservation(LandmarkCount);
            if (webcam == null || !webcam.isPlaying)
            {
                return false;
            }

            float width = Mathf.Max(webcam.width, 1);
            float height = Mathf.Max(webcam.height, 1);
            Vector3 center = new Vector3(width * 0.5f, height * 0.45f, 0f);
            float radius = Mathf.Min(width, height) * 0.18f;

            for (int i = 0; i < LandmarkCount; i++)
            {
                float angle = (2f * Mathf.PI * i) / LandmarkCount;
                observation.Landmarks[i] = center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.9f, 0f) * radius;
                observation.Confidences[i] = 0.92f;
            }

            observation.GlobalConfidence = 0.92f;
            return true;
        }
    }
}
