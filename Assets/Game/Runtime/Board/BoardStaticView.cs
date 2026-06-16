using System.Collections.Generic;
using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    /// <summary>
    /// 只负责静态棋盘地形表现。切换观察玩家时不应重建或清空此视图。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoardStaticView : MonoBehaviour
    {
        private enum SideDirection
        {
            Front,
            Left,
            Right
        }

        private readonly List<Mesh> _runtimeMeshes = new List<Mesh>();
        private readonly Dictionary<string, Material> _fallbackMaterials =
            new Dictionary<string, Material>();

        private Transform _meshRoot;
        private BoardVisualDefinition _visualDefinition;
        private BoardVisualLayout _layout;
        private BoardPerspectiveProjector _projector;
        private BoardSortingPolicy _sorting;
        private float _frontEdgeExtraDepth;

        public BoardVisualLayout Layout => _layout;
        public BoardPerspectiveProjector Projector => _projector;
        public BoardSortingPolicy Sorting => _sorting;

        public void Build(
            Transform meshRoot,
            BoardVisualDefinition visualDefinition,
            BoardVisualLayout layout,
            BoardPerspectiveProjector projector,
            BoardSortingPolicy sorting,
            float frontEdgeExtraDepth = 0f)
        {
            _meshRoot = meshRoot;
            _visualDefinition = visualDefinition;
            _layout = layout;
            _projector = projector;
            _sorting = sorting;
            _frontEdgeExtraDepth = Mathf.Max(0f, frontEdgeExtraDepth);

            Clear();

            if (_meshRoot == null || _layout == null || _projector == null || _sorting == null)
            {
                Debug.LogError("BoardStaticView requires mesh root, layout, projector, and sorting.");
                return;
            }

            foreach (BoardVisualCell cell in _layout.Cells)
            {
                CreateTop(cell);
            }

            foreach (BoardVisualCell cell in _layout.Cells)
            {
                CreateVisibleSides(cell);
            }
        }

        public void Clear()
        {
            if (_meshRoot != null)
            {
                for (int index = _meshRoot.childCount - 1; index >= 0; index--)
                {
                    DestroyRuntimeObject(_meshRoot.GetChild(index).gameObject);
                }
            }

            foreach (Mesh mesh in _runtimeMeshes)
            {
                DestroyRuntimeObject(mesh);
            }

            _runtimeMeshes.Clear();

            foreach (Material material in _fallbackMaterials.Values)
            {
                DestroyRuntimeObject(material);
            }

            _fallbackMaterials.Clear();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void CreateTop(BoardVisualCell cell)
        {
            _projector.GetCellTopCorners(
                cell,
                out Vector3 frontLeft,
                out Vector3 frontRight,
                out Vector3 backRight,
                out Vector3 backLeft);

            CreateQuad(
                $"CellTop_{cell.CellId}_{cell.X}_{cell.Y}",
                frontLeft,
                frontRight,
                backRight,
                backLeft,
                ResolveTopMaterial(cell),
                _sorting.GetTopOrder(cell));
        }

        private void CreateVisibleSides(BoardVisualCell cell)
        {
            bool hasFrontEdgeSkirt =
                cell.Y == 0 && _frontEdgeExtraDepth > 0f;
            if (cell.Height <= 0 && !hasFrontEdgeSkirt)
            {
                return;
            }

            float frontHeight = _layout.GetHeightOrZero(cell.X, cell.Y - 1);
            int leftHeight = _layout.GetHeightOrZero(cell.X - 1, cell.Y);
            int rightHeight = _layout.GetHeightOrZero(cell.X + 1, cell.Y);

            if (hasFrontEdgeSkirt)
            {
                // 只把最前排正面向下延伸，顶面高度与玩法坐标保持不变。
                frontHeight -= _frontEdgeExtraDepth;
            }

            if (frontHeight < cell.Height)
            {
                CreateSide(cell, SideDirection.Front, frontHeight);
            }

            float boardCenterX = _layout.Width * 0.5f;
            float cellCenterX = cell.X + 0.5f;

            if (cellCenterX > boardCenterX && leftHeight < cell.Height)
            {
                CreateSide(cell, SideDirection.Left, leftHeight);
            }

            if (cellCenterX < boardCenterX && rightHeight < cell.Height)
            {
                CreateSide(cell, SideDirection.Right, rightHeight);
            }
        }

        private void CreateSide(
            BoardVisualCell cell,
            SideDirection direction,
            float lowerHeight)
        {
            GetSideCorners(
                cell,
                direction,
                lowerHeight,
                out Vector3 topA,
                out Vector3 topB,
                out Vector3 bottomB,
                out Vector3 bottomA);

            CreateQuad(
                $"CellSide_{direction}_{cell.CellId}_{cell.X}_{cell.Y}",
                topA,
                topB,
                bottomB,
                bottomA,
                ResolveSideMaterial(cell, direction),
                _sorting.GetSideOrder(cell, direction == SideDirection.Front));
        }

        private void CreateQuad(
            string objectName,
            Vector3 worldA,
            Vector3 worldB,
            Vector3 worldC,
            Vector3 worldD,
            Material material,
            int sortingOrder)
        {
            GameObject target = new GameObject(objectName);
            target.transform.SetParent(_meshRoot, false);

            MeshFilter filter = target.AddComponent<MeshFilter>();
            MeshRenderer renderer = target.AddComponent<MeshRenderer>();

            Mesh mesh = new Mesh
            {
                name = $"Mesh_{objectName}",
                vertices = new[]
                {
                    _meshRoot.InverseTransformPoint(worldA),
                    _meshRoot.InverseTransformPoint(worldB),
                    _meshRoot.InverseTransformPoint(worldC),
                    _meshRoot.InverseTransformPoint(worldD)
                },
                triangles = new[] { 0, 2, 1, 0, 3, 2 },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                }
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _runtimeMeshes.Add(mesh);

            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
        }

        private void GetSideCorners(
            BoardVisualCell cell,
            SideDirection direction,
            float lowerHeight,
            out Vector3 topA,
            out Vector3 topB,
            out Vector3 bottomB,
            out Vector3 bottomA)
        {
            int x = cell.X;
            int y = cell.Y;
            int height = cell.Height;

            switch (direction)
            {
                case SideDirection.Front:
                    topA = _projector.ProjectGridPoint(x, y, height);
                    topB = _projector.ProjectGridPoint(x + 1, y, height);
                    bottomB = _projector.ProjectGridPoint(x + 1, y, lowerHeight);
                    bottomA = _projector.ProjectGridPoint(x, y, lowerHeight);
                    break;

                case SideDirection.Left:
                    topA = _projector.ProjectGridPoint(x, y + 1, height);
                    topB = _projector.ProjectGridPoint(x, y, height);
                    bottomB = _projector.ProjectGridPoint(x, y, lowerHeight);
                    bottomA = _projector.ProjectGridPoint(x, y + 1, lowerHeight);
                    break;

                default:
                    topA = _projector.ProjectGridPoint(x + 1, y, height);
                    topB = _projector.ProjectGridPoint(x + 1, y + 1, height);
                    bottomB = _projector.ProjectGridPoint(x + 1, y + 1, lowerHeight);
                    bottomA = _projector.ProjectGridPoint(x + 1, y, lowerHeight);
                    break;
            }
        }

        private Material ResolveTopMaterial(BoardVisualCell cell)
        {
            BoardCellMaterialOverride cellOverride =
                _visualDefinition?.GetCellOverride(cell.X, cell.Y);
            if (cellOverride != null && cellOverride.overrideTop)
            {
                return cellOverride.topMaterial != null
                    ? cellOverride.topMaterial
                    : GetFallbackMaterial(
                        $"cell-top:{cell.X},{cell.Y}:{cellOverride.fallbackTopColor}",
                        cellOverride.fallbackTopColor);
            }

            BoardMaterialEntry entry = _visualDefinition?.GetMaterialEntry(cell.VisualKey);
            if (entry != null)
            {
                return entry.topMaterial != null
                    ? entry.topMaterial
                    : GetFallbackMaterial(
                        $"visual-top:{cell.VisualKey}:{entry.fallbackTopColor}",
                        entry.fallbackTopColor);
            }

            Material configured = null;
            Color fallback = Color.magenta;

            if (_visualDefinition != null)
            {
                switch (cell.Terrain)
                {
                    case BoardTerrainType.Ground:
                        configured = _visualDefinition.GroundTopMaterial;
                        fallback = _visualDefinition.GroundTopColor;
                        break;
                    case BoardTerrainType.HighGround:
                        configured = _visualDefinition.HighGroundTopMaterial;
                        fallback = _visualDefinition.HighGroundTopColor;
                        break;
                    case BoardTerrainType.Obstacle:
                        configured = _visualDefinition.ObstacleTopMaterial;
                        fallback = _visualDefinition.ObstacleTopColor;
                        break;
                }
            }

            return configured != null
                ? configured
                : GetFallbackMaterial($"top:{cell.Terrain}:{fallback}", fallback);
        }

        private Material ResolveSideMaterial(
            BoardVisualCell cell,
            SideDirection direction)
        {
            BoardCellMaterialOverride cellOverride =
                _visualDefinition?.GetCellOverride(cell.X, cell.Y);
            if (TryResolveCellSideOverride(
                cellOverride,
                direction,
                out Material cellMaterial,
                out Color cellFallback))
            {
                return cellMaterial != null
                    ? cellMaterial
                    : GetFallbackMaterial(
                        $"cell-side:{cell.X},{cell.Y}:{direction}:{cellFallback}",
                        cellFallback);
            }

            BoardMaterialEntry entry = _visualDefinition?.GetMaterialEntry(cell.VisualKey);
            Material configured = null;
            Color fallback = GetDefaultSideColor(direction);

            if (entry != null)
            {
                switch (direction)
                {
                    case SideDirection.Front:
                        configured = entry.frontSideMaterial;
                        fallback = entry.fallbackFrontSideColor;
                        break;
                    case SideDirection.Left:
                        configured = entry.leftSideMaterial;
                        fallback = entry.fallbackLeftSideColor;
                        break;
                    case SideDirection.Right:
                        configured = entry.rightSideMaterial;
                        fallback = entry.fallbackRightSideColor;
                        break;
                }
            }
            else if (_visualDefinition != null)
            {
                switch (direction)
                {
                    case SideDirection.Front:
                        configured = _visualDefinition.FrontSideMaterial;
                        fallback = _visualDefinition.FrontSideColor;
                        break;
                    case SideDirection.Left:
                        configured = _visualDefinition.LeftSideMaterial;
                        fallback = _visualDefinition.LeftSideColor;
                        break;
                    case SideDirection.Right:
                        configured = _visualDefinition.RightSideMaterial;
                        fallback = _visualDefinition.RightSideColor;
                        break;
                }
            }

            return configured != null
                ? configured
                : GetFallbackMaterial($"side:{direction}:{fallback}", fallback);
        }

        private static bool TryResolveCellSideOverride(
            BoardCellMaterialOverride cellOverride,
            SideDirection direction,
            out Material material,
            out Color fallback)
        {
            material = null;
            fallback = Color.white;
            if (cellOverride == null)
            {
                return false;
            }

            switch (direction)
            {
                case SideDirection.Front:
                    material = cellOverride.frontSideMaterial;
                    fallback = cellOverride.fallbackFrontSideColor;
                    return cellOverride.overrideFrontSide;
                case SideDirection.Left:
                    material = cellOverride.leftSideMaterial;
                    fallback = cellOverride.fallbackLeftSideColor;
                    return cellOverride.overrideLeftSide;
                default:
                    material = cellOverride.rightSideMaterial;
                    fallback = cellOverride.fallbackRightSideColor;
                    return cellOverride.overrideRightSide;
            }
        }

        private Material GetFallbackMaterial(string key, Color color)
        {
            if (_fallbackMaterials.TryGetValue(key, out Material material))
            {
                return material;
            }

            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");
            material = new Material(shader)
            {
                name = $"Runtime_BoardMaterial_{key}",
                color = color
            };
            _fallbackMaterials.Add(key, material);
            return material;
        }

        private static Color GetDefaultSideColor(SideDirection direction)
        {
            switch (direction)
            {
                case SideDirection.Front:
                    return new Color(0.45f, 0.35f, 0.18f, 1f);
                case SideDirection.Left:
                    return new Color(0.38f, 0.3f, 0.16f, 1f);
                default:
                    return new Color(0.5f, 0.4f, 0.22f, 1f);
            }
        }

        private static void DestroyRuntimeObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
