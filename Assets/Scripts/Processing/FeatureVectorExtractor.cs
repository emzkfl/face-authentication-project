using UnityEngine;
using BiometricAuth.Data;

namespace BiometricAuth.Processing
{
    public class FeatureVectorExtractor
    {
        private static readonly int[] EyeIndices = { 33, 133, 159, 145, 263, 362, 386, 374, 36, 39, 37, 41, 42, 45, 43, 47 };
        private static readonly int[] BrowIndices = { 70, 300, 21, 22 };
        private static readonly int[] NoseIndices = { 98, 327, 30, 31, 35, 1 };
        private static readonly int[] MouthIndices = { 61, 291, 48, 54 };
        private static readonly int[] JawIndices = { 234, 454, 0, 16 };

        public void Extract(FaceObservation observation, out FeatureVector features)
        {
            features = default;
            if (observation == null || observation.LandmarkCount == 0)
            {
                return;
            }

            Vector3 leftEye = GetEyeCenter(observation, true);
            Vector3 rightEye = GetEyeCenter(observation, false);
            Vector3 leftBrow = GetAveragePoint(observation, 70, 21);
            Vector3 rightBrow = GetAveragePoint(observation, 300, 22);
            Vector3 noseLeft = observation.GetLandmark(GetPreferredIndex(observation, 98, 31));
            Vector3 noseRight = observation.GetLandmark(GetPreferredIndex(observation, 327, 35));
            Vector3 noseTip = observation.GetLandmark(GetPreferredIndex(observation, 1, 30));
            Vector3 chin = observation.GetLandmark(GetPreferredIndex(observation, 152, 8));
            Vector3 mouthLeft = observation.GetLandmark(GetPreferredIndex(observation, 61, 48));
            Vector3 mouthRight = observation.GetLandmark(GetPreferredIndex(observation, 291, 54));
            Vector3 jawLeft = observation.GetLandmark(GetPreferredIndex(observation, 234, 0));
            Vector3 jawRight = observation.GetLandmark(GetPreferredIndex(observation, 454, 16));
            Vector3 leftUpper = observation.GetLandmark(GetPreferredIndex(observation, 159, 37));
            Vector3 leftLower = observation.GetLandmark(GetPreferredIndex(observation, 145, 41));
            Vector3 rightUpper = observation.GetLandmark(GetPreferredIndex(observation, 386, 43));
            Vector3 rightLower = observation.GetLandmark(GetPreferredIndex(observation, 374, 47));

            features.EyeDistance = Vector3.Distance(leftEye, rightEye);
            features.BrowDistance = Vector3.Distance(leftBrow, rightBrow);
            features.NoseWidth = Vector3.Distance(noseLeft, noseRight);
            features.NoseToChinRatio = Distance(noseTip, chin) / Mathf.Max(features.EyeDistance, 1f);
            features.MouthWidth = Vector3.Distance(mouthLeft, mouthRight);
            features.JawWidth = Vector3.Distance(jawLeft, jawRight);
            features.EyeOpenness = (Distance(leftUpper, leftLower) + Distance(rightUpper, rightLower)) * 0.5f;
            features.FaceAspectRatio = CalculateAspectRatio(leftEye, rightEye, chin, noseTip);
            CalculatePose(leftEye, rightEye, noseTip, chin, ref features);

            float scale = Mathf.Max(features.EyeDistance, 1f);
            features = features.Normalize(scale);
        }

        private static int GetPreferredIndex(FaceObservation observation, int primary, int fallback)
        {
            if (primary >= 0 && primary < observation.LandmarkCount)
            {
                return primary;
            }

            if (fallback >= 0 && fallback < observation.LandmarkCount)
            {
                return fallback;
            }

            return 0;
        }

        private static Vector3 GetEyeCenter(FaceObservation observation, bool left)
        {
            if (left)
            {
                Vector3 outer = observation.GetLandmark(GetPreferredIndex(observation, 33, 36));
                Vector3 inner = observation.GetLandmark(GetPreferredIndex(observation, 133, 39));
                return (outer + inner) * 0.5f;
            }

            Vector3 outerR = observation.GetLandmark(GetPreferredIndex(observation, 263, 45));
            Vector3 innerR = observation.GetLandmark(GetPreferredIndex(observation, 362, 42));
            return (outerR + innerR) * 0.5f;
        }

        private static Vector3 GetAveragePoint(FaceObservation observation, int first, int second)
        {
            Vector3 a = observation.GetLandmark(GetPreferredIndex(observation, first, first));
            Vector3 b = observation.GetLandmark(GetPreferredIndex(observation, second, second));
            return (a + b) * 0.5f;
        }

        private static float Distance(Vector3 a, Vector3 b)
        {
            return Vector3.Distance(a, b);
        }

        private static float CalculateAspectRatio(Vector3 leftEye, Vector3 rightEye, Vector3 chin, Vector3 noseTip)
        {
            float width = Vector3.Distance(leftEye, rightEye);
            float height = Vector3.Distance(noseTip, chin);
            return height <= 0f ? 1f : Mathf.Clamp(width / height, 0.4f, 2.5f);
        }

        private static void CalculatePose(Vector3 leftEye, Vector3 rightEye, Vector3 noseTip, Vector3 chin, ref FeatureVector vector)
        {
            Vector3 eyeVector = rightEye - leftEye;
            vector.Roll = Mathf.Atan2(eyeVector.y, eyeVector.x) * Mathf.Rad2Deg;

            Vector3 noseVector = chin - noseTip;
            vector.Pitch = Mathf.Atan2(noseVector.y, Mathf.Max(Mathf.Abs(noseVector.z), 0.001f)) * Mathf.Rad2Deg;
            vector.Yaw = Mathf.Atan2(noseVector.x, Mathf.Max(Mathf.Abs(noseVector.z), 0.001f)) * Mathf.Rad2Deg;
        }
    }
}
