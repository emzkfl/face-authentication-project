using UnityEngine;

namespace BiometricAuth.Data
{
    public class FaceObservation
    {
        public Vector3[] Landmarks;
        public float[] Confidences;
        public float GlobalConfidence;
        public float Timestamp;

        public FaceObservation(int landmarkCount)
        {
            Landmarks = new Vector3[landmarkCount];
            Confidences = new float[landmarkCount];
            GlobalConfidence = 0f;
            Timestamp = Time.realtimeSinceStartup;
        }

        public int LandmarkCount => Landmarks != null ? Landmarks.Length : 0;

        public float GetAverageConfidence(int[] indices)
        {
            float sum = 0f;
            int count = 0;

            for (int i = 0; i < indices.Length; i++)
            {
                int index = indices[i];
                if (index >= 0 && index < Confidences.Length)
                {
                    sum += Confidences[index];
                    count++;
                }
            }

            return count == 0 ? 0f : sum / count;
        }

        public Vector3 GetLandmark(int index)
        {
            if (index < 0 || index >= LandmarkCount)
            {
                return Vector3.zero;
            }

            return Landmarks[index];
        }
    }
}
