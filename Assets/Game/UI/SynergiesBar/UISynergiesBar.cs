using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Runtime.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace ProtectTree.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class UISynergiesBar : MatchSceneFeature
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField] private UISynergiesBarItem itemPrefab;

        private readonly List<UISynergiesBarItem> _items =
            new List<UISynergiesBarItem>();
        private int _visibleCount = -1;

        public override void Refresh(MatchSceneContext context)
        {
            Render(context.Pieces?.SynergyProgresses, context.LocalPlayerId);
        }

        public override void OnRuntimeUnavailable()
        {
            Render(null, 0);
        }

        private void Render(
            IReadOnlyList<SynergyProgressSnapshot> synergies,
            int localPlayerId)
        {
            int visibleIndex = 0;
            if (synergies != null)
            {
                for (int index = 0; index < synergies.Count; index++)
                {
                    SynergyProgressSnapshot synergy = synergies[index];
                    if (synergy.PlayerId != localPlayerId
                        || synergy.UniquePieceCount <= 0)
                    {
                        continue;
                    }

                    UISynergiesBarItem item = EnsureItem(visibleIndex);
                    if (item == null)
                    {
                        break;
                    }

                    item.Render(synergy);
                    visibleIndex++;
                }
            }

            for (int index = visibleIndex; index < _items.Count; index++)
            {
                _items[index].Hide();
            }

            if (_visibleCount != visibleIndex)
            {
                _visibleCount = visibleIndex;
                RebuildContentWidth();
            }

            if (scrollRect != null)
            {
                scrollRect.horizontalNormalizedPosition = 0f;
            }
        }

        private UISynergiesBarItem EnsureItem(int index)
        {
            while (_items.Count <= index)
            {
                if (itemPrefab == null || content == null)
                {
                    Debug.LogError(
                        "Synergy bar item prefab and content must be assigned.",
                        this);
                    return null;
                }

                UISynergiesBarItem item = Instantiate(itemPrefab, content);
                _items.Add(item);
            }

            return _items[index];
        }

        private void RebuildContentWidth()
        {
            if (content == null || viewport == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();

            float preferredWidth = LayoutUtility.GetPreferredWidth(content);
            float viewportWidth = viewport.rect.width;

            float finalWidth = Mathf.Max(preferredWidth, viewportWidth);

            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, finalWidth);

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }
    }
}
