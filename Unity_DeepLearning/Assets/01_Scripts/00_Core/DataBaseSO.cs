using System.Collections.Generic;
using UnityEngine;

namespace DeepLearning.GameData
{
    [CreateAssetMenu(fileName = "ItemDataBase", menuName = "Deep Learning/Item DataBase")]
    public sealed class DataBaseSO : ScriptableObject
    {
        [SerializeField] private List<ItemData> items = new List<ItemData>();

        public int Count => items?.Count ?? 0;
        public IReadOnlyList<ItemData> Items => items;

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

        private void OnValidate()
        {
            if (items == null)
            {
                items = new List<ItemData>();
            }
        }
    }
}
