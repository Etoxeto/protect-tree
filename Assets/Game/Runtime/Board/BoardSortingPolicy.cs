using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    public sealed class BoardSortingPolicy
    {
        private const int RowStride = 2000;
        private const int HeightStride = 200;
        private const int SideStage = 40;
        private const int TopStage = 60;
        private const int FrontStage = 80;
        private const int HighlightStage = 160;
        private const int UnitStage = 900;

        private readonly int _boardHeight;

        public BoardSortingPolicy(int boardHeight)
        {
            _boardHeight = boardHeight;
        }

        public int GetTopOrder(BoardVisualCell cell)
        {
            return Clamp(GetRowBase(cell.Y) + cell.Height * HeightStride + TopStage + cell.X);
        }

        public int GetSideOrder(BoardVisualCell cell, bool isFront)
        {
            int stage = isFront ? FrontStage : SideStage;
            return Clamp(GetRowBase(cell.Y) + cell.Height * HeightStride + stage + cell.X);
        }

        public int GetUnitOrder(BoardVisualCell cell)
        {
            return Clamp(GetRowBase(cell.Y) + cell.Height * HeightStride + UnitStage + cell.X);
        }

        public int GetHighlightOrder(BoardVisualCell cell)
        {
            // 高亮盖住所属高度的顶面，但仍会被更高一级地形的侧面和顶面遮挡。
            return Clamp(
                GetRowBase(cell.Y)
                + cell.Height * HeightStride
                + HighlightStage
                + cell.X);
        }

        private int GetRowBase(int y)
        {
            return (_boardHeight - y) * RowStride;
        }

        private static int Clamp(int order)
        {
            return Mathf.Clamp(order, short.MinValue, short.MaxValue);
        }
    }
}
