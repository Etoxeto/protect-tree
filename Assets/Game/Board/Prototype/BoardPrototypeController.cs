using ProtectTree.Runtime.Board;
using UnityEngine;

namespace ProtectTree.BoardPrototype
{
    /// <summary>
    /// 仅用于 BoardPrototype 场景验证表现组件，不参与正式比赛玩法判定。
    /// </summary>
    public sealed class BoardPrototypeController : MonoBehaviour
    {
        [Header("Board Size")]
        [SerializeField] private int boardWidth = 4;
        [SerializeField] private int boardHeight = 3;

        [Header("Board Map")]
        [SerializeField] private BoardMapDefinition mapDefinition;

        [Header("Board Visual")]
        [SerializeField] private BoardVisualDefinition visualDefinition;

        [Header("Projection")]
        [SerializeField] private float tileWidth = 1.6f;
        [SerializeField] private float tileDepth = 0.8f;
        [SerializeField] private float heightStep = 0.35f;
        [SerializeField] private float frontRowScale = 1.0f;
        [SerializeField] private float backRowScale = 0.82f;

        [Header("Visual Root")]
        [SerializeField] private Transform meshRoot;
        [SerializeField] private Transform highlightRoot;
        [SerializeField] private Transform unitRoot;

        [Header("Debug")]
        [SerializeField] private bool drawProjectionGizmos = true;
        [SerializeField] private float centerPointRadius = 0.035f;

        [Header("Selection Highlight")]
        [SerializeField] private float selectedOutlineWidth = 0.045f;

        [Header("Unit Prototype")]
        [SerializeField] private float unitPreviewWidth = 0.38f;
        [SerializeField] private float unitPreviewHeight = 0.85f;
        [Range(0f, 1f)]
        [SerializeField] private float unitAnchorDepth = 0.45f;

        private BoardVisualLayout _layout;
        private BoardPerspectiveProjector _projector;
        private BoardSortingPolicy _sorting;
        private BoardCellPicker _picker;
        private BoardCellHighlightView _highlight;
        private BoardPrototypeUnitPreviewView _unitPreview;

        private void Start()
        {
            _layout = mapDefinition != null
                ? mapDefinition.CreateLayout()
                : BoardPrototypeLayoutFactory.CreateDefault(boardWidth, boardHeight);
            boardWidth = _layout.Width;
            boardHeight = _layout.Height;

            _projector = new BoardPerspectiveProjector(
                transform,
                _layout.Width,
                _layout.Height,
                tileWidth,
                tileDepth,
                heightStep,
                frontRowScale,
                backRowScale);
            _sorting = new BoardSortingPolicy(_layout.Height);
            _picker = new BoardCellPicker(_layout, _projector, _sorting);

            BoardStaticView staticView = GetOrAddComponent<BoardStaticView>();
            staticView.Build(meshRoot, visualDefinition, _layout, _projector, _sorting);

            _highlight = GetOrAddComponent<BoardCellHighlightView>();
            _highlight.Initialize(
                highlightRoot,
                visualDefinition,
                _projector,
                _sorting,
                selectedOutlineWidth);

            _unitPreview = GetOrAddComponent<BoardPrototypeUnitPreviewView>();
            _unitPreview.Initialize(
                unitRoot,
                visualDefinition,
                _projector,
                _sorting,
                unitPreviewWidth,
                unitPreviewHeight,
                unitAnchorDepth);

            Debug.Log(BoardPrototypeLayoutFactory.BuildDebugText(_layout));
        }

        private T GetOrAddComponent<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        private void Update()
        {
            if (!Input.GetMouseButtonDown(0))
            {
                return;
            }

            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogWarning("Board prototype cannot pick a cell without Main Camera.");
                return;
            }

            Vector3 world = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            if (!_picker.TryPick(world, out BoardVisualCell cell))
            {
                _highlight.SetVisible(false);
                _unitPreview.Hide();
                return;
            }

            _highlight.Show(cell);

            // 这里只演示单位锚点；正式部署是否合法仍必须交由 Lua 权威层判断。
            if (cell.Terrain == BoardTerrainType.Obstacle)
            {
                _unitPreview.Hide();
            }
            else
            {
                _unitPreview.Show(cell);
            }

            Debug.Log(
                $"Clicked visual cell {cell.CellId}: ({cell.X}, {cell.Y}), "
                + $"Terrain = {cell.Terrain}, Height = {cell.Height}");
        }

        private void OnDrawGizmos()
        {
            if (!drawProjectionGizmos || _layout == null || _projector == null)
            {
                return;
            }

            foreach (BoardVisualCell cell in _layout.Cells)
            {
                _projector.GetCellTopCorners(
                    cell,
                    out Vector3 frontLeft,
                    out Vector3 frontRight,
                    out Vector3 backRight,
                    out Vector3 backLeft);

                Gizmos.color = cell.Terrain == BoardTerrainType.Ground
                    ? Color.white
                    : cell.Terrain == BoardTerrainType.HighGround
                        ? Color.yellow
                        : Color.red;
                Gizmos.DrawLine(frontLeft, frontRight);
                Gizmos.DrawLine(frontRight, backRight);
                Gizmos.DrawLine(backRight, backLeft);
                Gizmos.DrawLine(backLeft, frontLeft);
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(_projector.GetCellCenter(cell), centerPointRadius);
            }
        }
    }
}
