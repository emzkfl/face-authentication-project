using UnityEngine;
using BiometricAuth.Data;
using BiometricAuth.Processing;

namespace BiometricAuth.Fuzzy
{
    public class FuzzyAssociativeMemory
    {
        public float Evaluate(FeatureVector candidate, Enrollment.EnrollmentManager.EnrollmentProfile model, OcclusionReport occlusion, out float weightedAverage)
        {
            float eyeWeight = 1.0f;
            float browWeight = 0.75f;
            float noseWeight = 0.9f;
            float noseToChinWeight = 0.85f;
            float mouthWeight = 0.9f;
            float jawWeight = 0.9f;
            float opennessWeight = 0.8f;
            float aspectWeight = 0.7f;

            if (occlusion.MaskDetected)
            {
                mouthWeight = 0f;
                jawWeight = 0f;
                eyeWeight *= 1.25f;
                noseWeight *= 1.2f;
            }

            if (occlusion.GlassesDetected)
            {
                eyeWeight *= 0.82f;
            }

            float muEyeDistance = GaussianMembership.Evaluate(candidate.EyeDistance, model.Baseline.EyeDistance, model.Sigma.EyeDistance);
            float muBrowDistance = GaussianMembership.Evaluate(candidate.BrowDistance, model.Baseline.BrowDistance, model.Sigma.BrowDistance);
            float muNoseWidth = GaussianMembership.Evaluate(candidate.NoseWidth, model.Baseline.NoseWidth, model.Sigma.NoseWidth);
            float muNoseToChin = GaussianMembership.Evaluate(candidate.NoseToChinRatio, model.Baseline.NoseToChinRatio, model.Sigma.NoseToChinRatio);
            float muMouthWidth = GaussianMembership.Evaluate(candidate.MouthWidth, model.Baseline.MouthWidth, model.Sigma.MouthWidth);
            float muJawWidth = GaussianMembership.Evaluate(candidate.JawWidth, model.Baseline.JawWidth, model.Sigma.JawWidth);
            float muEyeOpenness = GaussianMembership.Evaluate(candidate.EyeOpenness, model.Baseline.EyeOpenness, model.Sigma.EyeOpenness);
            float muAspect = GaussianMembership.Evaluate(candidate.FaceAspectRatio, model.Baseline.FaceAspectRatio, model.Sigma.FaceAspectRatio);
            float muYaw = GaussianMembership.Evaluate(candidate.Yaw, model.Baseline.Yaw, model.Sigma.Yaw);
            float muPitch = GaussianMembership.Evaluate(candidate.Pitch, model.Baseline.Pitch, model.Sigma.Pitch);
            float muRoll = GaussianMembership.Evaluate(candidate.Roll, model.Baseline.Roll, model.Sigma.Roll);

            float scoreEye = Mathf.Min(muEyeDistance, eyeWeight);
            float scoreBrow = Mathf.Min(muBrowDistance, browWeight);
            float scoreNose = Mathf.Min(muNoseWidth, noseWeight);
            float scoreNoseChin = Mathf.Min(muNoseToChin, noseToChinWeight);
            float scoreMouth = Mathf.Min(muMouthWidth, mouthWeight);
            float scoreJaw = Mathf.Min(muJawWidth, jawWeight);
            float scoreOpenness = Mathf.Min(muEyeOpenness, opennessWeight);
            float scoreAspect = Mathf.Min(muAspect, aspectWeight);
            float scorePose = Mathf.Min(Mathf.Min(muYaw, muPitch), muRoll);

            float maxMinScore = scoreEye;
            maxMinScore = Mathf.Max(maxMinScore, scoreBrow);
            maxMinScore = Mathf.Max(maxMinScore, scoreNose);
            maxMinScore = Mathf.Max(maxMinScore, scoreNoseChin);
            maxMinScore = Mathf.Max(maxMinScore, scoreMouth);
            maxMinScore = Mathf.Max(maxMinScore, scoreJaw);
            maxMinScore = Mathf.Max(maxMinScore, scoreOpenness);
            maxMinScore = Mathf.Max(maxMinScore, scoreAspect);
            maxMinScore = Mathf.Max(maxMinScore, scorePose);

            float sumWeights = eyeWeight + browWeight + noseWeight + noseToChinWeight + mouthWeight + jawWeight + opennessWeight + aspectWeight + 1f;
            float sumWeightedMu = eyeWeight * muEyeDistance + browWeight * muBrowDistance + noseWeight * muNoseWidth + noseToChinWeight * muNoseToChin + mouthWeight * muMouthWidth + jawWeight * muJawWidth + opennessWeight * muEyeOpenness + aspectWeight * muAspect + 1f * scorePose;
            weightedAverage = sumWeights <= 0f ? 0f : sumWeightedMu / sumWeights;

            return Mathf.Max(maxMinScore, weightedAverage * 0.92f);
        }
    }
}
