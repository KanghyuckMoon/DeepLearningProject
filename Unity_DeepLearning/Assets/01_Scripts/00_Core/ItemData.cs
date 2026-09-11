using System;
using UnityEngine;
using Unity.InferenceEngine;

namespace DeepLearning.GameData
{
    [Serializable]
    public sealed class ItemData
    {
        [SerializeField] private Sprite sprite;
        [SerializeField] private string itemName;
        [Tooltip("이 아이템 전용 모델입니다. 비워 두면 DataBaseSO의 공용 모델을 사용합니다.")]
        [SerializeField] private ModelAsset aiModel;

        public Sprite Sprite => sprite;
        public string Name => itemName;
        public ModelAsset AIModel => aiModel;
    }
}
