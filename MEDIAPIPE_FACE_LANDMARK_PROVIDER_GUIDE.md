# MediaPipeFaceLandmarkProvider 구현 가이드

이 문서는 현재 프로젝트의 `MediaPipeFaceLandmarkProvider`를 실제 얼굴 landmark 추출기로 구현하기 위해 해야 할 일을 정리한 문서입니다.

현재 프로젝트에서 `MediaPipeFaceLandmarkProvider`의 역할은 다음과 같습니다.

```text
Unity 카메라 프레임
-> MediaPipe Face Landmarker 실행
-> 얼굴 landmark 결과 수신
-> FaceObservation으로 변환
-> 기존 등록/인증 로직에 전달
```

즉, 이 클래스는 인증 판단을 직접 하지 않습니다. 얼굴에서 점을 추출해서 `FaceObservation` 형태로 넘겨주는 provider입니다.

## 1. 현재 코드 구조 이해

현재 얼굴 landmark provider 인터페이스는 다음 구조입니다.

```csharp
public interface IFaceLandmarkProvider
{
    bool TryGetObservation(out FaceObservation observation);
    void Initialize(WebCamTexture texture);
    void Dispose();
}
```

`FaceAuthenticationController`는 이 인터페이스만 바라봅니다.

- `useMockProvider == true`: `MockFaceLandmarkProvider` 사용
- `useMockProvider == false`: `MediaPipeFaceLandmarkProvider` 사용

따라서 실제 제품에서는 Unity Inspector에서 `Use Mock Provider`를 끄고, `MediaPipeFaceLandmarkProvider`가 실제 카메라 얼굴 landmark를 반환하게 만들어야 합니다.

## 2. 구현 목표

1차 목표는 명확합니다.

```text
MediaPipeFaceLandmarkProvider.TryGetObservation()이 실제 카메라 얼굴에서 478개 Vector3 landmark를 반환하게 만들기
```

이 목표가 완료되면 기존 `FeatureVectorExtractor`, `EnrollmentManager`, `FuzzyFaceAuthenticator`가 실제 얼굴 데이터로 동작할 수 있습니다.

## 3. 왜 478개 landmark인가

Google MediaPipe Face Landmarker의 Face Mesh 모델은 얼굴에 대한 3D landmark를 출력합니다. 공식 문서 기준 Face Mesh 모델은 478개의 3D face landmarks를 출력합니다.

현재 프로젝트의 `FeatureVectorExtractor`도 다음과 같은 MediaPipe Face Mesh 인덱스를 이미 사용합니다.

- 왼쪽 눈: `33`, `133`, `159`, `145`
- 오른쪽 눈: `263`, `362`, `386`, `374`
- 코: `1`, `98`, `327`
- 입: `61`, `291`
- 턱/얼굴 윤곽: `152`, `234`, `454`

따라서 실제 구현에서는 `FaceObservation(478)`을 생성하는 것이 맞습니다.

참고:

- Google AI Edge Face Landmarker 문서: https://ai.google.dev/edge/mediapipe/solutions/vision/face_landmarker
- MediaPipe Face Landmarker Android 샘플: https://github.com/google-ai-edge/mediapipe-samples/tree/main/examples/face_landmarker/android

## 4. 구현 방식 선택

Unity에서 MediaPipe를 쓰는 방식은 크게 두 가지입니다.

## 4.1 Unity용 MediaPipe 플러그인 사용

가장 빠르게 검증할 수 있는 방식입니다.

장점:

- Unity 안에서 C#으로 결과를 받을 수 있습니다.
- 현재 프로젝트 구조와 연결하기 쉽습니다.
- 빠르게 실제 landmark 추출 여부를 검증할 수 있습니다.

단점:

- 사용하는 플러그인의 Unity 버전, OS, 빌드 타겟 호환성을 확인해야 합니다.
- 플러그인별 API가 다르므로 예제 코드에 맞춰 provider를 수정해야 합니다.

초기 제품화 검증 단계에서는 이 방식을 권장합니다.

## 4.2 플랫폼별 네이티브 브릿지 구현

Android, iOS, Windows 등 배포 플랫폼별로 MediaPipe를 네이티브에서 실행하고 Unity로 결과를 넘기는 방식입니다.

예시:

- Android: 공식 Android Face Landmarker + Unity Android Plugin
- iOS: 공식 iOS Face Landmarker + Unity Native Plugin
- Windows/macOS: C++ native plugin 또는 Unity 호환 MediaPipe native plugin

장점:

- 실제 배포 플랫폼에 최적화할 수 있습니다.
- 공식 플랫폼 SDK를 직접 사용할 수 있습니다.

단점:

- 구현 난이도가 높습니다.
- 플랫폼마다 bridge 코드를 따로 관리해야 합니다.

제품의 1차 타겟이 Android 또는 iOS로 확정된 이후에 고려하는 것이 좋습니다.

## 5. 구현 순서

## 5.1 MediaPipe 실행 환경 준비

