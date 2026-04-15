using UnityEngine;
using UnityEngine.UI;
using BiometricAuth.Interfaces;
using BiometricAuth.Data;
using BiometricAuth.Processing;
using BiometricAuth.Enrollment;
using BiometricAuth.Fuzzy;
using BiometricAuth.Authentication;
using BiometricAuth.Providers;

public class FaceAuthenticationController : MonoBehaviour
{
    [Header("Camera")]
    public RawImage cameraPreview;
    public int cameraIndex;
    public int targetFrameRate = 30;
    public int frameSkip = 1;
    public bool useMockProvider = true;

    [Header("UI")]
    public Text statusText;

    [Header("Enrollment")]
    public string activeEnrollmentId = "User1";
    public int requiredEnrollmentSamples = 24;

    private WebCamTexture webcamTexture;
    private IFaceLandmarkProvider landmarkProvider;
    private FeatureVectorExtractor extractor;
    private OcclusionAnalyzer occlusionAnalyzer;
    private EnrollmentManager enrollmentManager;
    private FuzzyAssociativeMemory fuzzyMemory;
    private FuzzyFaceAuthenticator authenticator;
    private int framesSinceLastProcess;
    private int enrollmentFramesCollected;
    private bool enrollmentActive;

    private void Awake()
    {
        extractor = new FeatureVectorExtractor();
        occlusionAnalyzer = new OcclusionAnalyzer();
        enrollmentManager = new EnrollmentManager();
        enrollmentManager.Initialize();
        fuzzyMemory = new FuzzyAssociativeMemory();
        authenticator = new FuzzyFaceAuthenticator(enrollmentManager, occlusionAnalyzer, fuzzyMemory);
    }

    private void Start()
    {
        Application.targetFrameRate = targetFrameRate;
        StartCamera();
        CreateLandmarkProvider();
        UpdateStatus("Authentication ready.");
    }

    private void Update()
    {
        if (webcamTexture == null || !webcamTexture.isPlaying || landmarkProvider == null)
        {
            return;
        }

        framesSinceLastProcess++;
        if (framesSinceLastProcess < frameSkip)
        {
            return;
        }

        framesSinceLastProcess = 0;
        ProcessFrame();
        HandleDebugInput();
    }

    private void StartCamera()
    {
        if (WebCamTexture.devices.Length == 0)
        {
            UpdateStatus("No camera detected.");
            return;
        }

        string deviceName = WebCamTexture.devices[Mathf.Clamp(cameraIndex, 0, WebCamTexture.devices.Length - 1)].name;
        webcamTexture = new WebCamTexture(deviceName, 640, 480, targetFrameRate);
        webcamTexture.Play();

        if (cameraPreview != null)
        {
            cameraPreview.texture = webcamTexture;
        }
    }

    private void CreateLandmarkProvider()
    {
        if (useMockProvider)
        {
            landmarkProvider = new MockFaceLandmarkProvider();
        }
        else
        {
            landmarkProvider = new MediaPipeFaceLandmarkProvider();
        }

        landmarkProvider.Initialize(webcamTexture);
    }

    private void ProcessFrame()
    {
        if (!landmarkProvider.TryGetObservation(out FaceObservation observation))
        {
            UpdateStatus("Unable to obtain landmarks.");
            return;
        }

        if (observation.LandmarkCount == 0)
        {
            UpdateStatus("No face detected.");
            return;
        }

        FeatureVector features;
        extractor.Extract(observation, out features);

        if (enrollmentActive)
        {
            if (enrollmentManager.AddEnrollmentSample(activeEnrollmentId, features))
            {
                enrollmentFramesCollected++;
                if (enrollmentFramesCollected >= requiredEnrollmentSamples)
                {
                    bool committed = enrollmentManager.CommitEnrollment(activeEnrollmentId);
                    enrollmentActive = false;
                    enrollmentFramesCollected = 0;
                    UpdateStatus(committed ? $"Enrollment complete: {activeEnrollmentId}" : "Enrollment failed: insufficient stability.");
                }
                else
                {
                    UpdateStatus($"Enrolling {activeEnrollmentId}: {enrollmentFramesCollected}/{requiredEnrollmentSamples}");
                }
            }

            return;
        }

        bool authenticated = authenticator.Authenticate(observation, features, out string matchedUserId, out float authScore);
        if (authenticated)
        {
            UpdateStatus($"Authenticated: {matchedUserId} ({authScore:F2})");
        }
        else
        {
            UpdateStatus($"Authentication failed ({authScore:F2})");
        }
    }

    private void HandleDebugInput()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            BeginEnrollment("User1");
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            BeginEnrollment("User2");
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            BeginEnrollment("User3");
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            CompleteEnrollment();
        }
#endif
    }

    private void BeginEnrollment(string userId)
    {
        activeEnrollmentId = userId;
        enrollmentActive = true;
        enrollmentFramesCollected = 0;
        UpdateStatus($"Begin enrollment: {userId}");
    }

    private void CompleteEnrollment()
    {
        if (!enrollmentActive)
        {
            UpdateStatus("No enrollment in progress.");
            return;
        }

        bool committed = enrollmentManager.CommitEnrollment(activeEnrollmentId);
        enrollmentActive = false;
        enrollmentFramesCollected = 0;
        UpdateStatus(committed ? $"Enrollment committed: {activeEnrollmentId}" : "Enrollment commit failed.");
    }

    private void UpdateStatus(string status)
    {
        if (statusText != null)
        {
            statusText.text = status;
        }
        Debug.Log(status);
    }

    private void OnDestroy()
    {
        if (landmarkProvider != null)
        {
            landmarkProvider.Dispose();
        }

        if (webcamTexture != null)
        {
            webcamTexture.Stop();
        }
    }
}
