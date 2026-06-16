using System;
using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    [Serializable]
    public sealed class BoardUnitVisualEntry
    {
        public string unitId;
        public GameObject prefab;
    }

    [CreateAssetMenu(
        fileName = "BoardUnitVisualCatalog",
        menuName = "Game/Board/Unit Visual Catalog"
    )]
    public sealed class BoardUnitVisualCatalog : ScriptableObject
    {
        [SerializeField] private BoardUnitVisualEntry[] pieceEntries;
        [SerializeField] private BoardUnitVisualEntry[] enemyEntries;

        public GameObject GetPiecePrefab(string pieceId)
        {
            return FindPrefab(pieceEntries, pieceId);
        }

        public GameObject GetEnemyPrefab(string enemyId)
        {
            return FindPrefab(enemyEntries, enemyId);
        }

        private static GameObject FindPrefab(
            BoardUnitVisualEntry[] entries,
            string unitId)
        {
            if (entries == null || string.IsNullOrEmpty(unitId))
            {
                return null;
            }

            foreach (BoardUnitVisualEntry entry in entries)
            {
                if (entry != null && entry.unitId == unitId)
                {
                    return entry.prefab;
                }
            }

            return null;
        }
    }
}
