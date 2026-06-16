using ProtectTree.Runtime.Board;
using UnityEngine;

namespace ProtectTree.BoardPrototype
{
    /// <summary>
    /// 原型场景中的单位锚点占位图，不是正式单位表现。
    /// </summary>
    public sealed class BoardPrototypeUnitPreviewView : MonoBehaviour
    {
        private Transform _unitRoot;
        private BoardPerspectiveProjector _projector;
        private BoardSortingPolicy _sorting;
        private float _width;
        private float _height;
        private float _anchorDepth;
        private GameObject _preview;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Material _runtimeMaterial;

        public void Initialize(
            Transform unitRoot,
            BoardVisualDefinition visualDefinition,
            BoardPerspectiveProjector projector,
            BoardSortingPolicy sorting,
            float width,
            float height,
            float anchorDepth)
        {
            _unitRoot = unitRoot;
            _projector = projector;
            _sorting = sorting;
            _width = width;
            _height = height;
            _anchorDepth = anchorDepth;

            _preview = new GameObject("UnitPreview");
            _preview.transform.SetParent(_unitRoot, false);
            MeshFilter filter = _preview.AddComponent<MeshFilter>();
            _renderer = _preview.AddComponent<MeshRenderer>();
            _mesh = new Mesh { name = "Mesh_UnitPreview" };
            filter.sharedMesh = _mesh;

            if (visualDefinition != null && visualDefinition.UnitPreviewMaterial != null)
            {
                _renderer.sharedMaterial = visualDefinition.UnitPreviewMaterial;
            }
            else
            {
                Shader shader = Shader.Find("Sprites/Default")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color");
                _runtimeMaterial = new Material(shader)
                {
                    name = "Runtime_BoardUnitPreview",
                    color = new Color(0.2f, 0.8f, 1f, 1f)
                };
                _renderer.sharedMaterial = _runtimeMaterial;
            }

            Hide();
        }

        public void Show(BoardVisualCell cell)
        {
            Vector3 anchorWorld = _projector.GetCellUnitAnchorWorld(cell, _anchorDepth);
            Vector3 anchor = _unitRoot.InverseTransformPoint(anchorWorld);
            float width = _width;
            float height = _height;

            _mesh.Clear();
            _mesh.vertices = new[]
            {
                anchor + new Vector3(-width * 0.28f, 0f, 0f),
                anchor + new Vector3(width * 0.28f, 0f, 0f),
                anchor + new Vector3(width * 0.5f, height * 0.62f, 0f),
                anchor + new Vector3(0f, height, 0f),
                anchor + new Vector3(-width * 0.5f, height * 0.62f, 0f)
            };
            _mesh.triangles = new[] { 0, 1, 2, 0, 2, 4, 4, 2, 3 };
            _mesh.RecalculateBounds();

            _renderer.sortingOrder = _sorting.GetUnitOrder(cell);
            _preview.SetActive(true);
        }

        public void Hide()
        {
            if (_preview != null)
            {
                _preview.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(_mesh);
            DestroyRuntimeObject(_runtimeMaterial);
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
