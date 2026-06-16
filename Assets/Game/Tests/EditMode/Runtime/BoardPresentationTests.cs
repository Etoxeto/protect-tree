using System;
using NUnit.Framework;
using ProtectTree.Core.Match;
using ProtectTree.Runtime.Board;
using UnityEngine;

namespace ProtectTree.Runtime.Tests
{
    public sealed class BoardPresentationTests
    {
        [Test]
        public void BoardVisualLayout_IndexesCellsByCoordinateAndAuthorityId()
        {
            BoardVisualCell cell = new BoardVisualCell(
                101,
                2,
                1,
                1,
                BoardTerrainType.HighGround);
            BoardVisualLayout layout = new BoardVisualLayout(
                4,
                3,
                new[] { cell });

            Assert.That(layout.GetCell(2, 1), Is.SameAs(cell));
            Assert.That(layout.GetCell(101), Is.SameAs(cell));
            Assert.That(layout.GetCell(999), Is.Null);
        }

        [Test]
        public void BoardVisualLayout_RejectsDuplicateAuthorityCellIds()
        {
            BoardVisualCell first = new BoardVisualCell(
                101,
                0,
                0,
                0,
                BoardTerrainType.Ground);
            BoardVisualCell second = new BoardVisualCell(
                101,
                1,
                0,
                0,
                BoardTerrainType.Ground);

            Assert.Throws<ArgumentException>(() =>
                new BoardVisualLayout(2, 1, new[] { first, second }));
        }

        [Test]
        public void BoardCellPicker_PicksProjectedCellWithoutCollider()
        {
            BoardVisualCell cell = new BoardVisualCell(
                101,
                0,
                0,
                0,
                BoardTerrainType.Ground);
            BoardVisualLayout layout = new BoardVisualLayout(
                1,
                1,
                new[] { cell });
            BoardPerspectiveProjector projector = new BoardPerspectiveProjector(
                null,
                1,
                1,
                1.6f,
                0.8f,
                0.35f,
                1f,
                0.82f);
            BoardCellPicker picker = new BoardCellPicker(
                layout,
                projector,
                new BoardSortingPolicy(layout.Height));

            bool found = picker.TryPick(projector.GetCellCenter(cell), out BoardVisualCell picked);

            Assert.That(found, Is.True);
            Assert.That(picked, Is.SameAs(cell));
        }

        [Test]
        public void BoardSortingPolicy_HighlightStaysBehindHigherTerrain()
        {
            BoardSortingPolicy sorting = new BoardSortingPolicy(7);
            BoardVisualCell selectedGround = new BoardVisualCell(
                57,
                1,
                5,
                0,
                BoardTerrainType.Ground);
            BoardVisualCell adjacentHighGround = new BoardVisualCell(
                58,
                2,
                5,
                1,
                BoardTerrainType.Obstacle);

            Assert.That(
                sorting.GetHighlightOrder(selectedGround),
                Is.GreaterThan(sorting.GetTopOrder(selectedGround)));
            Assert.That(
                sorting.GetHighlightOrder(selectedGround) + 1,
                Is.LessThan(sorting.GetSideOrder(adjacentHighGround, false)));
            Assert.That(
                sorting.GetHighlightOrder(selectedGround) + 1,
                Is.LessThan(sorting.GetTopOrder(adjacentHighGround)));
        }

        [Test]
        public void BoardSortingPolicy_CellTopStaysBetweenSideAndFront()
        {
            BoardSortingPolicy sorting = new BoardSortingPolicy(7);
            BoardVisualCell highGround = new BoardVisualCell(
                42,
                4,
                3,
                1,
                BoardTerrainType.HighGround);

            Assert.That(
                sorting.GetTopOrder(highGround),
                Is.GreaterThan(sorting.GetSideOrder(highGround, false)));
            Assert.That(
                sorting.GetTopOrder(highGround),
                Is.LessThan(sorting.GetSideOrder(highGround, true)));
        }

        [Test]
        public void BoardUnitVisualCatalog_DefaultCatalogMapsPrototypeUnits()
        {
            BoardUnitVisualCatalog catalog =
                Resources.Load<BoardUnitVisualCatalog>("Board/DefaultUnitVisualCatalog");

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.GetPiecePrefab("Bramble"), Is.Not.Null);
            Assert.That(catalog.GetEnemyPrefab("Crab"), Is.Not.Null);
            Assert.That(catalog.GetPiecePrefab("Sprout"), Is.Null);
        }

        [Test]
        public void BoardVisualLayoutConverter_UsesAuthorityVisualCoordinatesAndTerrain()
        {
            BoardSnapshot snapshot = new BoardSnapshot(
                "PersonalDefense",
                "Personal",
                4,
                2,
                -1,
                -1,
                11,
                11,
                new[]
                {
                    new BoardCellSnapshot(
                        103,
                        "HighGround",
                        "Battlefield",
                        0,
                        -1,
                        1,
                        0,
                        1,
                        "high_ground",
                        true,
                        false,
                        false,
                        false,
                        false,
                        Array.Empty<BoardCellRoutePositionSnapshot>()),
                },
                Array.Empty<BoardRouteSnapshot>());

            BoardVisualLayout layout = BoardVisualLayoutConverter.Create(snapshot);
            BoardVisualCell cell = layout.GetCell(103);

            Assert.That(layout.Width, Is.EqualTo(4));
            Assert.That(layout.Height, Is.EqualTo(2));
            Assert.That(cell.X, Is.EqualTo(1));
            Assert.That(cell.Y, Is.EqualTo(0));
            Assert.That(cell.Height, Is.EqualTo(1));
            Assert.That(cell.Terrain, Is.EqualTo(BoardTerrainType.HighGround));
            Assert.That(cell.VisualKey, Is.EqualTo("high_ground"));
            Assert.That(cell.Zone, Is.EqualTo("Battlefield"));
        }

        [Test]
        public void BoardVisualLayoutConverter_RejectsUnknownAuthorityTerrain()
        {
            BoardSnapshot snapshot = new BoardSnapshot(
                "PersonalDefense",
                "Personal",
                1,
                1,
                0,
                0,
                11,
                11,
                new[]
                {
                    new BoardCellSnapshot(
                        101,
                        "Water",
                        "Battlefield",
                        0,
                        0,
                        0,
                        0,
                        0,
                        "water",
                        true,
                        false,
                        false,
                        false,
                        false,
                        Array.Empty<BoardCellRoutePositionSnapshot>()),
                },
                Array.Empty<BoardRouteSnapshot>());

            Assert.Throws<ArgumentException>(
                () => BoardVisualLayoutConverter.Create(snapshot));
        }
    }
}