1. Unity 프로젝트 버전을 확정합니다.
2. 사용할 MediaPipe Unity 플러그인 또는 native bridge 방식을 선택합니다.
3. Face Landmarker 모델 파일을 프로젝트에 포함합니다.
4. 모델 파일이 빌드 결과물에 포함되는지 확인합니다.
5. Unity Editor와 실제 빌드 환경에서 플러그인이 로드되는지 확인합니다.

공식 MediaPipe Face Landmarker는 `.task` 모델 번들을 사용합니다. 사용하는 플러그인에 따라 `.task`, `.tflite`, graph config 파일 중 무엇이 필요한지 달라질 수 있으므로 해당 플러그인의 예제를 기준으로 맞춰야 합니다.

## 5.2 Initialize 구현

`Initialize(WebCamTexture texture)`에서는 카메라와 MediaPipe 엔진을 준비합니다.

해야 할 일:

1. 전달받은 `WebCamTexture`를 필드에 저장합니다.
2. MediaPipe Face Landmarker 인스턴스를 생성합니다.
3. 카메라 입력에 맞는 실행 모드를 설정합니다.
4. 최대 얼굴 수를 `1`로 설정합니다.
5. confidence threshold 값을 설정합니다.
6. 필요한 버퍼 또는 texture 변환 객체를 준비합니다.

의사 코드:

```csharp
public void Initialize(WebCamTexture texture)
{
    webcam = texture;

    // 1. 모델 파일 경로 준비
    // 2. FaceLandmarker 옵션 생성
    // 3. running mode를 camera/live stream 또는 video frame 처리용으로 설정
    // 4. FaceLandmarker 인스턴스 생성
    // 5. frame 변환용 버퍼 준비
}
```

MediaPipe 공식 문서에서는 카메라 같은 실시간 입력에는 `LIVE_STREAM` 모드를 사용한다고 설명합니다. 다만 Unity 플러그인에 따라 `VIDEO`처럼 timestamp가 포함된 프레임 처리 방식을 쓰는 경우도 있으므로, 선택한 플러그인의 샘플을 우선해야 합니다.

## 5.3 TryGetObservation 구현

`TryGetObservation`은 매 프레임 호출됩니다.

해야 할 일:

1. 카메라가 준비되었는지 확인합니다.
2. 현재 카메라 프레임을 가져옵니다.
3. MediaPipe 입력 이미지 형식으로 변환합니다.
4. Face Landmarker를 실행합니다.
5. 얼굴이 없으면 `false`를 반환합니다.
6. 첫 번째 얼굴의 landmark를 `FaceObservation`으로 변환합니다.
7. `true`를 반환합니다.

의사 코드:

```csharp
public bool TryGetObservation(out FaceObservation observation)
{
    observation = null;

    if (webcam == null || !webcam.isPlaying)
    {
        return false;
    }

    // 1. WebCamTexture에서 현재 프레임 가져오기
    // 2. MediaPipe 입력 이미지로 변환하기
    // 3. FaceLandmarker 실행하기
    // 4. 얼굴 결과가 없으면 false 반환하기
    // 5. 첫 번째 얼굴의 landmarks를 FaceObservation으로 변환하기

    return true;
}
```

## 5.4 landmark 좌표 변환

MediaPipe landmark는 일반적으로 정규화 좌표입니다.

```text
x: 0.0 ~ 1.0
y: 0.0 ~ 1.0
z: 얼굴 깊이 방향 상대값
```

현재 프로젝트의 `MockFaceLandmarkProvider`는 픽셀 좌표를 사용합니다. 일관성을 위해 MediaPipe 결과도 픽셀 좌표로 변환하는 것이 좋습니다.

예시:

```csharp
float x = landmark.X * webcam.width;
float y = (1f - landmark.Y) * webcam.height;
float z = landmark.Z * webcam.width;

observation.Landmarks[i] = new Vector3(x, y, z);
```

`y`를 뒤집는 이유는 이미지 좌표계와 Unity 화면 좌표계의 위아래 방향이 다를 수 있기 때문입니다. 실제 카메라 미리보기와 landmark overlay를 비교했을 때 상하가 뒤집히면 이 부분을 조정해야 합니다.

## 5.5 FaceObservation 생성

실제 MediaPipe Face Mesh 기준으로는 478개 landmark를 넣습니다.

```csharp
FaceObservation observation = new FaceObservation(478);
```

그리고 landmark를 채웁니다.

```csharp
for (int i = 0; i < 478; i++)
{
    observation.Landmarks[i] = convertedPoint;
    observation.Confidences[i] = faceConfidence;
}

observation.GlobalConfidence = faceConfidence;
```

주의할 점:

- MediaPipe가 landmark별 confidence를 제공하지 않는 경우가 있습니다.
- 이 경우 face presence confidence 또는 tracking confidence를 전체 landmark confidence로 넣어도 됩니다.
- 나중에 mask, glasses, occlusion 판단을 고도화하려면 부위별 confidence 또는 visibility 추정 로직을 추가해야 합니다.

## 5.6 Dispose 구현

`Dispose()`에서는 MediaPipe 리소스를 정리합니다.

해야 할 일:

