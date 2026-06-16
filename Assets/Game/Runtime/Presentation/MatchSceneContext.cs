using System;
using System.Collections.Generic;
using ProtectTree.Core.Match;
using ProtectTree.Runtime.Lua;

namespace ProtectTree.Runtime.Presentation
{
    public sealed class MatchSceneContext
    {
        private Action<int> _requestObservedPlayer;

        public LuaRuntime Runtime { get; private set; }

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
            LocalPlayerId = localPlayerId;
            _requestObservedPlayer = requestObservedPlayer;
            ObservedPlayerId = observedPlayerId > 0
                ? observedPlayerId
                : localPlayerId;

            Flow = runtime.GetMatchFlowSnapshot();
            Board = Board ?? runtime.GetBoardSnapshot();
            Enemies = runtime.GetEnemyRosterSnapshot();
            Pieces = runtime.GetPieceRosterSnapshot();
            Players = runtime.GetPlayerRosterSnapshot();
            if (!ContainsPlayer(Players, ObservedPlayerId))
            {
                ObservedPlayerId = localPlayerId;
            }

            Shop = runtime.GetShopSnapshot(localPlayerId);
            // Events are drained once per frame and then shared by all views.
            Events = runtime.DrainMatchEvents();
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
        }

        public void SetPieceInspectBlocked(bool isBlocked)
        {
            IsPieceInspectBlocked = isBlocked;
        }

        public void Clear()
        {
            Runtime = null;
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
            IsPieceInspectBlocked = false;
            _requestObservedPlayer = null;
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
    }
}
