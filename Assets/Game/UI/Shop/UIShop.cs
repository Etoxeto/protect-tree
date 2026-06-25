using System;
using ProtectTree.Core.Match;
using ProtectTree.Core.Network;
using ProtectTree.Runtime.Lua;
using ProtectTree.Runtime.Network;
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
        [SerializeField] private UIPieceInspectPanel pieceInspectPanel;

        [SerializeField] private TextMeshProUGUI gold_num;

        private UIShopItem[] _itemViews = Array.Empty<UIShopItem>();
        private TextMeshProUGUI _lockText;
        private Image _shopBackground;
        private LuaRuntime _runtime;
        private LanMatchRuntime _lanMatch;
        private MatchSceneContext _context;
        private ShopSnapshot _shop;
        private bool _isContentVisible = true;
        private bool _isBattleHidden;
        private bool[] _battleHiddenChildStates = Array.Empty<bool>();
        private bool _hasBattleHiddenChildStates;
        private bool _hideOrShowButtonWasActive;
        private int _pendingPurchaseSlotIndex;
        private int _pendingPurchaseStartedFrame = -1;

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

        private void Update()
        {
            if (_pendingPurchaseSlotIndex <= 0 || !Input.GetMouseButtonDown(0))
            {
                return;
            }

            if (Time.frameCount == _pendingPurchaseStartedFrame)
            {
                return;
            }

            UIShopItem itemView = FindItemView(_pendingPurchaseSlotIndex);
            if (itemView == null
                || !itemView.gameObject.activeInHierarchy
                || !itemView.ContainsScreenPoint(Input.mousePosition))
            {
                ClearPendingPurchase();
            }
        }

        public override void OnRuntimeChanged(LuaRuntime runtime)
        {
            _runtime = runtime;
        }

        public override void OnRuntimeUnavailable()
        {
            _runtime = null;
            _lanMatch = null;
            _context = null;
            _shop = null;
            ClearPendingPurchase(clearInspectSelection: false);
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
            _context = context;
            _runtime = context.Runtime;
            _lanMatch = context.LanMatch;
            _shop = context.Shop;

            bool shouldHideShop = ShouldHideShop(context.Flow);
            SetBattleHidden(shouldHideShop);
            if (shouldHideShop)
            {
                ClearPendingPurchase();
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
            bool canUseShop = isPreparation && hasBenchSpace;
            ClearPendingIfInvalid();
            SetActionButtonsInteractable(canRefresh, canUpgrade, isPreparation);
            SetText(gold_num, gold.ToString());
            RenderButtonText();
            RenderItems(gold, canUseShop, isPreparation);
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

        private void RenderItems(int gold, bool canUseShop, bool canInspect)
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
                itemView.Render(
                    offer,
                    canAfford,
                    canUseShop && canAfford,
                    _pendingPurchaseSlotIndex == offer.SlotIndex,
                    canInspect);
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
            if (_shop == null)
            {
                return;
            }

            if (_pendingPurchaseSlotIndex != slotIndex)
            {
                ShopOfferSnapshot offer = FindOffer(_shop, slotIndex);
                if (offer == null || offer.IsSold)
                {
                    return;
                }

                _pendingPurchaseSlotIndex = slotIndex;
                _pendingPurchaseStartedFrame = Time.frameCount;
                _context?.SetPieceInspectBlocked(false);
                _context?.SelectShopOffer(slotIndex, offer);
                ResolvePieceInspectPanel()?.PreviewShopOffer(offer);
                return;
            }

            if (!CanPurchaseOffer(slotIndex))
            {
                return;
            }

            ClearPendingPurchase();
            if (TryExecute(
                MatchCommand.PurchaseShopOffer(slotIndex),
                () => _runtime.PurchaseShopOffer(_shop.PlayerId, slotIndex)))
            {
                ProtectTree.AudioManager.PlayCoinSpend();
            }
        }

        private void ClearPendingIfInvalid()
        {
            if (_pendingPurchaseSlotIndex <= 0)
            {
                return;
            }

            ShopOfferSnapshot offer = FindOffer(_shop, _pendingPurchaseSlotIndex);
            if (offer == null || offer.IsSold)
            {
                ClearPendingPurchase();
            }
        }

        private bool CanPurchaseOffer(int slotIndex)
        {
            if (_context == null || _shop == null)
            {
                return false;
            }

            ShopOfferSnapshot offer = FindOffer(_shop, slotIndex);
            if (offer == null || offer.IsSold)
            {
                return false;
            }

            bool isPreparation = _context.Flow != null
                && (_context.Flow.Phase == "Preparation"
                    || _context.Flow.Phase == "BossPreparation");
            if (!isPreparation || !HasBenchSpace(_context.Pieces, _shop.PlayerId))
            {
                return false;
            }

            PlayerSnapshot player = FindPlayer(_context.Players, _shop.PlayerId);
            return player != null && player.Gold >= offer.Cost;
        }

        private void ClearPendingPurchase(bool clearInspectSelection = true)
        {
            if (_pendingPurchaseSlotIndex <= 0)
            {
                return;
            }

            _pendingPurchaseSlotIndex = 0;
            _pendingPurchaseStartedFrame = -1;
            ResolvePieceInspectPanel()?.ClearShopOfferPreview();
            if (clearInspectSelection)
            {
                _context?.ClearShopOfferSelection();
            }
        }

        private UIPieceInspectPanel ResolvePieceInspectPanel()
        {
            if (pieceInspectPanel != null)
            {
                return pieceInspectPanel;
            }

            pieceInspectPanel = FindObjectOfType<UIPieceInspectPanel>(true);
            return pieceInspectPanel;
        }

        private UIShopItem FindItemView(int slotIndex)
        {
            if (_shop == null || slotIndex <= 0)
            {
                return null;
            }

            for (int index = 0; index < _shop.Offers.Count; index++)
            {
                if (_shop.Offers[index].SlotIndex == slotIndex
                    && index < _itemViews.Length)
                {
                    return _itemViews[index];
                }
            }

            return null;
        }

        private static ShopOfferSnapshot FindOffer(ShopSnapshot shop, int slotIndex)
        {
            if (shop == null || slotIndex <= 0)
            {
                return null;
            }

            foreach (ShopOfferSnapshot offer in shop.Offers)
            {
                if (offer.SlotIndex == slotIndex)
                {
                    return offer;
                }
            }

            return null;
        }

        private void RequestRefresh()
        {
            if (TryExecute(
                MatchCommand.RefreshShop(),
                () => _runtime.RefreshShop(_shop.PlayerId)))
            {
                ProtectTree.AudioManager.PlayCoinSpend();
            }
        }

        private void RequestUpgrade()
        {
            if (TryExecute(
                MatchCommand.UpgradeShop(),
                () => _runtime.UpgradeShop(_shop.PlayerId)))
            {
                ProtectTree.AudioManager.PlayCoinSpend();
            }
        }

        private void RequestToggleLock()
        {
            if (TryExecute(
                MatchCommand.ToggleShopLock(),
                () => _runtime.ToggleShopLock(_shop.PlayerId)))
            {
                ProtectTree.AudioManager.PlayButtonClick();
            }
        }

        private bool TryExecute(MatchCommand networkCommand, Action localCommand)
        {
            if (_runtime == null || _shop == null)
            {
                return false;
            }

            if (TrySendClientCommand(networkCommand))
            {
                return true;
            }

            try
            {
                localCommand();
                return true;
            }
            catch (Exception exception)
            {
                // 快照与点击之间可能跨过一个 Tick，最终仍以 Lua 的权威验证结果为准。
                Debug.LogWarning($"Shop command was rejected: {exception.Message}", this);
                return false;
            }
        }

        private bool TrySendClientCommand(MatchCommand command)
        {
            if (_lanMatch == null || !_lanMatch.IsActive || !_lanMatch.IsClient)
            {
                return false;
            }

            if (!_lanMatch.TrySendCommand(command))
            {
                Debug.LogWarning(
                    $"Shop command {command.Type} was not sent; waiting for LAN match transport.",
                    this);
            }

            // 客户端只提交意图，真正的金币、商店内容和棋子变化等待 Host 快照同步。
            return true;
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
                && (flow.Phase == "Battle"
                    || flow.Phase == "JointDefenseIntro"
                    || flow.Phase == "JointDefense"
                    || flow.Phase == "BossBattle");
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
