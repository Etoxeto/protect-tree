using System.Collections.Generic;
using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    [DisallowMultipleComponent]
    public sealed class BoardCellHighlightView : MonoBehaviour
    {
        private Transform _highlightRoot;
        private BoardPerspectiveProjector _projector;
        private BoardSortingPolicy _sorting;
        private Mesh _fillMesh;
        private MeshRenderer _fillRenderer;
        private LineRenderer _outlineRenderer;
        private Material _runtimeFillMaterial;
        private Material _runtimeOutlineMaterial;
        private Material _runtimePlacementMaterial;
        private Material _placementMaterial;
        private readonly List<GameObject> _placementHighlights =
            new List<GameObject>();
        private readonly List<Mesh> _placementMeshes = new List<Mesh>();

        public void Initialize(
            Transform highlightRoot,
            BoardVisualDefinition visualDefinition,
            BoardPerspectiveProjector projector,
            BoardSortingPolicy sorting,
            float outlineWidth)
        {
            Clear();

            _highlightRoot = highlightRoot;
            _projector = projector;
            _sorting = sorting;

            GameObject fill = new GameObject("SelectedCell_Fill");
            fill.transform.SetParent(_highlightRoot, false);
            MeshFilter filter = fill.AddComponent<MeshFilter>();
            _fillRenderer = fill.AddComponent<MeshRenderer>();
            _fillMesh = new Mesh { name = "Mesh_SelectedCell_Fill" };
            filter.sharedMesh = _fillMesh;
            _fillRenderer.sharedMaterial = visualDefinition != null
                && visualDefinition.SelectedFillMaterial != null
                    ? visualDefinition.SelectedFillMaterial
                    : CreateRuntimeMaterial(new Color(0f, 0.9f, 1f, 0.28f), out _runtimeFillMaterial);

            GameObject outline = new GameObject("SelectedCell_Outline");
            outline.transform.SetParent(_highlightRoot, false);
            _outlineRenderer = outline.AddComponent<LineRenderer>();
            _outlineRenderer.useWorldSpace = false;
            _outlineRenderer.loop = true;
            _outlineRenderer.positionCount = 4;
            _outlineRenderer.startWidth = outlineWidth;
            _outlineRenderer.endWidth = outlineWidth;
            _outlineRenderer.numCornerVertices = 2;
            _outlineRenderer.numCapVertices = 2;
            _outlineRenderer.sharedMaterial = visualDefinition != null
                && visualDefinition.SelectedOutlineMaterial != null
                    ? visualDefinition.SelectedOutlineMaterial
                    : CreateRuntimeMaterial(Color.cyan, out _runtimeOutlineMaterial);
            _placementMaterial =
                visualDefinition != null && visualDefinition.SelectedFillMaterial != null
                    ? visualDefinition.SelectedFillMaterial
                    : CreateRuntimeMaterial(
                        new Color(0.2f, 1f, 0.65f, 0.18f),
                        out _runtimePlacementMaterial);

            SetVisible(false);
        }

        public void Clear()
        {
            SetVisible(false);
            ClearPlacementCells();

            if (_highlightRoot != null)
            {
                for (int index = _highlightRoot.childCount - 1; index >= 0; index--)
                {
                    DestroyRuntimeObject(_highlightRoot.GetChild(index).gameObject);
                }
            }

            DestroyRuntimeObject(_fillMesh);
            DestroyRuntimeObject(_runtimeFillMaterial);
            DestroyRuntimeObject(_runtimeOutlineMaterial);
            DestroyRuntimeObject(_runtimePlacementMaterial);

            _fillMesh = null;
            _fillRenderer = null;
            _outlineRenderer = null;
            _runtimeFillMaterial = null;
            _runtimeOutlineMaterial = null;
            _runtimePlacementMaterial = null;
            _placementMaterial = null;
            _projector = null;
            _sorting = null;
        }

        public void Show(BoardVisualCell cell)
        {
            if (cell == null || _highlightRoot == null || _projector == null)
            {
                SetVisible(false);
                return;
            }

            _projector.GetCellTopCorners(
                cell,
                out Vector3 frontLeft,
                out Vector3 frontRight,
                out Vector3 backRight,
                out Vector3 backLeft);

            Vector3[] vertices =
            {
                _highlightRoot.InverseTransformPoint(frontLeft),
                _highlightRoot.InverseTransformPoint(frontRight),
                _highlightRoot.InverseTransformPoint(backRight),
                _highlightRoot.InverseTransformPoint(backLeft)
            };

            _fillMesh.Clear();
            _fillMesh.vertices = vertices;
            _fillMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            _fillMesh.RecalculateBounds();

            _outlineRenderer.SetPositions(vertices);

            int order = _sorting.GetHighlightOrder(cell);
            _fillRenderer.sortingOrder = order;
            _outlineRenderer.sortingOrder = order + 1;
            SetVisible(true);
        }

        public void ShowPlacementCells(IEnumerable<BoardVisualCell> cells)
        {
            ClearPlacementCells();

            if (cells == null
                || _highlightRoot == null
                || _projector == null
                || _sorting == null)
            {
                return;
            }

            foreach (BoardVisualCell cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }

                CreatePlacementHighlight(cell);
            }
        }

        public void ClearPlacementCells()
        {
            foreach (GameObject highlight in _placementHighlights)
            {
                DestroyRuntimeObject(highlight);
            }

            foreach (Mesh mesh in _placementMeshes)
            {
                DestroyRuntimeObject(mesh);
            }

            _placementHighlights.Clear();
            _placementMeshes.Clear();
        }

        public void SetVisible(bool visible)
        {
            if (_fillRenderer != null)
            {
                _fillRenderer.enabled = visible;
            }

            if (_outlineRenderer != null)
            {
                _outlineRenderer.enabled = visible;
            }
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void CreatePlacementHighlight(BoardVisualCell cell)
        {
            _projector.GetCellTopCorners(
                cell,
                out Vector3 frontLeft,
                out Vector3 frontRight,
                out Vector3 backRight,
                out Vector3 backLeft);

            Vector3[] vertices =
            {
                _highlightRoot.InverseTransformPoint(frontLeft),
                _highlightRoot.InverseTransformPoint(frontRight),
                _highlightRoot.InverseTransformPoint(backRight),
                _highlightRoot.InverseTransformPoint(backLeft)
            };

            GameObject target = new GameObject($"DeployableCell_{cell.CellId}");
            target.transform.SetParent(_highlightRoot, false);

            Mesh mesh = new Mesh { name = $"Mesh_DeployableCell_{cell.CellId}" };
            mesh.vertices = vertices;
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();

            MeshFilter filter = target.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = target.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _placementMaterial;
            renderer.sortingOrder = _sorting.GetHighlightOrder(cell) - 1;

            _placementHighlights.Add(target);
            _placementMeshes.Add(mesh);
        }

        private static Material CreateRuntimeMaterial(Color color, out Material runtimeMaterial)
        {
            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");
            runtimeMaterial = new Material(shader)
            {
                name = $"Runtime_BoardHighlight_{color}",
                color = color
            };
            return runtimeMaterial;
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
