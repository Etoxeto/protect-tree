using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    /// <summary>
    /// 直接使用投影后的格子多边形拾取，不要求为每个格子创建 Collider。
    /// </summary>
    public sealed class BoardCellPicker
    {
        private readonly BoardVisualLayout _layout;
        private readonly BoardPerspectiveProjector _projector;
        private readonly BoardSortingPolicy _sorting;

        public BoardCellPicker(
            BoardVisualLayout layout,
            BoardPerspectiveProjector projector,
            BoardSortingPolicy sorting)
        {
            _layout = layout;
            _projector = projector;
            _sorting = sorting;
        }

        public System.Collections.Generic.IReadOnlyList<BoardVisualCell> Cells =>
            _layout.Cells;

        public bool TryPick(Vector2 worldPoint, out BoardVisualCell pickedCell)
        {
            pickedCell = null;
            int bestOrder = int.MinValue;

            foreach (BoardVisualCell cell in _layout.Cells)
            {
                _projector.GetCellTopCorners(
                    cell,
                    out Vector3 frontLeft,
                    out Vector3 frontRight,
                    out Vector3 backRight,
                    out Vector3 backLeft);

                if (!ContainsPoint(
                        worldPoint,
                        frontLeft,
                        frontRight,
                        backRight,
                        backLeft))
                {
                    continue;
                }

                int order = _sorting.GetTopOrder(cell);
                if (order > bestOrder)
                {
                    bestOrder = order;
                    pickedCell = cell;
                }
            }

            return pickedCell != null;
        }

        private static bool ContainsPoint(
            Vector2 point,
            Vector2 a,
            Vector2 b,
            Vector2 c,
            Vector2 d)
        {
            bool hasNegative = Cross(a, b, point) < 0f
                || Cross(b, c, point) < 0f
                || Cross(c, d, point) < 0f
                || Cross(d, a, point) < 0f;
            bool hasPositive = Cross(a, b, point) > 0f
                || Cross(b, c, point) > 0f
                || Cross(c, d, point) > 0f
                || Cross(d, a, point) > 0f;

            return !(hasNegative && hasPositive);
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 point)
        {
            return (b.x - a.x) * (point.y - a.y)
                - (b.y - a.y) * (point.x - a.x);
        }
    }
}
