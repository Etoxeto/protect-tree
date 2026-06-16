using System;
using ProtectTree.Core.Match;
using ProtectTree.Runtime.Lua;
using ProtectTree.Runtime.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ProtectTree.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class UIShop : MatchSceneFeature, IPieceSellDropZone
    {
        [FormerlySerializedAs("grids")]
        [SerializeField] private Transform[] itemSlots;
        [SerializeField] private Button buttonHideOrShow;
        [SerializeField] private TextMeshProUGUI hideOrShowText;
        [SerializeField] private Button buttonLevelUp;
        [SerializeField] private TextMeshProUGUI levelUpText;
        [FormerlySerializedAs("refresh")]
        [SerializeField] private Button buttonRefresh;
        [SerializeField] private Button buttonLock;
        [SerializeField] private UIShopItem itemPrefab;
        [FormerlySerializedAs("itemsRoot")]
        [SerializeField] private GameObject itemSlotsRoot;

        [SerializeField] private TextMeshProUGUI gold_num;

        private UIShopItem[] _itemViews = Array.Empty<UIShopItem>();
        private TextMeshProUGUI _lockText;
        private Image _shopBackground;
        private LuaRuntime _runtime;
        private ShopSnapshot _shop;
        private bool _isContentVisible = true;
        private bool _isBattleHidden;
        private bool[] _battleHiddenChildStates = Array.Empty<bool>();
        private bool _hasBattleHiddenChildStates;
        private bool _hideOrShowButtonWasActive;

        private void Awake()
        {
            _shopBackground = GetComponent<Image>();
            _lockText = buttonLock != null
                ? buttonLock.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;

            CreateItemViews();
            AddButtonListeners();
            SetContentVisible(true);
        }

        private void OnDestroy()
        {
            RemoveButtonListeners();
        }

        public override void OnRuntimeChanged(LuaRuntime runtime)
        {
            _runtime = runtime;
        }

        public override void OnRuntimeUnavailable()
        {
            _runtime = null;
            _shop = null;
            SetBattleHidden(false);
            SetActionButtonsInteractable(false, false, false);
            SetText(gold_num, "--");

            foreach (UIShopItem itemView in _itemViews)
            {
                itemView?.Hide();
            }
        }

        public override void Refresh(MatchSceneContext context)
        {
            _runtime = context.Runtime;
            _shop = context.Shop;

            bool shouldHideShop = ShouldHideShop(context.Flow);
            SetBattleHidden(shouldHideShop);
            if (shouldHideShop)
            {
                SetActionButtonsInteractable(false, false, false);
                return;
            }

            PlayerSnapshot player = FindPlayer(context.Players, _shop.PlayerId);
            int gold = player?.Gold ?? 0;
            bool isPreparation = context.Flow.Phase == "Preparation"
                || context.Flow.Phase == "BossPreparation";
            bool hasBenchSpace = HasBenchSpace(context.Pieces, _shop.PlayerId);

            bool canRefresh = isPreparation && gold >= _shop.RefreshCost;
            bool canUpgrade = isPreparation
                && _shop.CanUpgrade
                && gold >= _shop.UpgradeCost;
            SetActionButtonsInteractable(canRefresh, canUpgrade, isPreparation);
            SetText(gold_num, gold.ToString());
            RenderButtonText();
            RenderItems(gold, isPreparation && hasBenchSpace);
        }

        public bool CanAcceptPieceSellDrop(
            MatchSceneContext context,
            PieceSnapshot piece)
        {
            if (context == null
                || piece == null
                || !_isContentVisible
                || _isBattleHidden)
            {
                return false;
            }

            bool isPreparation = context.Flow.Phase == "Preparation"
                || context.Flow.Phase == "BossPreparation";
            return isPreparation
                && piece.OwnerPlayerId == context.LocalPlayerId;
        }

        private void CreateItemViews()
        {
            if (itemSlots == null || itemPrefab == null)
            {
                Debug.LogError("Shop item slots and item prefab must be assigned.", this);
                return;
            }

            _itemViews = new UIShopItem[itemSlots.Length];
            for (int index = 0; index < itemSlots.Length; index++)
            {
                if (itemSlots[index] == null)
                {
                    Debug.LogError($"Shop item slot {index + 1} is not assigned.", this);
                    continue;
                }

                UIShopItem itemView = Instantiate(itemPrefab, itemSlots[index], false);
                RectTransform rectTransform = itemView.transform as RectTransform;
                if (rectTransform != null)
                {
                    // 槽位是零尺寸定位节点，商品卡必须保留 Prefab 自身尺寸，不能拉伸到父节点。
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.anchoredPosition = Vector2.zero;
                    rectTransform.localScale = Vector3.one;
                }

                // 商品卡固定置于槽位最后，确保它会覆盖槽位中的 nothing 提示文本。
                itemView.transform.SetAsLastSibling();
                itemView.Initialize(RequestPurchase);
                itemView.Hide();
                _itemViews[index] = itemView;
            }
        }

        private void AddButtonListeners()
        {
            buttonHideOrShow?.onClick.AddListener(ToggleContent);
            buttonLevelUp?.onClick.AddListener(RequestUpgrade);
            buttonRefresh?.onClick.AddListener(RequestRefresh);
            buttonLock?.onClick.AddListener(RequestToggleLock);
        }

        private void RemoveButtonListeners()
        {
            buttonHideOrShow?.onClick.RemoveListener(ToggleContent);
            buttonLevelUp?.onClick.RemoveListener(RequestUpgrade);
            buttonRefresh?.onClick.RemoveListener(RequestRefresh);
            buttonLock?.onClick.RemoveListener(RequestToggleLock);
        }

        private void RenderItems(int gold, bool canUseShop)
        {
            for (int index = 0; index < _itemViews.Length; index++)
            {
                UIShopItem itemView = _itemViews[index];
                if (itemView == null)
                {
                    continue;
                }

                if (index >= _shop.Offers.Count || _shop.Offers[index].IsSold)
                {
                    itemView.Hide();
                    continue;
                }

                ShopOfferSnapshot offer = _shop.Offers[index];
                bool canAfford = gold >= offer.Cost;
                itemView.Render(offer, canAfford, canUseShop && canAfford);
            }
        }

        private void RenderButtonText()
        {
            if (levelUpText != null)
            {
                levelUpText.text = _shop.CanUpgrade
                    ? $"等级 {_shop.Level}  升级 {_shop.UpgradeCost}"
                    : $"等级 {_shop.Level}  已满级";
            }

            if (_lockText != null)
            {
                _lockText.text = _shop.IsLocked ? "解锁" : "锁定";
            }
        }

        private void ToggleContent()
        {
            if (_isBattleHidden)
            {
                return;
            }

            SetContentVisible(!_isContentVisible);
        }

        private void SetContentVisible(bool isVisible)
        {
            _isContentVisible = isVisible;
            ApplyVisibility();
        }

        private void SetBattleHidden(bool isHidden)
        {
            if (_isBattleHidden == isHidden)
            {
                return;
            }

            if (isHidden)
            {
                CaptureAndHideShopChildren();
            }
            else
            {
                RestoreShopChildren();
            }

            _isBattleHidden = isHidden;
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            bool shopVisible = !_isBattleHidden;
            bool contentVisible = shopVisible && _isContentVisible;

            // 战斗阶段整页商店都不可操作，因此连“显示/隐藏商店”按钮也一起临时隐藏。
            SetActive(buttonHideOrShow, shopVisible);
            itemSlotsRoot?.SetActive(contentVisible);
            SetActive(buttonLevelUp, contentVisible);
            SetActive(buttonRefresh, contentVisible);
            SetActive(buttonLock, contentVisible);

            if (_shopBackground != null)
            {
                _shopBackground.enabled = contentVisible;
            }

            if (hideOrShowText != null)
            {
                hideOrShowText.text = _isContentVisible ? "收起" : "商店";
            }
        }

        private void CaptureAndHideShopChildren()
        {
            _hideOrShowButtonWasActive = buttonHideOrShow != null
                && buttonHideOrShow.gameObject.activeSelf;

            int childCount = transform.childCount;
            if (_battleHiddenChildStates.Length != childCount)
            {
                _battleHiddenChildStates = new bool[childCount];
            }

            for (int index = 0; index < childCount; index++)
            {
                Transform child = transform.GetChild(index);
                _battleHiddenChildStates[index] = child.gameObject.activeSelf;
                child.gameObject.SetActive(false);
            }

            if (buttonHideOrShow != null)
            {
                buttonHideOrShow.gameObject.SetActive(false);
            }

            _hasBattleHiddenChildStates = true;
        }

        private void RestoreShopChildren()
        {
            if (_hasBattleHiddenChildStates)
            {
                int childCount = Mathf.Min(
                    transform.childCount,
                    _battleHiddenChildStates.Length);
                for (int index = 0; index < childCount; index++)
                {
                    Transform child = transform.GetChild(index);
                    child.gameObject.SetActive(_battleHiddenChildStates[index]);
                }
            }

            if (buttonHideOrShow != null)
            {
                buttonHideOrShow.gameObject.SetActive(_hideOrShowButtonWasActive);
            }

            _hasBattleHiddenChildStates = false;
        }

        private void RequestPurchase(int slotIndex)
        {
            TryExecute(() => _runtime.PurchaseShopOffer(_shop.PlayerId, slotIndex));
        }

        private void RequestRefresh()
        {
            TryExecute(() => _runtime.RefreshShop(_shop.PlayerId));
        }

        private void RequestUpgrade()
        {
            TryExecute(() => _runtime.UpgradeShop(_shop.PlayerId));
        }

        private void RequestToggleLock()
        {
            TryExecute(() => _runtime.ToggleShopLock(_shop.PlayerId));
        }

        private void TryExecute(Action command)
        {
            if (_runtime == null || _shop == null)
            {
                return;
            }

            try
            {
                command();
            }
            catch (Exception exception)
            {
                // 快照与点击之间可能跨过一个 Tick，最终仍以 Lua 的权威验证结果为准。
                Debug.LogWarning($"Shop command was rejected: {exception.Message}", this);
            }
        }

        private void SetActionButtonsInteractable(
            bool canRefresh,
            bool canUpgrade,
            bool canToggleLock)
        {
            if (buttonRefresh != null)
            {
                buttonRefresh.interactable = canRefresh;
            }

            if (buttonLevelUp != null)
            {
                buttonLevelUp.interactable = canUpgrade;
            }

            if (buttonLock != null)
            {
                buttonLock.interactable = canToggleLock;
            }
        }

        private static void SetActive(Button button, bool isActive)
        {
            if (button != null)
            {
                button.gameObject.SetActive(isActive);
            }
        }

        private static void SetText(TextMeshProUGUI text, string value)
        {
            if (text != null)
            {
                text.text = value;
            }
        }

        private static bool IsBattlePhase(MatchFlowSnapshot flow)
        {
            return flow != null
                && (flow.Phase == "Battle" || flow.Phase == "BossBattle");
        }

        private static bool ShouldHideShop(MatchFlowSnapshot flow)
        {
            return flow == null || flow.IsFinished || IsBattlePhase(flow);
        }

        private static PlayerSnapshot FindPlayer(PlayerRosterSnapshot roster, int playerId)
        {
            foreach (PlayerSnapshot player in roster.Players)
            {
                if (player.PlayerId == playerId)
                {
                    return player;
                }
            }

            return null;
        }

        private static bool HasBenchSpace(PieceRosterSnapshot roster, int playerId)
        {
            foreach (PieceCapacitySnapshot capacity in roster.Players)
            {
                if (capacity.PlayerId == playerId)
                {
                    return capacity.BenchCount < capacity.BenchCapacity;
                }
            }

            return false;
        }
    }
}
