using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Runtime.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
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
        [SerializeField] private UISynergyInfo synergyInfo; // 点开单个SynergyItem后显示的页面

        private readonly List<UISynergiesBarItem> _items =
            new List<UISynergiesBarItem>();
        private readonly List<RaycastResult> _uiRaycastResults =
            new List<RaycastResult>();
        private int _visibleCount = -1;
        private string _selectedSynergyId;

        private void Update()
        {
            if (string.IsNullOrEmpty(_selectedSynergyId)
                || synergyInfo == null
                || !synergyInfo.gameObject.activeInHierarchy
                || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (!IsPointerOverSynergyUi())
            {
                ClearSelection();
            }
        }

        public override void Refresh(MatchSceneContext context)
        {
            if (context == null)
            {
                Render(null, 0);
                return;
            }

            Render(context.Pieces, context.LocalPlayerId);
        }

        public override void OnRuntimeUnavailable()
        {
            ClearSelection();
            Render(null, 0);
        }

        private void Render(
            PieceRosterSnapshot pieces,
            int localPlayerId)
        {
            IReadOnlyList<SynergyProgressSnapshot> synergies =
                pieces?.SynergyProgresses;
            int visibleIndex = 0;
            SynergyProgressSnapshot selectedSynergy = null;
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
                    if (synergy.SynergyId == _selectedSynergyId)
                    {
                        selectedSynergy = synergy;
                    }

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

            if (string.IsNullOrEmpty(_selectedSynergyId))
            {
                synergyInfo?.Hide();
            }
            else if (selectedSynergy != null)
            {
                synergyInfo?.Render(selectedSynergy, pieces, localPlayerId);
            }
            else
            {
                ClearSelection();
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
                item.Initialize(OnItemClicked);
                _items.Add(item);
            }

            return _items[index];
        }

        private void OnItemClicked(
            UISynergiesBarItem item,
            SynergyProgressSnapshot synergy)
        {
            if (synergy == null)
            {
                return;
            }

            _selectedSynergyId = _selectedSynergyId == synergy.SynergyId
                ? null
                : synergy.SynergyId;
        }

        private void ClearSelection()
        {
            _selectedSynergyId = null;
            synergyInfo?.Hide();
        }

        private bool IsPointerOverSynergyUi()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            PointerEventData pointerData = new PointerEventData(eventSystem)
            {
                position = Input.mousePosition,
            };
            _uiRaycastResults.Clear();
            eventSystem.RaycastAll(pointerData, _uiRaycastResults);

            foreach (RaycastResult result in _uiRaycastResults)
            {
                if (IsInSynergyInfo(result.gameObject)
                    || IsInSynergyBarItem(result.gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsInSynergyInfo(GameObject target)
        {
            return synergyInfo != null
                && target != null
                && target.GetComponentInParent<UISynergyInfo>(true) == synergyInfo;
        }

        private static bool IsInSynergyBarItem(GameObject target)
        {
            return target != null
                && target.GetComponentInParent<UISynergiesBarItem>(true) != null;
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
