using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace DeepLearning.GameClient
{
    public sealed class WebCamFeedMono : MonoBehaviour
    {
        [SerializeField] private RawImage display;
        [SerializeField] private AspectRatioFitter aspectRatioFitter;
        [SerializeField] private string preferredDeviceName = "";
        [SerializeField, Min(16)] private int requestedWidth = 1280;
        [SerializeField, Min(16)] private int requestedHeight = 720;
        [SerializeField, Min(1)] private int requestedFps = 30;
        [SerializeField] private bool mirrorHorizontally = true;
        [Tooltip("모바일에서 전면 카메라를 우선 선택합니다.")]
        [SerializeField] private bool preferFrontFacing = true;

        [Header("Camera Sharing")]
        [SerializeField] private GameClientMono gameClient;
        [SerializeField] private bool shareCamera = true;
        [SerializeField, Range(1f, 10f)] private float sharedFramesPerSecond = 3f;
        [SerializeField, Range(64, 640)] private int sharedWidth = 320;
        [SerializeField, Range(64, 480)] private int sharedHeight = 180;
        [SerializeField, Range(10, 90)] private int jpegQuality = 45;

        [Header("Multiple Instances")]
        [Tooltip("같은 PC에서 실행한 다른 게임 인스턴스가 동일한 웹캠을 사용 중이면 카메라 열기를 건너뜁니다.")]
        [SerializeField] private bool preventConcurrentDeviceAccess = true;
        [SerializeField] private bool startCameraOnStart = true;

        private WebCamTexture _texture;
        private Mutex _deviceMutex;
        private bool _ownsDeviceMutex;
        private Texture2D _sharedFrameTexture;
        private float _nextShareTime;
        private bool _sharingErrorLogged;
        private bool _restartAfterPause;
#if UNITY_ANDROID && !UNITY_EDITOR
        private bool _permissionRequestInProgress;
        private PermissionCallbacks _permissionCallbacks;
#endif

        public WebCamTexture Texture => _texture;
        public bool HasValidFrame => _texture != null && _texture.isPlaying && _texture.didUpdateThisFrame;
        public bool IsCameraRunning => _texture != null && _texture.isPlaying;
        public bool IsDeviceInUseByAnotherInstance { get; private set; }
        public string StatusMessage { get; private set; } = "카메라 대기";

        private void OnEnable()
        {
            if (gameClient == null)
            {
                gameClient = FindFirstObjectByType<GameClientMono>();
            }
        }

        private void Start()
        {
            if (startCameraOnStart)
            {
                StartCamera();
            }
        }

        private void Update()
        {
            if (_texture == null || _texture.width <= 16 || _texture.height <= 16)
            {
                return;
            }

            if (aspectRatioFitter != null)
            {
                bool rotated = Mathf.Abs(_texture.videoRotationAngle) % 180 != 0;
                aspectRatioFitter.aspectRatio = rotated
                    ? (float)_texture.height / _texture.width
                    : (float)_texture.width / _texture.height;
            }

            if (display != null)
            {
                display.rectTransform.localEulerAngles = new Vector3(0f, 0f, -_texture.videoRotationAngle);
                display.uvRect = mirrorHorizontally
                    ? new Rect(1f, _texture.videoVerticallyMirrored ? 1f : 0f, -1f, _texture.videoVerticallyMirrored ? -1f : 1f)
                    : new Rect(0f, _texture.videoVerticallyMirrored ? 1f : 0f, 1f, _texture.videoVerticallyMirrored ? -1f : 1f);
            }

            TryShareCurrentFrame();
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            _permissionRequestInProgress = false;
            ReleasePermissionCallbacks();
#endif
            StopCamera();

            if (_sharedFrameTexture != null)
            {
                Destroy(_sharedFrameTexture);
                _sharedFrameTexture = null;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                _restartAfterPause = IsCameraRunning;
                if (_restartAfterPause)
                {
                    _texture.Stop();
                }

                return;
            }

            if (_restartAfterPause)
            {
                _restartAfterPause = false;
                StartCamera();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus || !startCameraOnStart || _texture != null)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                return;
            }
#endif
            StartCamera();
        }

        private void OnValidate()
        {
            sharedFramesPerSecond = Mathf.Clamp(sharedFramesPerSecond, 1f, 10f);
            sharedWidth = Mathf.Clamp(sharedWidth, 64, 640);
            sharedHeight = Mathf.Clamp(sharedHeight, 64, 480);
            jpegQuality = Mathf.Clamp(jpegQuality, 10, 90);
        }

        [ContextMenu("Start Camera")]
        public void StartCamera()
        {
            if (_texture != null && _texture.isPlaying)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                RequestAndroidCameraPermission();
                return;
            }
#endif

            if (_texture != null)
            {
                _texture.Play();
                StatusMessage = $"웹캠 실행 중: {_texture.deviceName}";
                return;
            }

            string deviceName = SelectDeviceName();
            if (deviceName == null)
            {
                StatusMessage = "사용 가능한 웹캠을 찾지 못했습니다.";
                Debug.LogWarning(StatusMessage, this);
                return;
            }

            if (preventConcurrentDeviceAccess &&
                !Application.isMobilePlatform &&
                !TryAcquireDeviceMutex(deviceName))
            {
                IsDeviceInUseByAnotherInstance = true;
                StatusMessage = $"웹캠 '{deviceName}'은 다른 게임 인스턴스에서 사용 중입니다. 카메라 없이 실행합니다.";
                Debug.LogWarning(StatusMessage, this);
                return;
            }

            try
            {
                IsDeviceInUseByAnotherInstance = false;
                _texture = new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFps);
                if (display != null)
                {
                    display.texture = _texture;
                }

                _texture.Play();
                StatusMessage = $"웹캠 실행 중: {deviceName}";
            }
            catch (Exception exception)
            {
                StatusMessage = $"웹캠을 열 수 없습니다: {exception.Message}";
                Debug.LogWarning(StatusMessage, this);
                ReleaseDeviceMutex();
                _texture = null;
            }
        }

        [ContextMenu("Stop Camera")]
        public void StopCamera()
        {
            if (_texture != null && _texture.isPlaying)
            {
                _texture.Stop();
            }

            if (display != null && display.texture == _texture)
            {
                display.texture = null;
            }

            _texture = null;
            IsDeviceInUseByAnotherInstance = false;
            StatusMessage = "카메라 중지";
            ReleaseDeviceMutex();
        }

        private string SelectDeviceName()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(preferredDeviceName))
            {
                foreach (WebCamDevice device in devices)
                {
                    if (device.name == preferredDeviceName)
                    {
                        return device.name;
                    }
                }
            }

            if (Application.isMobilePlatform)
            {
                foreach (WebCamDevice device in devices)
                {
                    if (device.isFrontFacing == preferFrontFacing)
                    {
                        return device.name;
                    }
                }
            }

            return devices[0].name;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void RequestAndroidCameraPermission()
        {
            if (_permissionRequestInProgress)
            {
                return;
            }

            _permissionRequestInProgress = true;
            StatusMessage = "카메라 권한 요청 중";
            _permissionCallbacks = new PermissionCallbacks();
            _permissionCallbacks.PermissionGranted += HandleCameraPermissionGranted;
            _permissionCallbacks.PermissionDenied += HandleCameraPermissionDenied;
            Permission.RequestUserPermission(Permission.Camera, _permissionCallbacks);
        }

        private void HandleCameraPermissionGranted(string permissionName)
        {
            _permissionRequestInProgress = false;
            ReleasePermissionCallbacks();
            StatusMessage = "카메라 권한 승인";
            StartCamera();
        }

        private void HandleCameraPermissionDenied(string permissionName)
        {
            _permissionRequestInProgress = false;
            ReleasePermissionCallbacks();
            StatusMessage = Permission.ShouldShowRequestPermissionRationale(Permission.Camera)
                ? "카메라 권한이 거부되었습니다. 카메라 사용을 위해 권한을 허용해 주세요."
                : "카메라 권한을 요청할 수 없습니다. Android 설정에서 카메라 권한을 허용해 주세요.";
            Debug.LogWarning(StatusMessage, this);
        }

        private void ReleasePermissionCallbacks()
        {
            if (_permissionCallbacks == null)
            {
                return;
            }

            _permissionCallbacks.PermissionGranted -= HandleCameraPermissionGranted;
            _permissionCallbacks.PermissionDenied -= HandleCameraPermissionDenied;
            _permissionCallbacks = null;
        }
