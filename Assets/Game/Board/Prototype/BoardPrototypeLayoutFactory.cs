using System.Collections.Generic;
using System.Text;
using System;
using ProtectTree.Runtime.Board;

namespace ProtectTree.BoardPrototype
{
    public static class BoardPrototypeLayoutFactory
    {
        public static BoardVisualLayout CreateDefault(int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            List<BoardVisualCell> cells = new List<BoardVisualCell>(width * height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int cellId = y * width + x + 1;
                    BoardTerrainType terrain = BoardTerrainType.Ground;
                    int cellHeight = 0;

                    if ((x == 1 || x == 2) && y == 1)
                    {
                        terrain = BoardTerrainType.HighGround;
                        cellHeight = 1;
                    }
                    else if (x == 3 && y == 2)
                    {
                        terrain = BoardTerrainType.Obstacle;
                    }

                    cells.Add(new BoardVisualCell(
                        cellId,
                        x,
                        y,
                        cellHeight,
                        terrain));
                }
            }

            return new BoardVisualLayout(width, height, cells);
        }

        public static string BuildDebugText(BoardVisualLayout layout)
        {
            StringBuilder builder = new StringBuilder();

            builder.AppendLine("Board data generated:");
            builder.AppendLine($"Size: {layout.Width} x {layout.Height}");
            builder.AppendLine();

            for (int y = layout.Height - 1; y >= 0; y--)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    BoardVisualCell cell = layout.GetCell(x, y);

                    if (cell == null)
                    {
                        builder.Append("[?:H?] ");
                        continue;
                    }

                    string typeMark = cell.Terrain switch
                    {
                        BoardTerrainType.Ground => "G",
                        BoardTerrainType.HighGround => "H",
                        BoardTerrainType.Obstacle => "X",
                        _ => "?"
                    };

                    builder.Append($"[{typeMark}:H{cell.Height}] ");
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }
    }
}
