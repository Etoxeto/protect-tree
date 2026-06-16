using System.Collections.Generic;
using ProtectTree.Core.Match;
using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    [DisallowMultipleComponent]
    public sealed class BoardRouteView : MonoBehaviour
    {
        private static readonly Color[] RouteColors =
        {
            new Color(0.1f, 0.85f, 1f, 0.9f),
            new Color(1f, 0.25f, 0.85f, 0.9f),
            new Color(1f, 0.65f, 0.15f, 0.9f),
            new Color(0.45f, 1f, 0.35f, 0.9f),
        };

        private readonly List<Material> _runtimeMaterials = new List<Material>();
        private Transform _routeRoot;

        public void Build(
            Transform routeRoot,
            BoardVisualLayout layout,
            IReadOnlyList<BoardRouteSnapshot> routes,
            BoardPerspectiveProjector projector,
            BoardSortingPolicy sorting,
            float lineWidth,
            float routeSeparation)
        {
            _routeRoot = routeRoot;
            Clear();

            if (_routeRoot == null
                || layout == null
                || routes == null
                || projector == null
                || sorting == null)
            {
                Debug.LogError(
                    "BoardRouteView requires route root, layout, routes, projector, and sorting.");
                return;
            }

            for (int routeIndex = 0; routeIndex < routes.Count; routeIndex++)
            {
                BoardRouteSnapshot route = routes[routeIndex];
                Material material = CreateRuntimeMaterial(
                    RouteColors[routeIndex % RouteColors.Length],
                    route.RouteId);

                for (int sampleIndex = 1; sampleIndex < route.Samples.Count; sampleIndex++)
                {
                    BoardRouteSampleSnapshot previous = route.Samples[sampleIndex - 1];
                    BoardRouteSampleSnapshot current = route.Samples[sampleIndex];
                    BoardVisualCell previousCell = layout.GetCell(previous.CellId);
                    BoardVisualCell currentCell = layout.GetCell(current.CellId);

                    if (previousCell == null || currentCell == null)
                    {
                        Debug.LogError(
                            $"Board route {route.RouteId} references an unknown visual cell.");
                        continue;
                    }

                    CreateSegment(
                        route.RouteId,
                        sampleIndex,
                        routeIndex,
                        previousCell,
                        currentCell,
                        projector,
                        sorting,
                        material,
                        lineWidth,
                        routeSeparation);
                }
            }
        }

        public void Clear()
        {
            if (_routeRoot != null)
            {
                for (int index = _routeRoot.childCount - 1; index >= 0; index--)
                {
                    DestroyRuntimeObject(_routeRoot.GetChild(index).gameObject);
                }
            }

            foreach (Material material in _runtimeMaterials)
            {
                DestroyRuntimeObject(material);
            }

            _runtimeMaterials.Clear();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void CreateSegment(
            int routeId,
            int segmentIndex,
            int routeIndex,
            BoardVisualCell previousCell,
            BoardVisualCell currentCell,
            BoardPerspectiveProjector projector,
            BoardSortingPolicy sorting,
            Material material,
            float lineWidth,
            float routeSeparation)
        {
            Vector3 start = projector.GetCellCenter(previousCell);
            Vector3 end = projector.GetCellCenter(currentCell);
            Vector2 direction = end - start;
            Vector2 perpendicular = direction.sqrMagnitude > 0f
                ? new Vector2(-direction.y, direction.x).normalized
                : Vector2.zero;
            float signedOffset = (routeIndex - 0.5f) * routeSeparation;
            Vector3 offset = perpendicular * signedOffset;

            GameObject segment = new GameObject($"Route_{routeId}_Segment_{segmentIndex}");
            segment.transform.SetParent(_routeRoot, false);
            LineRenderer renderer = segment.AddComponent<LineRenderer>();
            renderer.useWorldSpace = false;
            renderer.positionCount = 2;
            renderer.startWidth = lineWidth;
            renderer.endWidth = lineWidth;
            renderer.numCapVertices = 3;
            renderer.sharedMaterial = material;
            renderer.SetPosition(
                0,
                _routeRoot.InverseTransformPoint(start + offset));
            renderer.SetPosition(
                1,
                _routeRoot.InverseTransformPoint(end + offset));

            // 路线盖住对应地面，但仍应被更高地形遮挡。
            renderer.sortingOrder = Mathf.Max(
                sorting.GetHighlightOrder(previousCell),
                sorting.GetHighlightOrder(currentCell));
        }

        private Material CreateRuntimeMaterial(Color color, int routeId)
        {
            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");
            Material material = new Material(shader)
            {
                name = $"Runtime_BoardRoute_{routeId}",
                color = color
            };
            _runtimeMaterials.Add(material);
            return material;
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
