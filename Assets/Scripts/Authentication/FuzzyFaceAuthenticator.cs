using UnityEngine;
using BiometricAuth.Data;
using BiometricAuth.Enrollment;
using BiometricAuth.Fuzzy;
using BiometricAuth.Processing;

namespace BiometricAuth.Authentication
{
    public class FuzzyFaceAuthenticator
    {
        private readonly EnrollmentManager enrollmentManager;
        private readonly OcclusionAnalyzer occlusionAnalyzer;
        private readonly FuzzyAssociativeMemory fuzzyMemory;

        public FuzzyFaceAuthenticator(EnrollmentManager enrollmentManager, OcclusionAnalyzer occlusionAnalyzer, FuzzyAssociativeMemory fuzzyMemory)
        {
            this.enrollmentManager = enrollmentManager;
            this.occlusionAnalyzer = occlusionAnalyzer;
            this.fuzzyMemory = fuzzyMemory;
        }

        public bool Authenticate(FaceObservation observation, FeatureVector features, out string matchedUserId, out float score)
        {
            matchedUserId = null;
            score = 0f;

            if (observation == null || observation.LandmarkCount == 0)
            {
                return false;
            }

            OcclusionReport report = occlusionAnalyzer.Analyze(observation, features);
            if (report.LowConfidenceFace || !report.LivenessPassed)
            {
                return false;
            }

            EnrollmentManager.EnrollmentProfile[] profiles = enrollmentManager.LoadedProfiles;
            if (profiles == null)
            {
                return false;
            }

            float bestScore = 0f;
            string bestId = null;

            for (int i = 0; i < profiles.Length; i++)
            {
                EnrollmentManager.EnrollmentProfile profile = profiles[i];
                if (profile == null || string.IsNullOrEmpty(profile.UserId))
                {
                    continue;
                }

                float fallback;
                float rawScore = fuzzyMemory.Evaluate(features, profile, report, out fallback);
                rawScore *= ComputeSecurityPenalty(observation, report);

                if (rawScore > bestScore)
                {
                    bestScore = rawScore;
                    bestId = profile.UserId;
                }
            }

            score = bestScore;
            if (bestScore > 0.78f)
            {
                matchedUserId = bestId;
                return true;
            }

            return false;
        }

        private float ComputeSecurityPenalty(FaceObservation observation, OcclusionReport report)
        {
            float penalty = 1f;
            if (observation.GlobalConfidence < 0.68f)
            {
                penalty *= 0.85f;
            }

            if (report.MaskDetected)
            {
                penalty *= 0.82f;
            }

            if (report.GlassesDetected)
            {
                penalty *= 0.9f;
            }

            if (report.StabilityScore < 0.1f)
            {
                penalty *= 0.7f;
            }

            return penalty;
        }
    }
}
