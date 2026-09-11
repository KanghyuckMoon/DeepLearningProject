using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("Multiple Instances")]
        [Tooltip("같은 PC에서 실행한 다른 게임 인스턴스가 동일한 웹캠을 사용 중이면 카메라 열기를 건너뜁니다.")]
        [SerializeField] private bool preventConcurrentDeviceAccess = true;
        [SerializeField] private bool startCameraOnStart = true;

        private WebCamTexture _texture;
        private Mutex _deviceMutex;
        private bool _ownsDeviceMutex;

        public WebCamTexture Texture => _texture;
        public bool HasValidFrame => _texture != null && _texture.isPlaying && _texture.didUpdateThisFrame;
        public bool IsCameraRunning => _texture != null && _texture.isPlaying;
        public bool IsDeviceInUseByAnotherInstance { get; private set; }
        public string StatusMessage { get; private set; } = "카메라 대기";

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
                aspectRatioFitter.aspectRatio = (float)_texture.width / _texture.height;
            }

            if (display != null)
            {
                display.rectTransform.localEulerAngles = new Vector3(0f, 0f, -_texture.videoRotationAngle);
                display.uvRect = mirrorHorizontally
                    ? new Rect(1f, _texture.videoVerticallyMirrored ? 1f : 0f, -1f, _texture.videoVerticallyMirrored ? -1f : 1f)
                    : new Rect(0f, _texture.videoVerticallyMirrored ? 1f : 0f, 1f, _texture.videoVerticallyMirrored ? -1f : 1f);
            }
        }

        private void OnDestroy()
        {
            StopCamera();
        }

        [ContextMenu("Start Camera")]
        public void StartCamera()
        {
            if (_texture != null && _texture.isPlaying)
            {
                return;
            }

            string deviceName = SelectDeviceName();
            if (deviceName == null)
            {
                StatusMessage = "사용 가능한 웹캠을 찾지 못했습니다.";
                Debug.LogWarning(StatusMessage, this);
                return;
            }

            if (preventConcurrentDeviceAccess && !TryAcquireDeviceMutex(deviceName))
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

            return devices[0].name;
        }

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
