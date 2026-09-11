using UnityEngine;
using UnityEngine.UI;

namespace DeepLearning.GameClient
{
    public sealed class WebCamFeed : MonoBehaviour
    {
        [SerializeField] private RawImage display;
        [SerializeField] private AspectRatioFitter aspectRatioFitter;
        [SerializeField] private string preferredDeviceName = "";
        [SerializeField, Min(16)] private int requestedWidth = 1280;
        [SerializeField, Min(16)] private int requestedHeight = 720;
        [SerializeField, Min(1)] private int requestedFps = 30;
        [SerializeField] private bool mirrorHorizontally = true;

        private WebCamTexture _texture;

        public WebCamTexture Texture => _texture;
        public bool HasValidFrame => _texture != null && _texture.isPlaying && _texture.didUpdateThisFrame;

        private void Start()
        {
            StartCamera();
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
                Debug.LogError("사용 가능한 웹캠을 찾지 못했습니다.", this);
                return;
            }

            _texture = new WebCamTexture(deviceName, requestedWidth, requestedHeight, requestedFps);
            if (display != null)
            {
                display.texture = _texture;
            }

            _texture.Play();
        }

        [ContextMenu("Stop Camera")]
        public void StopCamera()
        {
            if (_texture != null && _texture.isPlaying)
            {
                _texture.Stop();
            }
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
    }
}
