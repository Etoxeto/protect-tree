using System;
using System.Collections.Generic;

namespace ProtectTree.Runtime.Board
{
    /// <summary>
    /// 一次构建棋盘画面所需的只读布局。正式接入后应由 Lua 权威棋盘快照转换而来。
    /// </summary>
    public sealed class BoardVisualLayout
    {
        private readonly BoardVisualCell[,] _cells;
        private readonly IReadOnlyList<BoardVisualCell> _orderedCells;
        private readonly Dictionary<int, BoardVisualCell> _cellsById;

        public BoardVisualLayout(
            int width,
            int height,
            IEnumerable<BoardVisualCell> cells)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            Width = width;
            Height = height;
            _cells = new BoardVisualCell[width, height];
            _cellsById = new Dictionary<int, BoardVisualCell>(width * height);
            List<BoardVisualCell> orderedCells =
                new List<BoardVisualCell>(width * height);

            foreach (BoardVisualCell cell in cells)
            {
                if (cell == null)
                {
                    throw new ArgumentException("Board layout cannot contain null cells.", nameof(cells));
                }

                if (!IsInside(cell.X, cell.Y))
                {
                    throw new ArgumentException(
                        $"Cell {cell.CellId} at ({cell.X}, {cell.Y}) is outside the board.",
                        nameof(cells));
                }

                if (_cells[cell.X, cell.Y] != null)
                {
                    throw new ArgumentException(
                        $"Board layout contains duplicate coordinate ({cell.X}, {cell.Y}).",
                        nameof(cells));
                }

                if (cell.CellId <= 0 || _cellsById.ContainsKey(cell.CellId))
                {
                    throw new ArgumentException(
                        $"Board layout contains invalid or duplicate cell ID {cell.CellId}.",
                        nameof(cells));
                }

                _cells[cell.X, cell.Y] = cell;
                _cellsById.Add(cell.CellId, cell);
                orderedCells.Add(cell);
            }

            _orderedCells = orderedCells.AsReadOnly();
        }

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<BoardVisualCell> Cells => _orderedCells;

        public bool IsInside(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        public BoardVisualCell GetCell(int x, int y)
        {
            return IsInside(x, y) ? _cells[x, y] : null;
        }

        public BoardVisualCell GetCell(int cellId)
        {
            return _cellsById.TryGetValue(cellId, out BoardVisualCell cell)
                ? cell
                : null;
        }

        public int GetHeightOrZero(int x, int y)
        {
            BoardVisualCell cell = GetCell(x, y);
            return cell == null ? 0 : cell.Height;
        }
    }
}
