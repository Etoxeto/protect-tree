using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    [System.Serializable]
    public sealed class BoardMaterialEntry
    {
        public string visualKey;

        public Material topMaterial;
        public Material frontSideMaterial;
        public Material leftSideMaterial;
        public Material rightSideMaterial;

        public Color fallbackTopColor = Color.white;
        public Color fallbackFrontSideColor = new Color(0.45f, 0.35f, 0.18f, 1f);
        public Color fallbackLeftSideColor = new Color(0.38f, 0.3f, 0.16f, 1f);
        public Color fallbackRightSideColor = new Color(0.5f, 0.4f, 0.22f, 1f);
    }
}
