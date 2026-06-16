using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    /// <summary>
    /// 单个视觉坐标的材质覆盖，仅供 Unity 表现层使用。
    /// </summary>
    [System.Serializable]
    public sealed class BoardCellMaterialOverride
    {
        public int gridX;
        public int gridY;

        public bool overrideTop = true;
        public Material topMaterial;
        public Color fallbackTopColor = Color.white;

        public bool overrideFrontSide;
        public Material frontSideMaterial;
        public Color fallbackFrontSideColor = new Color(0.45f, 0.35f, 0.18f, 1f);

        public bool overrideLeftSide;
        public Material leftSideMaterial;
        public Color fallbackLeftSideColor = new Color(0.38f, 0.3f, 0.16f, 1f);

        public bool overrideRightSide;
        public Material rightSideMaterial;
        public Color fallbackRightSideColor = new Color(0.5f, 0.4f, 0.22f, 1f);
    }
}
