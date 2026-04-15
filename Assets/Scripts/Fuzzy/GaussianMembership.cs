using UnityEngine;

namespace BiometricAuth.Fuzzy
{
    public static class GaussianMembership
    {
        public static float Evaluate(float x, float center, float sigma)
        {
            float variance = sigma * sigma;
            if (variance <= 1e-5f)
            {
                return Mathf.Abs(x - center) < 0.05f ? 1f : 0f;
            }

            float delta = x - center;
            return Mathf.Exp(-(delta * delta) / (2f * variance));
        }
    }
}
