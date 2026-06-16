using System.Collections.Generic;
using UnityEngine;

namespace ProtectTree.Runtime.UI
{
    internal static class UIResourceLoader
    {
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> MissingPaths = new HashSet<string>();

        public static Sprite LoadSprite(string resourcePath)
        {
            if (Sprites.TryGetValue(resourcePath, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
            {
                Sprites.Add(resourcePath, sprite);
                return sprite;
            }

            if (MissingPaths.Add(resourcePath))
            {
                Debug.LogWarning($"UI sprite was not found in Resources: {resourcePath}");
            }

            return null;
        }
    }
}
