using System;
using UnityEngine;

namespace DeepLearning.GameData
{
    [Serializable]
    public sealed class ItemData
    {
        [SerializeField] private Sprite sprite;
        [SerializeField] private string itemName;

        public Sprite Sprite => sprite;
        public string Name => itemName;
    }
}
