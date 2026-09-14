using System;
using UnityEngine;

namespace DeepLearning.GameServer.Core
{
    [Serializable]
    public sealed class GameServerSettings
    {
        public const string DiscoveryRequest = "DEEP_LEARNING_SERVER_DISCOVERY_V1";
        public const string DiscoveryResponsePrefix = "DEEP_LEARNING_SERVER_V1:";

        [Tooltip("서버가 바인딩할 IPv4 주소입니다. 모든 네트워크 인터페이스를 사용하려면 0.0.0.0을 입력하세요.")]
        public string host = "0.0.0.0";

        [Min(1)]
        public int port = 5000;

        [Min(1)]
        [Tooltip("같은 네트워크의 클라이언트가 서버를 자동 검색할 때 사용하는 UDP 포트입니다.")]
        public int discoveryPort = 5001;

        [Min(1)]
        public int winScore = 5;

        [Min(1)]
        [Tooltip("서버에 동시에 접속할 수 있는 최대 플레이어 수입니다.")]
        public int maxPlayers = 4;

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
            discoveryPort = Mathf.Clamp(discoveryPort, 1, 65535);
            winScore = Mathf.Max(1, winScore);
            maxPlayers = Mathf.Max(1, maxPlayers);
            roundResultDelaySeconds = Mathf.Max(0f, roundResultDelaySeconds);
        }
    }
}
