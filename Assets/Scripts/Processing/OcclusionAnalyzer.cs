using UnityEngine;
using BiometricAuth.Data;

namespace BiometricAuth.Processing
{
    public struct OcclusionReport
    {
        public bool MaskDetected;
        public bool GlassesDetected;
        public bool LowConfidenceFace;
        public bool LivenessPassed;
        public float EyesConfidence;
        public float NoseConfidence;
        public float MouthConfidence;
        public float JawConfidence;
        public float EyeOpennessVariance;
        public float StabilityScore;
    }

    public class OcclusionAnalyzer
    {
        private const int HistorySize = 12;
        private readonly float[] eyeOpennessHistory = new float[HistorySize];
        private readonly Vector3[] poseHistory = new Vector3[HistorySize];
        private int historyIndex;
        private int historyCount;
        private float previousEyeOpenness;
        private bool blinkObserved;

        private static readonly int[] EyeIndices = { 33, 133, 159, 145, 263, 362, 386, 374, 36, 39, 37, 41, 42, 45, 43, 47 };
        private static readonly int[] NoseIndices = { 98, 327, 1, 30, 31, 35 };
        private static readonly int[] MouthIndices = { 61, 291, 48, 54, 62, 66 };
        private static readonly int[] JawIndices = { 234, 454, 0, 16, 152, 8 };

        public OcclusionReport Analyze(FaceObservation observation, FeatureVector feature)
        {
            OcclusionReport report = default;
            if (observation == null || observation.LandmarkCount == 0)
            {
                report.LowConfidenceFace = true;
                return report;
            }

            report.EyesConfidence = observation.GetAverageConfidence(EyeIndices);
            report.NoseConfidence = observation.GetAverageConfidence(NoseIndices);
            report.MouthConfidence = observation.GetAverageConfidence(MouthIndices);
            report.JawConfidence = observation.GetAverageConfidence(JawIndices);
            report.LowConfidenceFace = observation.GlobalConfidence < 0.55f || report.EyesConfidence < 0.5f || report.NoseConfidence < 0.45f;
            report.MaskDetected = report.MouthConfidence < 0.45f || report.JawConfidence < 0.38f;

            UpdateHistory(feature);
            report.EyeOpennessVariance = CalculateOpennessVariance();
            report.GlassesDetected = report.EyesConfidence < 0.68f && report.EyeOpennessVariance > 0.005f;
            report.LivenessPassed = EvaluateLiveness(feature, report.EyeOpennessVariance);
            report.StabilityScore = CalculateMotionVariance();
            return report;
        }

        private void UpdateHistory(FeatureVector feature)
        {
            eyeOpennessHistory[historyIndex] = feature.EyeOpenness;
            poseHistory[historyIndex] = new Vector3(feature.Yaw, feature.Pitch, feature.Roll);
            historyIndex = (historyIndex + 1) % HistorySize;
            historyCount = Mathf.Min(historyCount + 1, HistorySize);
            previousEyeOpenness = feature.EyeOpenness;
        }

        private bool EvaluateLiveness(FeatureVector feature, float opennessVariance)
        {
            bool blink = DetectBlink(feature.EyeOpenness);
            float motionVariance = CalculateMotionVariance();

            bool movementLiveness = motionVariance > 0.75f || opennessVariance > 0.007f;
            bool staticPenalty = motionVariance < 0.08f && opennessVariance < 0.0025f;

            bool livenessDetected = blink || movementLiveness;
            return livenessDetected && !staticPenalty;
        }

        private bool DetectBlink(float currentEyeOpenness)
        {
            bool blink = !blinkObserved && previousEyeOpenness > 0.18f && currentEyeOpenness < 0.12f;
            if (blink)
            {
                blinkObserved = true;
            }

            if (blinkObserved && currentEyeOpenness > 0.18f)
            {
                blinkObserved = false;
            }

            return blink;
        }

        private float CalculateMotionVariance()
        {
            if (historyCount < 2)
            {
                return 0f;
            }

            Vector3 mean = Vector3.zero;
            for (int i = 0; i < historyCount; i++)
            {
                mean += poseHistory[i];
            }

            mean /= historyCount;
            float variance = 0f;

            for (int i = 0; i < historyCount; i++)
            {
                Vector3 delta = poseHistory[i] - mean;
                variance += delta.sqrMagnitude;
            }

            return variance / historyCount;
        }

        private float CalculateOpennessVariance()
        {
            if (historyCount < 2)
            {
                return 0f;
            }

            float sum = 0f;
            for (int i = 0; i < historyCount; i++)
            {
                sum += eyeOpennessHistory[i];
            }

            float mean = sum / historyCount;
            float variance = 0f;

            for (int i = 0; i < historyCount; i++)
            {
                float delta = eyeOpennessHistory[i] - mean;
                variance += delta * delta;
            }

            return variance / historyCount;
        }
    }
}
