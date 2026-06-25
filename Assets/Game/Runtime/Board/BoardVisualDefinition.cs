using UnityEngine;
using UnityEngine.Serialization;

namespace ProtectTree.Runtime.Board
{
    [CreateAssetMenu(
        fileName = "BoardVisualDefinition",
        menuName = "Game/Board/Board Visual Definition"
    )]
    public class BoardVisualDefinition : ScriptableObject
    {
        [Header("Top Materials")]
        [SerializeField] private Material groundTopMaterial;
        [FormerlySerializedAs("platformTopMaterial")]
        [SerializeField] private Material highGroundTopMaterial;
        [SerializeField] private Material obstacleTopMaterial;

        [Header("Side Materials")]
        [SerializeField] private Material frontSideMaterial;
        [SerializeField] private Material leftSideMaterial;
        [SerializeField] private Material rightSideMaterial;

        [Header("Highlight Materials")]
        [SerializeField] private Material selectedFillMaterial;
        [SerializeField] private Material selectedOutlineMaterial;
        [SerializeField] private Material attackRangeFillMaterial;
        [SerializeField] private Color attackRangeFillColor =
            new Color(1f, 0.28f, 0.05f, 0.3f);

        [Header("Unit Prototype Material")]
        [SerializeField] private Material unitPreviewMaterial;

        [Header("Fallback Top Colors")]
        [SerializeField] private Color groundTopColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        [FormerlySerializedAs("platformTopColor")]
        [SerializeField] private Color highGroundTopColor = new Color(0.95f, 0.8f, 0.25f, 1f);
        [SerializeField] private Color obstacleTopColor = new Color(0.9f, 0.25f, 0.25f, 1f);

        [Header("Fallback Side Colors")]
        [SerializeField] private Color frontSideColor = new Color(0.45f, 0.35f, 0.18f, 1f);
        [SerializeField] private Color leftSideColor = new Color(0.38f, 0.3f, 0.16f, 1f);
        [SerializeField] private Color rightSideColor = new Color(0.5f, 0.4f, 0.22f, 1f);

        [Header("Visual Key Materials")]
        [SerializeField] private BoardMaterialEntry[] materialEntries;

        [Header("Cell Visual Editor")]
        [SerializeField] private int visualGridWidth = 11;
        [SerializeField] private int visualGridHeight = 7;
        [HideInInspector]
        [SerializeField] private BoardCellMaterialOverride[] cellOverrides;

        public Material GroundTopMaterial => groundTopMaterial;
        public Material HighGroundTopMaterial => highGroundTopMaterial;
        public Material ObstacleTopMaterial => obstacleTopMaterial;

        public Material FrontSideMaterial => frontSideMaterial;
        public Material LeftSideMaterial => leftSideMaterial;
        public Material RightSideMaterial => rightSideMaterial;

        public Material SelectedFillMaterial => selectedFillMaterial;
        public Material SelectedOutlineMaterial => selectedOutlineMaterial;
        public Material AttackRangeFillMaterial => attackRangeFillMaterial;
        public Color AttackRangeFillColor => attackRangeFillColor;
        public Material UnitPreviewMaterial => unitPreviewMaterial;

        public Color GroundTopColor => groundTopColor;
        public Color HighGroundTopColor => highGroundTopColor;
        public Color ObstacleTopColor => obstacleTopColor;

        public Color FrontSideColor => frontSideColor;
        public Color LeftSideColor => leftSideColor;
        public Color RightSideColor => rightSideColor;

        public int VisualGridWidth => Mathf.Max(1, visualGridWidth);
        public int VisualGridHeight => Mathf.Max(1, visualGridHeight);

        public BoardMaterialEntry GetMaterialEntry(string visualKey)
        {
            if (materialEntries == null || string.IsNullOrEmpty(visualKey))
            {
                return null;
            }

            for (int i = 0; i < materialEntries.Length; i++)
            {
                BoardMaterialEntry entry = materialEntries[i];

                if (entry != null && entry.visualKey == visualKey)
                {
                    return entry;
                }
            }

            return null;
        }

        public BoardCellMaterialOverride GetCellOverride(int gridX, int gridY)
        {
            if (cellOverrides == null)
            {
                return null;
            }

            for (int i = 0; i < cellOverrides.Length; i++)
            {
                BoardCellMaterialOverride entry = cellOverrides[i];
                if (entry != null && entry.gridX == gridX && entry.gridY == gridY)
                {
                    return entry;
                }
            }

            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            visualGridWidth = Mathf.Max(1, visualGridWidth);
            visualGridHeight = Mathf.Max(1, visualGridHeight);
        }
#endif
    }
}
