using UnityEngine;
using ProtectTree.Runtime.Board;

namespace ProtectTree.BoardPrototype
{
    [CreateAssetMenu(
        fileName = "BoardMapDefinition",
        menuName = "Game/Board Prototype/Board Map Definition"
    )]
    public sealed class BoardMapDefinition : ScriptableObject
    {
        [SerializeField] private int width = 4;
        [SerializeField] private int height = 3;

        [TextArea(3, 12)]
        [SerializeField]
        private string rowsFromBackToFront =
            @"...X
            .11.
            ....";

        [TextArea(3, 12)]
        [SerializeField]
        private string visualRowsFromBackToFront =
            @"sssr
            .sss
            gggg";

        public int Width => Mathf.Max(1, width);
        public int Height => Mathf.Max(1, height);

        public BoardVisualLayout CreateLayout()
        {
            BoardVisualCell[] cells = new BoardVisualCell[Width * Height];
            int index = 0;

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    cells[index++] = CreateCell(x, y);
                }
            }

            return new BoardVisualLayout(Width, Height, cells);
        }

        public BoardVisualCell CreateCell(int x, int y)
        {
            char tileChar = GetTileChar(x, y);
            string visualKey = GetVisualKey(x, y);
            int cellId = y * Width + x + 1;

            switch (tileChar)
            {
                case '.':
                    return new BoardVisualCell(
                        cellId, x, y, 0, BoardTerrainType.Ground, visualKey);

                case '1':
                    return new BoardVisualCell(
                        cellId, x, y, 1, BoardTerrainType.HighGround, visualKey);

                case '2':
                    return new BoardVisualCell(
                        cellId, x, y, 2, BoardTerrainType.HighGround, visualKey);

                case '3':
                    return new BoardVisualCell(
                        cellId, x, y, 3, BoardTerrainType.HighGround, visualKey);

                case 'X':
                case 'x':
                case '#':
                    return new BoardVisualCell(
                        cellId, x, y, 0, BoardTerrainType.Obstacle, visualKey);

                case 'A':
                case 'a':
                    return new BoardVisualCell(
                        cellId, x, y, 1, BoardTerrainType.Obstacle, visualKey);

                case 'B':
                case 'b':
                    return new BoardVisualCell(
                        cellId, x, y, 2, BoardTerrainType.Obstacle, visualKey);

                case 'C':
                case 'c':
                    return new BoardVisualCell(
                        cellId, x, y, 3, BoardTerrainType.Obstacle, visualKey);

                default:
                    Debug.LogWarning($"Unknown board tile char '{tileChar}' at ({x}, {y}), fallback to Ground.");
                    return new BoardVisualCell(
                        cellId, x, y, 0, BoardTerrainType.Ground, visualKey);
            }
        }

        private string GetVisualKey(int x, int y)
        {
            char visualChar = GetCharFromRows(visualRowsFromBackToFront, x, y);

            switch (visualChar)
            {
                case 's':
                case 'S':
                    return "stone";

                case 'g':
                case 'G':
                    return "grass";

                case 'd':
                case 'D':
                    return "dirt";

                case 'r':
                case 'R':
                    return "rock";

                case 'c':
                case 'C':
                    return "crystal";

                default:
                    return "";
            }
        }

        private char GetTileChar(int x, int y)
        {
            return GetCharFromRows(rowsFromBackToFront, x, y);
        }

        private char GetCharFromRows(string sourceRows, int x, int y)
        {
            string[] rows = sourceRows
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');

            int rowIndex = Height - 1 - y;

            if (rowIndex < 0 || rowIndex >= rows.Length)
            {
                return '.';
            }

            string row = rows[rowIndex];

            if (string.IsNullOrEmpty(row))
            {
                return '.';
            }

            if (x < 0 || x >= row.Length)
            {
                return '.';
            }

            return row[x];
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
        }
#endif
    }
}
