using System;
using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Core.Network;
using ProtectTree.Runtime.Lua;
using ProtectTree.Runtime.Network;

namespace ProtectTree.Runtime.Presentation
{
    public sealed class MatchSceneContext
    {
        private Action<int> _requestObservedPlayer;

        public LuaRuntime Runtime { get; private set; }

        public LanMatchRuntime LanMatch { get; private set; }

        public MatchFlowSnapshot Flow { get; private set; }

        public BoardSnapshot Board { get; private set; }

        public EnemyRosterSnapshot Enemies { get; private set; }

        public PieceRosterSnapshot Pieces { get; private set; }

        public PlayerRosterSnapshot Players { get; private set; }

        public ShopSnapshot Shop { get; private set; }

        public IReadOnlyList<MatchEvent> Events { get; private set; } =
            Array.Empty<MatchEvent>();

        public int LocalPlayerId { get; private set; }

        public int ObservedPlayerId { get; private set; }

        public int SelectedPieceInstanceId { get; private set; }

        public int SelectedShopSlotIndex { get; private set; }

        public ShopOfferSnapshot SelectedShopOfferPreview { get; private set; }

        public bool IsPieceInspectBlocked { get; private set; }

        public void Capture(
            LuaRuntime runtime,
            int localPlayerId,
            int observedPlayerId,
            Action<int> requestObservedPlayer)
        {
            if (Runtime != runtime)
            {
                // Static board data is rebuilt only when the Lua authority changes.
                Board = null;
            }

            Runtime = runtime;
            LanMatch = LanMatchRuntime.Instance;
            LocalPlayerId = localPlayerId;
            _requestObservedPlayer = requestObservedPlayer;
            ObservedPlayerId = observedPlayerId > 0
                ? observedPlayerId
                : localPlayerId;

            Board = Board ?? runtime.GetBoardSnapshot();
            MatchStateSnapshot remoteSnapshot = GetRemoteSnapshot(LanMatch);
            if (remoteSnapshot != null)
            {
                Flow = remoteSnapshot.Flow;
                Enemies = remoteSnapshot.Enemies;
                Pieces = remoteSnapshot.Pieces;
                Players = remoteSnapshot.Players;
                Shop = remoteSnapshot.Shop;
                Events = remoteSnapshot.Events;
            }
            else
            {
                Flow = runtime.GetMatchFlowSnapshot();
                Enemies = runtime.GetEnemyRosterSnapshot();
                Pieces = runtime.GetPieceRosterSnapshot();
                Players = runtime.GetPlayerRosterSnapshot();
                Shop = runtime.GetShopSnapshot(localPlayerId);
                if (LanMatch != null && LanMatch.IsActive && LanMatch.IsHost)
                {
                    Events = Array.Empty<MatchEvent>();
                }
                else
                {
                    // Events are drained once per frame and then shared by all views.
                    Events = runtime.DrainMatchEvents();
                }
            }

            if (!ContainsPlayer(Players, ObservedPlayerId))
            {
                ObservedPlayerId = localPlayerId;
            }

            SyncSelectedShopOfferPreview();
        }

        public void RequestObservePlayer(int playerId)
        {
            if (playerId <= 0 || !ContainsPlayer(Players, playerId))
            {
                return;
            }

            _requestObservedPlayer?.Invoke(playerId);
        }

        public void SelectPiece(int pieceInstanceId)
        {
            SelectedPieceInstanceId = pieceInstanceId;
            SelectedShopSlotIndex = 0;
            SelectedShopOfferPreview = null;
        }

        public void ClearPieceSelection()
        {
            SelectedPieceInstanceId = 0;
        }

        public void SelectShopOffer(int slotIndex)
        {
            SelectShopOffer(slotIndex, FindOffer(Shop, slotIndex));
        }

        public void SelectShopOffer(int slotIndex, ShopOfferSnapshot offer)
        {
            SelectedPieceInstanceId = 0;
            SelectedShopSlotIndex = slotIndex;
            SelectedShopOfferPreview = offer;
        }

        public void ClearShopOfferSelection()
        {
            SelectedShopSlotIndex = 0;
            SelectedShopOfferPreview = null;
        }

        public void SetPieceInspectBlocked(bool isBlocked)
        {
            IsPieceInspectBlocked = isBlocked;
        }

        public void Clear()
        {
            Runtime = null;
            LanMatch = null;
            Flow = null;
            Board = null;
            Enemies = null;
            Pieces = null;
            Players = null;
            Shop = null;
            Events = Array.Empty<MatchEvent>();
            LocalPlayerId = 0;
            ObservedPlayerId = 0;
            SelectedPieceInstanceId = 0;
            SelectedShopSlotIndex = 0;
            SelectedShopOfferPreview = null;
            IsPieceInspectBlocked = false;
            _requestObservedPlayer = null;
        }

        private void SyncSelectedShopOfferPreview()
        {
            if (SelectedShopSlotIndex <= 0)
            {
                SelectedShopOfferPreview = null;
                return;
            }

            ShopOfferSnapshot offer = FindOffer(Shop, SelectedShopSlotIndex);
            if (offer == null || offer.IsSold)
            {
                ClearShopOfferSelection();
                return;
            }

            SelectedShopOfferPreview = offer;
        }

        private static bool ContainsPlayer(
            PlayerRosterSnapshot players,
            int playerId)
        {
            if (players == null)
            {
                return false;
            }

            foreach (PlayerSnapshot player in players.Players)
            {
                if (player.PlayerId == playerId)
                {
                    return true;
                }
            }

            return false;
        }

        private static ShopOfferSnapshot FindOffer(
            ShopSnapshot shop,
            int slotIndex)
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

        private static MatchStateSnapshot GetRemoteSnapshot(
            LanMatchRuntime lanMatch)
        {
            if (lanMatch == null || !lanMatch.IsClient || !lanMatch.HasRemoteSnapshot)
            {
                return null;
            }

            return lanMatch.LatestRemoteSnapshot;
        }
    }
}
