using System;
using System.IO;
using UnityEngine;
using BiometricAuth.Data;

namespace BiometricAuth.Enrollment
{
    public class EnrollmentManager
    {
        private const int MaxProfiles = 3;
        private const string StorageFileName = "face_enrollment.json";

        [Serializable]
        public class EnrollmentProfile
        {
            public string UserId;
            public FeatureVector Baseline;
            public FeatureVector Sigma;
        }

        [Serializable]
        private class EnrollmentStore
        {
            public EnrollmentProfile[] Profiles;
        }

        private readonly EnrollmentProfile[] profiles = new EnrollmentProfile[MaxProfiles];
        private readonly EnrollmentRecorder[] recorders = new EnrollmentRecorder[MaxProfiles];

        public EnrollmentManager()
        {
            for (int i = 0; i < MaxProfiles; i++)
            {
                recorders[i] = new EnrollmentRecorder();
            }
        }

        public EnrollmentProfile[] LoadedProfiles => profiles;

        public void Initialize()
        {
            Load();
        }

        public bool AddEnrollmentSample(string userId, FeatureVector sample)
        {
            int index = FindProfileIndex(userId);
            if (index < 0)
            {
                index = CreateProfile(userId);
                if (index < 0)
                {
                    return false;
                }
            }

            recorders[index].Add(sample);
            return true;
        }

        public bool CommitEnrollment(string userId)
        {
            int index = FindProfileIndex(userId);
            if (index < 0 || profiles[index] == null)
            {
                return false;
            }

            if (recorders[index].Count < 4)
            {
                return false;
            }

            recorders[index].CommitToProfile(profiles[index]);
            Save();
            return true;
        }

        public string[] GetUserIds()
        {
            int count = 0;
            for (int i = 0; i < MaxProfiles; i++)
            {
                if (profiles[i] != null && !string.IsNullOrEmpty(profiles[i].UserId))
                {
                    count++;
                }
            }

            string[] ids = new string[count];
            int insert = 0;
            for (int i = 0; i < MaxProfiles; i++)
            {
                if (profiles[i] != null && !string.IsNullOrEmpty(profiles[i].UserId))
                {
                    ids[insert++] = profiles[i].UserId;
                }
            }

            return ids;
        }

        private int FindProfileIndex(string userId)
        {
            for (int i = 0; i < MaxProfiles; i++)
            {
                if (profiles[i] != null && profiles[i].UserId == userId)
                {
                    return i;
                }
            }

            return -1;
        }

        private int CreateProfile(string userId)
        {
            for (int i = 0; i < MaxProfiles; i++)
            {
                if (profiles[i] == null || string.IsNullOrEmpty(profiles[i].UserId))
                {
                    profiles[i] = new EnrollmentProfile { UserId = userId, Baseline = new FeatureVector(), Sigma = new FeatureVector() };
                    recorders[i].Reset();
                    return i;
                }
            }

            return -1;
        }

        private void Save()
        {
            try
            {
                EnrollmentStore store = new EnrollmentStore { Profiles = profiles };
                string json = JsonUtility.ToJson(store, true);
                File.WriteAllText(StoragePath, json);
            }
            catch (Exception)
            {
                // Persistent storage failure should not crash runtime.
            }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(StoragePath))
                {
                    return;
                }

                string json = File.ReadAllText(StoragePath);
                EnrollmentStore store = JsonUtility.FromJson<EnrollmentStore>(json);
                if (store?.Profiles == null)
                {
                    return;
                }

                int copy = Mathf.Min(store.Profiles.Length, MaxProfiles);
                for (int i = 0; i < copy; i++)
                {
                    profiles[i] = store.Profiles[i];
                    recorders[i].Reset();
                }

                for (int i = copy; i < MaxProfiles; i++)
                {
                    profiles[i] = null;
                    recorders[i].Reset();
                }
            }
            catch (Exception)
            {
                // Corrupt or unavailable storage should not block runtime.
            }
        }

        private string StoragePath => Path.Combine(Application.persistentDataPath, StorageFileName);

        [Serializable]
        private struct EnrollmentRecorder
        {
            public int Count;
            public FeatureVector Mean;
            public FeatureVector M2;

            public void Reset()
            {
                Count = 0;
                Mean = default;
                M2 = default;
            }

            public void Add(FeatureVector sample)
            {
                Count++;
                UpdateSample(ref Mean.EyeDistance, ref M2.EyeDistance, sample.EyeDistance);
                UpdateSample(ref Mean.BrowDistance, ref M2.BrowDistance, sample.BrowDistance);
                UpdateSample(ref Mean.NoseWidth, ref M2.NoseWidth, sample.NoseWidth);
                UpdateSample(ref Mean.NoseToChinRatio, ref M2.NoseToChinRatio, sample.NoseToChinRatio);
                UpdateSample(ref Mean.MouthWidth, ref M2.MouthWidth, sample.MouthWidth);
                UpdateSample(ref Mean.JawWidth, ref M2.JawWidth, sample.JawWidth);
                UpdateSample(ref Mean.EyeOpenness, ref M2.EyeOpenness, sample.EyeOpenness);
                UpdateSample(ref Mean.FaceAspectRatio, ref M2.FaceAspectRatio, sample.FaceAspectRatio);
                UpdateSample(ref Mean.Yaw, ref M2.Yaw, sample.Yaw);
                UpdateSample(ref Mean.Pitch, ref M2.Pitch, sample.Pitch);
                UpdateSample(ref Mean.Roll, ref M2.Roll, sample.Roll);
            }

            public void CommitToProfile(EnrollmentProfile profile)
            {
                profile.Baseline = Mean;
                profile.Sigma = new FeatureVector
                {
                    EyeDistance = ComputeSigma(M2.EyeDistance),
                    BrowDistance = ComputeSigma(M2.BrowDistance),
                    NoseWidth = ComputeSigma(M2.NoseWidth),
                    NoseToChinRatio = ComputeSigma(M2.NoseToChinRatio),
                    MouthWidth = ComputeSigma(M2.MouthWidth),
                    JawWidth = ComputeSigma(M2.JawWidth),
                    EyeOpenness = ComputeSigma(M2.EyeOpenness),
                    FaceAspectRatio = ComputeSigma(M2.FaceAspectRatio),
                    Yaw = ComputeSigma(M2.Yaw),
                    Pitch = ComputeSigma(M2.Pitch),
                    Roll = ComputeSigma(M2.Roll)
                };
            }

            private void UpdateSample(ref float mean, ref float m2, float value)
            {
                if (Count == 1)
                {
                    mean = value;
                    m2 = 0f;
                    return;
                }

                float delta = value - mean;
                mean += delta / Count;
                m2 += delta * (value - mean);
            }

            private float ComputeSigma(float accumulatedM2)
            {
                if (accumulatedM2 <= 0f)
                {
                    return 0.05f;
                }

                return Mathf.Max(0.05f, Mathf.Sqrt(accumulatedM2 / Mathf.Max(1, Count - 1)));
            }
        }
    }
}