1. Face Landmarker 인스턴스를 해제합니다.
2. frame 변환용 buffer를 해제합니다.
3. native resource를 해제합니다.
4. 중복 Dispose 호출에도 안전하게 처리합니다.

의사 코드:

```csharp
public void Dispose()
{
    // landmarker?.Close();
    // landmarker?.Dispose();
    // buffer cleanup
    landmarker = null;
}
```

## 6. FaceAuthenticationController 연결

구현이 끝나면 Unity Inspector에서 다음 값을 설정합니다.

```text
Use Mock Provider: false
```

그러면 `FaceAuthenticationController`에서 다음 코드 경로가 실행됩니다.

```csharp
landmarkProvider = new MediaPipeFaceLandmarkProvider();
```

이후 `ProcessFrame()`에서 실제 얼굴 landmark 기반으로 feature extraction과 인증이 진행됩니다.

## 7. 테스트 방법

## 7.1 1차 테스트

Unity Play Mode에서 다음을 확인합니다.

1. 카메라가 정상적으로 켜집니다.
2. `MediaPipeFaceLandmarkProvider.Initialize()`가 에러 없이 실행됩니다.
3. 얼굴이 카메라에 보일 때 `TryGetObservation()`이 `true`를 반환합니다.
4. `observation.LandmarkCount`가 `478`입니다.
5. 주요 landmark 좌표가 0이 아닙니다.
6. `FeatureVectorExtractor.Extract()` 결과가 정상적인 값을 가집니다.

## 7.2 등록 테스트

Unity Editor 실행 중:

```text
1 키: User1 등록 시작
2 키: User2 등록 시작
3 키: User3 등록 시작
E 키: 현재 등록 강제 완료
```

확인할 것:

1. 등록 진행률이 올라갑니다.
2. 등록 완료 후 `face_enrollment.json`이 생성됩니다.
3. 다시 실행해도 등록 정보가 로드됩니다.

## 7.3 인증 테스트

확인할 것:

1. 등록한 사람은 인증 성공률이 높아야 합니다.
2. 등록하지 않은 사람은 인증 실패해야 합니다.
3. 조명, 거리, 각도 변화에서 점수 변화를 확인합니다.
4. 마스크, 안경, 얼굴 가림 상태에서 실패 또는 경고가 발생하는지 확인합니다.

## 8. 디버깅 체크리스트

## 8.1 얼굴이 감지되지 않는 경우

확인할 것:

1. 카메라 권한이 허용되었는지 확인합니다.
2. `WebCamTexture.isPlaying`이 true인지 확인합니다.
3. 카메라 프레임이 MediaPipe 입력 형식으로 제대로 변환되는지 확인합니다.
4. 이미지 회전값이 잘못되지 않았는지 확인합니다.
5. RGB/BGRA 색상 포맷이 잘못되지 않았는지 확인합니다.
6. 모델 파일 경로가 올바른지 확인합니다.

## 8.2 landmark 위치가 뒤집힌 경우

확인할 것:

1. `x` 좌표가 좌우 반전되어야 하는지 확인합니다.
2. `y` 좌표가 상하 반전되어야 하는지 확인합니다.
3. 카메라 미리보기가 mirror 처리되어 있는지 확인합니다.
4. front camera와 rear camera의 회전/반전 차이를 확인합니다.

## 8.3 인증 점수가 불안정한 경우

확인할 것:

1. landmark jitter가 심한지 확인합니다.
2. `num_faces`가 1인지 확인합니다.
3. tracking confidence threshold를 조정합니다.
4. registration sample 수를 늘립니다.
5. 얼굴이 너무 작게 잡히지 않는지 확인합니다.
6. `FeatureVectorExtractor`에서 사용하는 landmark index가 정상 좌표를 갖는지 확인합니다.

## 9. 구현 완료 기준

`MediaPipeFaceLandmarkProvider` 구현 완료 기준은 다음과 같습니다.

1. Mock Provider 없이 실제 카메라 얼굴에서 landmark를 얻습니다.
2. `FaceObservation(478)`이 정상 생성됩니다.
3. `FeatureVectorExtractor`가 실제 얼굴에서 feature vector를 계산합니다.
4. User1 등록이 가능합니다.
5. 앱 재실행 후 등록 정보가 유지됩니다.
6. 등록한 사용자는 인증 성공합니다.
7. 다른 사용자는 인증 실패합니다.
8. 얼굴 없음, 카메라 없음, 모델 로드 실패 상황에서 앱이 멈추지 않습니다.

## 10. 추천 다음 작업

바로 다음 순서로 진행하는 것을 권장합니다.

1. Unity용 MediaPipe 실행 방식을 선택합니다.
2. Face Landmarker 샘플을 Unity에서 먼저 독립 실행합니다.
3. 샘플에서 landmark 좌표를 가져오는 코드를 확인합니다.
4. 해당 결과를 `FaceObservation`으로 변환하는 adapter를 작성합니다.
5. `MediaPipeFaceLandmarkProvider`에 adapter를 연결합니다.
6. `useMockProvider`를 false로 바꾸고 실제 인증 흐름을 테스트합니다.

