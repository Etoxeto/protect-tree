
using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    public sealed class BoardPerspectiveProjector
    {
        public int BoardWidth { get; }
        public int BoardHeight { get; }

        private readonly Transform rootTransform;
        private readonly float tileWidth;
        private readonly float tileDepth;
        private readonly float heightStep;
        private readonly float frontRowScale;
        private readonly float backRowScale;

        public BoardPerspectiveProjector(
            Transform rootTransform,
            int boardWidth,
            int boardHeight,
            float tileWidth,
            float tileDepth,
            float heightStep,
            float frontRowScale,
            float backRowScale)
        {
            this.rootTransform = rootTransform;
            BoardWidth = boardWidth;
            BoardHeight = boardHeight;
            this.tileWidth = tileWidth;
            this.tileDepth = tileDepth;
            this.heightStep = heightStep;
            this.frontRowScale = frontRowScale;
            this.backRowScale = backRowScale;
        }

        public Vector3 ProjectGridPoint(float gx, float gy, float height)
        {
            float normalizedY = BoardHeight <= 0 ? 0f : gy / BoardHeight;
            float rowScale = Mathf.Lerp(frontRowScale, backRowScale, normalizedY);

            float boardCenterX = BoardWidth * 0.5f;

            float worldX = (gx - boardCenterX) * tileWidth * rowScale;
            float worldY = gy * tileDepth + height * heightStep;

            Vector3 localPoint = new Vector3(worldX, worldY, 0f);

            if (rootTransform == null)
            {
                return localPoint;
            }

            return rootTransform.TransformPoint(localPoint);
        }

        public void GetCellTopCorners(
            BoardVisualCell cell,
            out Vector3 frontLeft,
            out Vector3 frontRight,
            out Vector3 backRight,
            out Vector3 backLeft)
        {
            int x = cell.X;
            int y = cell.Y;
            int h = cell.Height;

            frontLeft = ProjectGridPoint(x, y, h);
            frontRight = ProjectGridPoint(x + 1, y, h);
            backRight = ProjectGridPoint(x + 1, y + 1, h);
            backLeft = ProjectGridPoint(x, y + 1, h);
        }

        public Vector3 GetCellCenter(BoardVisualCell cell)
        {
            GetCellTopCorners(
                cell,
                out Vector3 frontLeft,
                out Vector3 frontRight,
                out Vector3 backRight,
                out Vector3 backLeft
            );

            return (frontLeft + frontRight + backRight + backLeft) * 0.25f;
        }

        public Vector3 GetCellUnitAnchorWorld(BoardVisualCell cell, float unitAnchorDepth)
        {
            GetCellTopCorners(
                cell,
                out Vector3 frontLeft,
                out Vector3 frontRight,
                out Vector3 backRight,
                out Vector3 backLeft
            );

            Vector3 frontCenter = (frontLeft + frontRight) * 0.5f;
            Vector3 backCenter = (backLeft + backRight) * 0.5f;

            return Vector3.Lerp(frontCenter, backCenter, unitAnchorDepth);
        }
    }
}
