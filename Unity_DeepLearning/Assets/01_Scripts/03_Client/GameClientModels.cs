using System;
using System.Collections.Generic;

namespace DeepLearning.GameClient
{
    public enum GameClientEventType
    {
        Connected,
        Disconnected,
        RoundStarted,
        RoundResult,
        GameOver,
        PlayerJoined,
        PlayerLeft,
        PlayerUpdated,
        CameraFrame,
        StateChanged,
        Error
    }

    public sealed class RemoteCameraFrame
    {
        public RemoteCameraFrame(
            int playerId,
            byte[] jpegData,
            int width,
            int height,
            int rotation,
            bool mirrorHorizontally,
            bool flipVertically)
        {
            PlayerId = playerId;
            JpegData = jpegData;
            Width = width;
            Height = height;
            Rotation = rotation;
            MirrorHorizontally = mirrorHorizontally;
            FlipVertically = flipVertically;
        }

        public int PlayerId { get; }
        public byte[] JpegData { get; }
        public int Width { get; }
        public int Height { get; }
        public int Rotation { get; }
        public bool MirrorHorizontally { get; }
        public bool FlipVertically { get; }
    }

    public sealed class GameClientSnapshot
    {
        public GameClientSnapshot(
            int playerId,
            int round,
            IReadOnlyDictionary<int, int> scores,
            IReadOnlyDictionary<int, string> nicknames,
            int answerIndex,
            int roundWinner,
            int gameWinner,
            bool connected,
            bool detectionSent)
        {
            PlayerId = playerId;
            Round = round;
            Scores = scores;
            Nicknames = nicknames;
            AnswerIndex = answerIndex;
            RoundWinner = roundWinner;
            GameWinner = gameWinner;
            Connected = connected;
            DetectionSent = detectionSent;
        }

        public int PlayerId { get; }
        public int Round { get; }
        public IReadOnlyDictionary<int, int> Scores { get; }
        public IReadOnlyDictionary<int, string> Nicknames { get; }
        public int AnswerIndex { get; }
        public int RoundWinner { get; }
        public int GameWinner { get; }
        public bool Connected { get; }
        public bool DetectionSent { get; }
    }

    public sealed class GameClientEvent
    {
        public GameClientEvent(
            GameClientEventType type,
            GameClientSnapshot snapshot,
            int subjectPlayerId = -1,
            string message = null,
            RemoteCameraFrame cameraFrame = null)
        {
            Type = type;
            Snapshot = snapshot;
            SubjectPlayerId = subjectPlayerId;
            Message = message;
            CameraFrame = cameraFrame;
        }

        public GameClientEventType Type { get; }
        public GameClientSnapshot Snapshot { get; }
        public int SubjectPlayerId { get; }
        public string Message { get; }
        public RemoteCameraFrame CameraFrame { get; }
    }

    internal sealed class ServerMessage
    {
        public string Type;
        public int PlayerId = -1;
        public int Round = -1;
        public int Winner = -1;
        public int AnswerIndex = -1;
        public int MaxPlayers = -1;
        public string Nickname;
        public Dictionary<int, string> Nicknames;
        public string ImageBase64;
        public int ImageWidth = -1;
        public int ImageHeight = -1;
        public int Rotation;
        public bool MirrorHorizontally;
        public bool FlipVertically;
        public bool GameFinished;
        public Dictionary<int, int> Scores;
    }
}
