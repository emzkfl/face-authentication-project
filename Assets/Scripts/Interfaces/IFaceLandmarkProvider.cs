using UnityEngine;

namespace BiometricAuth.Interfaces
{
    public interface IFaceLandmarkProvider
    {
        bool TryGetObservation(out BiometricAuth.Data.FaceObservation observation);
        void Initialize(WebCamTexture texture);
        void Dispose();
    }
}
