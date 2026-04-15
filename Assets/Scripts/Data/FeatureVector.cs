using System;
using UnityEngine;

namespace BiometricAuth.Data
{
    [Serializable]
    public struct FeatureVector
    {
        public float EyeDistance;
        public float BrowDistance;
        public float NoseWidth;
        public float NoseToChinRatio;
        public float MouthWidth;
        public float JawWidth;
        public float EyeOpenness;
        public float FaceAspectRatio;
        public float Yaw;
        public float Pitch;
        public float Roll;

        public FeatureVector Normalize(float scale)
        {
            if (scale <= 0f)
            {
                return this;
            }

            FeatureVector normalized = this;
            normalized.EyeDistance /= scale;
            normalized.BrowDistance /= scale;
            normalized.NoseWidth /= scale;
            normalized.NoseToChinRatio /= scale;
            normalized.MouthWidth /= scale;
            normalized.JawWidth /= scale;
            normalized.EyeOpenness /= scale;
            return normalized;
        }
    }
}
