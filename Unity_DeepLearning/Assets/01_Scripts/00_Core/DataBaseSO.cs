using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;

namespace DeepLearning.GameData
{
    [CreateAssetMenu(fileName = "ItemDataBase", menuName = "Deep Learning/Item DataBase")]
    public sealed class DataBaseSO : ScriptableObject
    {
        [Header("AI Model")]
        [Tooltip("아이템 전용 모델이 없을 때 사용하는 기본 ONNX 모델입니다.")]
        [SerializeField] private ModelAsset commonAIModel;

        [Header("Items")]
        [SerializeField] private List<ItemData> items = new List<ItemData>();

        public int Count => items?.Count ?? 0;
        public IReadOnlyList<ItemData> Items => items;
        public ModelAsset CommonAIModel => commonAIModel;

        public ItemData GetItem(int index)
        {
            if (!TryGetItem(index, out ItemData item))
            {
                throw new System.ArgumentOutOfRangeException(nameof(index));
            }

            return item;
        }

        public bool TryGetItem(int index, out ItemData item)
        {
            if (items != null && index >= 0 && index < items.Count && items[index] != null)
            {
                item = items[index];
                return true;
            }

            item = null;
            return false;
        }

        public ModelAsset GetAIModel(int itemIndex)
        {
            if (TryGetItem(itemIndex, out ItemData item) && item.AIModel != null)
            {
                return item.AIModel;
            }

            return commonAIModel;
        }

        public bool TryGetAIModel(int itemIndex, out ModelAsset model)
        {
            model = GetAIModel(itemIndex);
            return model != null;
        }

        private void OnValidate()
        {
            if (items == null)
            {
                items = new List<ItemData>();
            }
        }
    }
}