#endif

        private bool TryAcquireDeviceMutex(string deviceName)
        {
            ReleaseDeviceMutex();
            string mutexName = $"DeepLearningProject_WebCam_{CreateStableHash(deviceName):X8}";
            _deviceMutex = new Mutex(false, mutexName);

            try
            {
                _ownsDeviceMutex = _deviceMutex.WaitOne(0);
            }
            catch (AbandonedMutexException)
            {
                _ownsDeviceMutex = true;
            }

            if (_ownsDeviceMutex)
            {
                return true;
            }

            _deviceMutex.Dispose();
            _deviceMutex = null;
            return false;
        }

        private void ReleaseDeviceMutex()
        {
            if (_deviceMutex == null)
            {
                return;
            }

            if (_ownsDeviceMutex)
            {
                try
                {
                    _deviceMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // 이미 소유권이 해제된 경우입니다.
                }
            }

            _ownsDeviceMutex = false;
            _deviceMutex.Dispose();
            _deviceMutex = null;
        }

        private void TryShareCurrentFrame()
        {
            if (!shareCamera || gameClient == null || !gameClient.IsConnected ||
                !_texture.didUpdateThisFrame || Time.unscaledTime < _nextShareTime)
            {
                return;
            }

            _nextShareTime = Time.unscaledTime + (1f / sharedFramesPerSecond);

            RenderTexture temporary = null;
            RenderTexture previous = RenderTexture.active;

            try
            {
                temporary = RenderTexture.GetTemporary(
                    sharedWidth,
                    sharedHeight,
                    0,
                    RenderTextureFormat.ARGB32);
                Graphics.Blit(_texture, temporary);
                RenderTexture.active = temporary;

                if (_sharedFrameTexture == null ||
                    _sharedFrameTexture.width != sharedWidth ||
                    _sharedFrameTexture.height != sharedHeight)
                {
                    if (_sharedFrameTexture != null)
                    {
                        Destroy(_sharedFrameTexture);
                    }

                    _sharedFrameTexture = new Texture2D(
                        sharedWidth,
                        sharedHeight,
                        TextureFormat.RGB24,
                        false);
                }

                _sharedFrameTexture.ReadPixels(
                    new Rect(0f, 0f, sharedWidth, sharedHeight),
                    0,
                    0,
                    false);
                _sharedFrameTexture.Apply(false, false);

                byte[] jpegData = _sharedFrameTexture.EncodeToJPG(jpegQuality);
                gameClient.SendCameraFrame(
                    jpegData,
                    sharedWidth,
                    sharedHeight,
                    _texture.videoRotationAngle,
                    mirrorHorizontally,
                    _texture.videoVerticallyMirrored);
                _sharingErrorLogged = false;
            }
            catch (Exception exception)
            {
                if (!_sharingErrorLogged)
                {
                    Debug.LogWarning($"카메라 화면 공유 실패: {exception.Message}", this);
                    _sharingErrorLogged = true;
                }
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null)
                {
                    RenderTexture.ReleaseTemporary(temporary);
                }
            }
        }

        private static uint CreateStableHash(string value)
        {
            const uint offsetBasis = 2166136261;
            const uint prime = 16777619;
            uint hash = offsetBasis;

            foreach (char character in value)
            {
                hash ^= character;
                hash *= prime;
            }

            return hash;
        }
    }
}
