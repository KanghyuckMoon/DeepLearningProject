using System;
using UnityEngine;

namespace DeepLearning.GameServer.Core
{
    [Serializable]
    public sealed class GameServerSettings
    {
        [Tooltip("서버가 바인딩할 IPv4 주소입니다. 모든 네트워크 인터페이스를 사용하려면 0.0.0.0을 입력하세요.")]
        public string host = "10.10.59.205";

        [Min(1)]
        public int port = 5000;

        [Min(1)]
        public int winScore = 5;

        [Min(0f)]
        [Tooltip("라운드 결과를 표시한 뒤 다음 라운드를 시작하기까지의 시간입니다.")]
        public float roundResultDelaySeconds = 1f;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                host = "0.0.0.0";
            }

            port = Mathf.Clamp(port, 1, 65535);
            winScore = Mathf.Max(1, winScore);
            roundResultDelaySeconds = Mathf.Max(0f, roundResultDelaySeconds);
        }
    }
}
