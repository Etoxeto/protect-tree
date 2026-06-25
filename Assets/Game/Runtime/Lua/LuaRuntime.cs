using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using ProtectTree.Core.Match;
using ProtectTree.Core.Simulation;
using XLua;
using ProtectTree.LuaContracts;

namespace ProtectTree.Runtime.Lua
{
    public sealed class LuaRuntime : IDisposable
    {
        private const string EntryModuleGlobal = "__protect_tree_entry_module";

        private readonly FixedStepClock _simulationClock = new FixedStepClock(
            SimulationSettings.StepSeconds,
            SimulationSettings.MaxStepsPerFrame);

        private readonly Action<double> _runSimulationStep;

        private LuaEnv _environment;

        private LuaTable _entryModule;

        private BuildWelcomeDelegate _buildWelcome;

        private LuaLifecycleAction _start;

        private LuaUpdateAction _update;

        private LuaLifecycleAction _shutdown;

        private LuaReloadModuleAction _reloadModule;

        public LuaRuntime(params LuaEnv.CustomLoader[] loaders)
        {
            _environment = new LuaEnv();
            _runSimulationStep = RunSimulationStep;

            if (loaders == null || loaders.Length == 0)
            {
                throw new ArgumentException("At least one Lua loader is required.", nameof(loaders));
            }

            foreach (LuaEnv.CustomLoader loader in loaders)
            {
                _environment.AddLoader(loader
                    ?? throw new ArgumentException("A Lua loader cannot be null.", nameof(loaders)));
            }
        }

        public bool IsStarted { get; private set; }

        public long SimulationTick => _simulationClock.Tick;

        public LuaEnv Environment => _environment
            ?? throw new ObjectDisposedException(nameof(LuaRuntime));

        public static LuaRuntime CreateForProjectScripts()
        {
            FileSystemLuaLoader loader = FileSystemLuaLoader.CreateForProjectScripts();
            return new LuaRuntime(loader.Load, PackagedResourcesLuaLoader.Load);
        }

        public void Start(string entryModule)
        {
            if (IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has already started.");
            }

            if (string.IsNullOrWhiteSpace(entryModule))
            {
                throw new ArgumentException("A Lua entry module is required.", nameof(entryModule));
            }

            LuaEnv environment = Environment;
            environment.Global.Set<string, string>(EntryModuleGlobal, entryModule);

            try
            {
                object[] results = environment.DoString(
                    $"return require({EntryModuleGlobal})",
                    "LuaRuntime.Start");

                _entryModule = results[0] as LuaTable
                    ?? throw new InvalidOperationException("Entry module must return a Lua table.");

                _buildWelcome = _entryModule.Get<BuildWelcomeDelegate>("build_welcome")
                    ?? throw new InvalidOperationException("Lua function not found: build_welcome");
                _start = _entryModule.Get<LuaLifecycleAction>("start")
                    ?? throw new InvalidOperationException("Lua function not found: start");
                _update = _entryModule.Get<LuaUpdateAction>("update")
                    ?? throw new InvalidOperationException("Lua function not found: update");
                _shutdown = _entryModule.Get<LuaLifecycleAction>("shutdown")
                    ?? throw new InvalidOperationException("Lua function not found: shutdown");
                _reloadModule = _entryModule.Get<LuaReloadModuleAction>("reload_module")
                    ?? throw new InvalidOperationException("Lua function not found: reload_module");

                _simulationClock.Reset();
                _start();
                IsStarted = true;
            }
            finally
            {
                environment.Global.Set<string, object>(EntryModuleGlobal, null);
            }
        }

        public object[] CallEntryFunction(string functionName, params object[] args)
        {
            LuaFunction function = _entryModule.Get<LuaFunction>(functionName);

            if (function == null)
            {
                throw new InvalidOperationException($"Lua function not found: {functionName}");
            }

            try
            {
                return function.Call(args);
            }
            finally
            {
                function.Dispose();
            }
        }

        public string BuildWelcome(string playerName)
        {
            if (!IsStarted || _buildWelcome == null)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            return _buildWelcome(playerName);
        }

        public void StartLocalMultiplayer(int playerCount)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            if (playerCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playerCount),
                    "Player count must be positive.");
            }

            CallEntryFunction("start_local_multiplayer", playerCount);
            _simulationClock.Reset();
        }

        public int GrantPiece(int playerId, string pieceId)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results = CallEntryFunction("grant_piece", playerId, pieceId);
            if (results.Length != 1)
            {
                throw new InvalidOperationException(
                    "Lua function grant_piece must return one piece instance ID.");
            }

            return Convert.ToInt32(results[0]);
        }

        public int GrantOverflowPiece(int playerId, string pieceId)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results =
                CallEntryFunction("grant_overflow_piece", playerId, pieceId);
            if (results.Length != 1)
            {
                throw new InvalidOperationException(
                    "Lua function grant_overflow_piece must return one piece instance ID.");
            }

            return Convert.ToInt32(results[0]);
        }

        public bool DebugEnterBossPreparation()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results = CallEntryFunction("debug_enter_boss_preparation");
            if (results.Length != 1)
            {
                throw new InvalidOperationException(
                    "Lua function debug_enter_boss_preparation must return one state.");
            }

            return Convert.ToBoolean(results[0]);
        }

        public void DeployPiece(int playerId, int pieceInstanceId, int cellId)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            CallEntryFunction("deploy_piece", playerId, pieceInstanceId, cellId);
        }

        public void BenchPiece(int playerId, int pieceInstanceId)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            CallEntryFunction("bench_piece", playerId, pieceInstanceId);
        }

        public void PlacePiece(
            int playerId,
            int pieceInstanceId,
            int cellId,
            string facing)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            CallEntryFunction(
                "place_piece",
                playerId,
                pieceInstanceId,
                cellId,
                facing);
        }

        public int SellPiece(int playerId, int pieceInstanceId)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results = CallEntryFunction("sell_piece", playerId, pieceInstanceId);
            if (results.Length != 1)
            {
                throw new InvalidOperationException(
                    "Lua function sell_piece must return one sell value.");
            }

            return Convert.ToInt32(results[0]);
        }

        public void SetPieceFacing(int playerId, int pieceInstanceId, string facing)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            CallEntryFunction("set_piece_facing", playerId, pieceInstanceId, facing);
        }

        public void RefreshShop(int playerId)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            CallEntryFunction("refresh_shop", playerId);
        }

        public bool ToggleShopLock(int playerId)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results = CallEntryFunction("toggle_shop_lock", playerId);
            if (results.Length != 1)
            {
                throw new InvalidOperationException(
                    "Lua function toggle_shop_lock must return one lock state.");
            }

            return Convert.ToBoolean(results[0]);
        }

        public void UpgradeShop(int playerId)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            CallEntryFunction("upgrade_shop", playerId);
        }

        public int PurchaseShopOffer(int playerId, int slotIndex)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results = CallEntryFunction("purchase_shop_offer", playerId, slotIndex);
            if (results.Length != 1)
            {
                throw new InvalidOperationException(
                    "Lua function purchase_shop_offer must return one piece instance ID.");
            }

            return Convert.ToInt32(results[0]);
        }

        public void SetPlayerReady(int playerId, bool isReady)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            CallEntryFunction("set_player_ready", playerId, isReady);
        }

        public MatchFlowSnapshot GetMatchFlowSnapshot()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results = CallEntryFunction("get_match_snapshot");
            if (results.Length != 1 || !(results[0] is LuaTable snapshotTable))
            {
                throw new InvalidOperationException(
                    "Lua function get_match_snapshot must return one table.");
            }

            try
            {
                return new MatchFlowSnapshot(
                    snapshotTable.Get<string>("phase"),
                    snapshotTable.Get<int>("wave"),
                    snapshotTable.Get<double>("remaining_seconds"),
                    snapshotTable.Get<bool>("is_finished"),
                    snapshotTable.Get<string>("result"));
            }
            finally
            {
                snapshotTable.Dispose();
            }
        }

        public BoardSnapshot GetBoardSnapshot()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results = CallEntryFunction("get_board_snapshot");
            if (results.Length != 1 || !(results[0] is LuaTable snapshotTable))
            {
                throw new InvalidOperationException(
                    "Lua function get_board_snapshot must return one table.");
            }

            try
            {
                LuaTable cellsTable = snapshotTable.Get<LuaTable>("cells");
                try
                {
                    List<BoardCellSnapshot> cells =
                        new List<BoardCellSnapshot>(cellsTable.Length);

                    for (int index = 1; index <= cellsTable.Length; index++)
                    {
                        LuaTable cellTable = cellsTable.Get<int, LuaTable>(index);
                        if (cellTable == null)
                        {
                            throw new InvalidOperationException(
                                $"Board cell snapshot at Lua array index {index} must be a table.");
                        }

                        try
                        {
                            LuaTable routePositionsTable =
                                cellTable.Get<LuaTable>("route_positions");
                            List<BoardCellRoutePositionSnapshot> routePositions =
                                new List<BoardCellRoutePositionSnapshot>(
                                    routePositionsTable.Length);

                            try
                            {
                                for (int routePositionIndex = 1;
                                    routePositionIndex <= routePositionsTable.Length;
                                    routePositionIndex++)
                                {
                                    LuaTable routePositionTable =
                                        routePositionsTable.Get<int, LuaTable>(
                                            routePositionIndex);
                                    if (routePositionTable == null)
                                    {
                                        throw new InvalidOperationException(
                                            $"Board cell route position at Lua array index {routePositionIndex} must be a table.");
                                    }

                                    try
                                    {
                                        routePositions.Add(
                                            new BoardCellRoutePositionSnapshot(
                                                routePositionTable.Get<int>("route_id"),
                                                routePositionTable.Get<double>(
                                                    "path_progress")));
                                    }
                                    finally
                                    {
                                        routePositionTable.Dispose();
                                    }
                                }
                            }
                            finally
                            {
                                routePositionsTable.Dispose();
                            }

                            cells.Add(new BoardCellSnapshot(
                                cellTable.Get<int>("cell_id"),
                                cellTable.Get<string>("terrain"),
                                cellTable.Get<string>("zone"),
                                cellTable.Get<int>("grid_x"),
                                cellTable.Get<int>("grid_y"),
                                cellTable.Get<int>("visual_x"),
                                cellTable.Get<int>("visual_y"),
                                cellTable.Get<int>("visual_height"),
                                cellTable.Get<string>("visual_key"),
                                cellTable.Get<bool>("allows_battle_deployment"),
                                cellTable.Get<bool>("accepts_reserve_piece"),
                                cellTable.Get<bool>("accepts_overflow_piece"),
                                cellTable.Get<bool>("auto_sell_on_battle_start"),
                                cellTable.Get<bool>("allows_enemy_route"),
                                routePositions));
                        }
                        finally
                        {
                            cellTable.Dispose();
                        }
                    }

                    LuaTable routesTable = snapshotTable.Get<LuaTable>("routes");
                    try
                    {
                        List<BoardRouteSnapshot> routes =
                            new List<BoardRouteSnapshot>(routesTable.Length);

                        for (int routeIndex = 1;
                            routeIndex <= routesTable.Length;
                            routeIndex++)
                        {
                            LuaTable routeTable =
                                routesTable.Get<int, LuaTable>(routeIndex);
                            if (routeTable == null)
                            {
                                throw new InvalidOperationException(
                                    $"Board route snapshot at Lua array index {routeIndex} must be a table.");
                            }

                            try
                            {
                                LuaTable samplesTable =
                                    routeTable.Get<LuaTable>("samples");
                                try
                                {
                                    List<BoardRouteSampleSnapshot> samples =
                                        new List<BoardRouteSampleSnapshot>(
                                            samplesTable.Length);

                                    for (int sampleIndex = 1;
                                        sampleIndex <= samplesTable.Length;
                                        sampleIndex++)
                                    {
                                        LuaTable sampleTable =
                                            samplesTable.Get<int, LuaTable>(sampleIndex);
                                        if (sampleTable == null)
                                        {
                                            throw new InvalidOperationException(
                                                $"Board route sample at Lua array index {sampleIndex} must be a table.");
                                        }

                                        try
                                        {
                                            samples.Add(new BoardRouteSampleSnapshot(
                                                sampleTable.Get<int>("cell_id"),
                                                sampleTable.Get<int>("grid_x"),
                                                sampleTable.Get<int>("grid_y"),
                                                sampleTable.Get<double>("path_progress")));
                                        }
                                        finally
                                        {
                                            sampleTable.Dispose();
                                        }
                                    }

                                    routes.Add(new BoardRouteSnapshot(
                                        routeTable.Get<int>("route_id"),
                                        routeTable.Get<double>("start_progress"),
                                        routeTable.Get<double>("endpoint_progress"),
                                        samples));
                                }
                                finally
                                {
                                    samplesTable.Dispose();
                                }
                            }
                            finally
                            {
                                routeTable.Dispose();
                            }
                        }

                        return new BoardSnapshot(
                            snapshotTable.Get<string>("board_id"),
                            snapshotTable.Get<string>("board_kind"),
                            snapshotTable.Get<int>("width"),
                            snapshotTable.Get<int>("height"),
                            snapshotTable.Get<int>("origin_grid_x"),
                            snapshotTable.Get<int>("origin_grid_y"),
                            snapshotTable.Get<int>("reserve_capacity"),
                            snapshotTable.Get<int>("temporary_reserve_capacity"),
                            cells,
                            routes);
                    }
                    finally
                    {
                        routesTable.Dispose();
                    }
                }
                finally
                {
                    cellsTable.Dispose();
                }
            }
            finally
            {
                snapshotTable.Dispose();
            }
        }

        public WaveSpawnerSnapshot GetWaveSpawnerSnapshot()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results = CallEntryFunction("get_wave_spawner_snapshot");
            if (results.Length != 1 || !(results[0] is LuaTable snapshotTable))
            {
                throw new InvalidOperationException(
                    "Lua function get_wave_spawner_snapshot must return one table.");
            }

            try
            {
                return new WaveSpawnerSnapshot(
                    snapshotTable.Get<bool>("is_active"),
                    snapshotTable.Get<int>("wave"),
                    snapshotTable.Get<string>("enemy_id"),
                    snapshotTable.Get<int>("route_id"),
                    snapshotTable.Get<int>("remaining_count"),
                    snapshotTable.Get<int>("next_spawn_index"),
                    snapshotTable.Get<double>("seconds_until_next_spawn"));
            }
            finally
            {
                snapshotTable.Dispose();
            }
        }

        public EnemyRosterSnapshot GetEnemyRosterSnapshot()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results = CallEntryFunction("get_enemy_roster_snapshot");
            if (results.Length != 1 || !(results[0] is LuaTable snapshotTable))
            {
                throw new InvalidOperationException(
                    "Lua function get_enemy_roster_snapshot must return one table.");
            }

            try
            {
                LuaTable enemiesTable = snapshotTable.Get<LuaTable>("enemies");
                try
                {
                    List<EnemySnapshot> enemies =
                        new List<EnemySnapshot>(enemiesTable.Length);

                    for (int index = 1; index <= enemiesTable.Length; index++)
                    {
                        LuaTable enemyTable = enemiesTable.Get<int, LuaTable>(index);
                        if (enemyTable == null)
                        {
                            throw new InvalidOperationException(
                                $"Enemy snapshot at Lua array index {index} must be a table.");
                        }

                        try
                        {
                            enemies.Add(new EnemySnapshot(
                                enemyTable.Get<int>("instance_id"),
                                enemyTable.Get<string>("enemy_id"),
                                enemyTable.Get<int>("wave"),
                                enemyTable.Get<int>("spawn_index"),
                                enemyTable.Get<int>("target_player_id"),
                                enemyTable.Get<int>("health"),
                                enemyTable.Get<int>("max_health"),
                                enemyTable.Get<string>("status"),
                                enemyTable.Get<int>("attack_damage"),
                                enemyTable.Get<double>("attack_interval_seconds"),
                                enemyTable.Get<string>("attack_type"),
                                GetOptionalString(enemyTable, "attack_sfx_id"),
                                enemyTable.Get<bool>("is_boss"),
                                GetOptionalBool(enemyTable, "is_enraged") ?? false,
                                enemyTable.Get<int>("route_id"),
                                enemyTable.Get<double>("path_speed"),
                                enemyTable.Get<double>("path_progress"),
                                GetOptionalInt(
                                    enemyTable,
                                    "blocked_by_piece_instance_id")));
                        }
                        finally
                        {
                            enemyTable.Dispose();
                        }
                    }

                    return new EnemyRosterSnapshot(
                        snapshotTable.Get<int>("alive_count"),
                        enemies);
                }
                finally
                {
                    enemiesTable.Dispose();
                }
            }
            finally
            {
                snapshotTable.Dispose();
            }
        }

        public PieceRosterSnapshot GetPieceRosterSnapshot()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results = CallEntryFunction("get_piece_roster_snapshot");
            if (results.Length != 1 || !(results[0] is LuaTable snapshotTable))
            {
                throw new InvalidOperationException(
                    "Lua function get_piece_roster_snapshot must return one table.");
            }

            try
            {
                LuaTable piecesTable = snapshotTable.Get<LuaTable>("pieces");
                try
                {
                    List<PieceSnapshot> pieces =
                        new List<PieceSnapshot>(piecesTable.Length);

                    for (int index = 1; index <= piecesTable.Length; index++)
                    {
                        LuaTable pieceTable = piecesTable.Get<int, LuaTable>(index);
                        if (pieceTable == null)
                        {
                            throw new InvalidOperationException(
                                $"Piece snapshot at Lua array index {index} must be a table.");
                        }

                        try
                        {
                            LuaTable blockedEnemiesTable =
                                pieceTable.Get<LuaTable>("blocked_enemy_instance_ids");
                            List<int> blockedEnemyInstanceIds =
                                new List<int>(blockedEnemiesTable.Length);
                            LuaTable deployableTerrainsTable =
                                pieceTable.Get<LuaTable>("deployable_terrains");
                            List<string> deployableTerrains =
                                new List<string>(deployableTerrainsTable.Length);
                            LuaTable synergiesTable =
                                pieceTable.Get<LuaTable>("synergies");
                            List<ShopSynergySnapshot> synergies =
                                new List<ShopSynergySnapshot>(synergiesTable.Length);
                            LuaTable attackRangeTable =
                                pieceTable.Get<LuaTable>("attack_range");
                            List<PieceAttackRangeOffsetSnapshot> attackRange =
                                new List<PieceAttackRangeOffsetSnapshot>(
                                    attackRangeTable.Length);

                            try
                            {
                                for (int blockedIndex = 1;
                                    blockedIndex <= blockedEnemiesTable.Length;
                                    blockedIndex++)
                                {
                                    blockedEnemyInstanceIds.Add(
                                        blockedEnemiesTable.Get<int, int>(blockedIndex));
                                }

                                for (int terrainIndex = 1;
                                    terrainIndex <= deployableTerrainsTable.Length;
                                    terrainIndex++)
                                {
                                    deployableTerrains.Add(
                                        deployableTerrainsTable.Get<int, string>(terrainIndex));
                                }

                                for (int synergyIndex = 1;
                                     synergyIndex <= synergiesTable.Length;
                                     synergyIndex++)
                                {
                                    LuaTable synergyTable =
                                        synergiesTable.Get<int, LuaTable>(synergyIndex);
                                    if (synergyTable == null)
                                    {
                                        throw new InvalidOperationException(
                                            $"Piece synergy at Lua array index {synergyIndex} must be a table.");
                                    }

                                    try
                                    {
                                        synergies.Add(new ShopSynergySnapshot(
                                            synergyTable.Get<string>("synergy_id"),
                                            synergyTable.Get<string>("display_name")));
                                    }
                                    finally
                                    {
                                        synergyTable.Dispose();
                                    }
                                }

                                for (int offsetIndex = 1;
                                     offsetIndex <= attackRangeTable.Length;
                                     offsetIndex++)
                                {
                                    LuaTable offsetTable =
                                        attackRangeTable.Get<int, LuaTable>(offsetIndex);
                                    if (offsetTable == null)
                                    {
                                        throw new InvalidOperationException(
                                            $"Piece attack range offset at Lua array index {offsetIndex} must be a table.");
                                    }

                                    try
                                    {
                                        attackRange.Add(
                                            new PieceAttackRangeOffsetSnapshot(
                                                offsetTable.Get<int>("forward"),
                                                offsetTable.Get<int>("right")));
                                    }
                                    finally
                                    {
                                        offsetTable.Dispose();
                                    }
                                }
                            }
                            finally
                            {
                                attackRangeTable.Dispose();
                                synergiesTable.Dispose();
                                deployableTerrainsTable.Dispose();
                                blockedEnemiesTable.Dispose();
                            }

                            pieces.Add(new PieceSnapshot(
                                pieceTable.Get<int>("instance_id"),
                                pieceTable.Get<string>("piece_id"),
                                pieceTable.Get<int>("owner_player_id"),
                                pieceTable.Get<int>("level"),
                                pieceTable.Get<string>("location"),
                                GetOptionalInt(pieceTable, "cell_id"),
                                pieceTable.Get<string>("terrain"),
                                pieceTable.Get<string>("facing"),
                                pieceTable.Get<int>("health"),
                                pieceTable.Get<int>("max_health"),
                                pieceTable.Get<string>("status"),
                                pieceTable.Get<int>("max_block_count"),
                                blockedEnemyInstanceIds,
                                pieceTable.Get<double>("recovery_seconds_remaining"),
                                pieceTable.Get<int>("base_damage"),
                                pieceTable.Get<int>("damage"),
                                pieceTable.Get<double>("attack_interval_seconds"),
                                pieceTable.Get<int>("sell_value"),
                                deployableTerrains,
                                pieceTable.Get<string>("display_name"),
                                pieceTable.Get<string>("portrait"),
                                pieceTable.Get<string>("class_id"),
                                GetOptionalString(pieceTable, "attack_sfx_id"),
                                pieceTable.Get<int>("rarity"),
                                synergies,
                                attackRange,
                                GetOptionalString(
                                    pieceTable,
                                    "feature_description")));
                        }
                        finally
                        {
                            pieceTable.Dispose();
                        }
                    }

                    LuaTable playersTable = snapshotTable.Get<LuaTable>("players");
                    try
                    {
                        List<PieceCapacitySnapshot> players =
                            new List<PieceCapacitySnapshot>(playersTable.Length);

                        for (int index = 1; index <= playersTable.Length; index++)
                        {
                            LuaTable playerTable = playersTable.Get<int, LuaTable>(index);
                            if (playerTable == null)
                            {
                                throw new InvalidOperationException(
                                    $"Piece capacity at Lua array index {index} must be a table.");
                            }

                            try
                            {
                                players.Add(new PieceCapacitySnapshot(
                                    playerTable.Get<int>("player_id"),
                                    playerTable.Get<int>("bench_count"),
                                    playerTable.Get<int>("bench_capacity"),
                                    playerTable.Get<int>("board_count"),
                                    playerTable.Get<int>("deployment_limit"),
                                    playerTable.Get<int>("temporary_bench_count"),
                                    playerTable.Get<int>("temporary_bench_capacity")));
                            }
                            finally
                            {
                                playerTable.Dispose();
                            }
                        }

                        LuaTable activeSynergiesTable =
                            snapshotTable.Get<LuaTable>("active_synergies");
                        try
                        {
                            List<ActiveSynergySnapshot> activeSynergies =
                                new List<ActiveSynergySnapshot>(
                                    activeSynergiesTable.Length);

                            for (int index = 1;
                                 index <= activeSynergiesTable.Length;
                                 index++)
                            {
                                LuaTable synergyTable =
                                    activeSynergiesTable.Get<int, LuaTable>(index);
                                if (synergyTable == null)
                                {
                                    throw new InvalidOperationException(
                                        $"Active synergy at Lua array index {index} must be a table.");
                                }

                                try
                                {
                                    activeSynergies.Add(new ActiveSynergySnapshot(
                                        synergyTable.Get<int>("player_id"),
                                        synergyTable.Get<string>("synergy_id"),
                                        synergyTable.Get<int>("level"),
                                        GetOptionalInt(synergyTable, "layer_count")
                                            ?? 0,
                                        synergyTable.Get<int>("unique_piece_count"),
                                        synergyTable.Get<int>("required_unique_pieces"),
                                        synergyTable.Get<int>("damage_bonus"),
                                        GetOptionalString(
                                            synergyTable,
                                            "effect_description")));
                                }
                                finally
                                {
                                    synergyTable.Dispose();
                                }
                            }

                            LuaTable synergyProgressesTable =
                                snapshotTable.Get<LuaTable>("synergy_progresses");
                            try
                            {
                                List<SynergyProgressSnapshot> synergyProgresses =
                                    new List<SynergyProgressSnapshot>(
                                        synergyProgressesTable.Length);

                                for (int index = 1;
                                     index <= synergyProgressesTable.Length;
                                     index++)
                                {
                                    LuaTable progressTable =
                                        synergyProgressesTable.Get<int, LuaTable>(
                                            index);
                                    if (progressTable == null)
                                    {
                                        throw new InvalidOperationException(
                                            $"Synergy progress at Lua array index {index} must be a table.");
                                    }

                                    try
                                    {
                                        synergyProgresses.Add(
                                            new SynergyProgressSnapshot(
                                                progressTable.Get<int>("player_id"),
                                                progressTable.Get<string>(
                                                    "synergy_id"),
                                                progressTable.Get<string>(
                                                    "display_name"),
                                                progressTable.Get<int>("level"),
                                                GetOptionalInt(
                                                    progressTable,
                                                    "layer_count") ?? 0,
                                                progressTable.Get<int>(
                                                    "unique_piece_count"),
                                                progressTable.Get<int>(
                                                    "required_unique_pieces"),
                                                progressTable.Get<int>(
                                                    "damage_bonus"),
                                                GetOptionalString(
                                                    progressTable,
                                                    "effect_description")));
                                    }
                                    finally
                                    {
                                        progressTable.Dispose();
                                    }
                                }

                                return new PieceRosterSnapshot(
                                    pieces,
                                    players,
                                    activeSynergies,
                                    synergyProgresses);
                            }
                            finally
                            {
                                synergyProgressesTable.Dispose();
                            }

                        }
                        finally
                        {
                            activeSynergiesTable.Dispose();
                        }
                    }
                    finally
                    {
                        playersTable.Dispose();
                    }
                }
                finally
                {
                    piecesTable.Dispose();
                }
            }
            finally
            {
                snapshotTable.Dispose();
            }
        }

        public PlayerRosterSnapshot GetPlayerRosterSnapshot()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results = CallEntryFunction("get_player_roster_snapshot");
            if (results.Length != 1 || !(results[0] is LuaTable snapshotTable))
            {
                throw new InvalidOperationException(
                    "Lua function get_player_roster_snapshot must return one table.");
            }

            try
            {
                LuaTable playersTable = snapshotTable.Get<LuaTable>("players");
                try
                {
                    List<PlayerSnapshot> players =
                        new List<PlayerSnapshot>(playersTable.Length);

                    for (int index = 1; index <= playersTable.Length; index++)
                    {
                        LuaTable playerTable = playersTable.Get<int, LuaTable>(index);
                        if (playerTable == null)
                        {
                            throw new InvalidOperationException(
                                $"Player snapshot at Lua array index {index} must be a table.");
                        }

                        try
                        {
                            players.Add(new PlayerSnapshot(
                                playerTable.Get<int>("player_id"),
                                playerTable.Get<int>("health"),
                                playerTable.Get<int>("max_health"),
                                playerTable.Get<int>("gold"),
                                playerTable.Get<string>("status"),
                                playerTable.Get<bool>("is_ready")));
                        }
                        finally
                        {
                            playerTable.Dispose();
                        }
                    }

                    return new PlayerRosterSnapshot(
                        snapshotTable.Get<int>("alive_count"),
                        players);
                }
                finally
                {
                    playersTable.Dispose();
                }
            }
            finally
            {
                snapshotTable.Dispose();
            }
        }

        public ShopSnapshot GetShopSnapshot(int playerId)
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results = CallEntryFunction("get_shop_snapshot", playerId);
            if (results.Length != 1 || !(results[0] is LuaTable snapshotTable))
            {
                throw new InvalidOperationException(
                    "Lua function get_shop_snapshot must return one table.");
            }

            try
            {
                LuaTable offersTable = snapshotTable.Get<LuaTable>("offers");
                try
                {
                    List<ShopOfferSnapshot> offers =
                        new List<ShopOfferSnapshot>(offersTable.Length);

                    for (int index = 1; index <= offersTable.Length; index++)
                    {
                        LuaTable offerTable = offersTable.Get<int, LuaTable>(index);
                        if (offerTable == null)
                        {
                            throw new InvalidOperationException(
                                $"Shop offer at Lua array index {index} must be a table.");
                        }

                        try
                        {
                            LuaTable synergiesTable = offerTable.Get<LuaTable>("synergies");
                            List<ShopSynergySnapshot> synergies =
                                new List<ShopSynergySnapshot>(synergiesTable.Length);
                            try
                            {
                                for (int synergyIndex = 1;
                                     synergyIndex <= synergiesTable.Length;
                                     synergyIndex++)
                                {
                                    LuaTable synergyTable =
                                        synergiesTable.Get<int, LuaTable>(synergyIndex);
                                    if (synergyTable == null)
                                    {
                                        throw new InvalidOperationException(
                                            $"Shop synergy at Lua array index {synergyIndex} must be a table.");
                                    }

                                    try
                                    {
                                        synergies.Add(new ShopSynergySnapshot(
                                            synergyTable.Get<string>("synergy_id"),
                                            synergyTable.Get<string>("display_name")));
                                    }
                                    finally
                                    {
                                        synergyTable.Dispose();
                                    }
                                }
                            }
                            finally
                            {
                                synergiesTable.Dispose();
                            }

                            offers.Add(new ShopOfferSnapshot(
                                offerTable.Get<int>("slot_index"),
                                offerTable.Get<string>("piece_id"),
                                offerTable.Get<string>("display_name"),
                                offerTable.Get<string>("portrait"),
                                offerTable.Get<string>("class_id"),
                                offerTable.Get<int>("rarity"),
                                offerTable.Get<int>("cost"),
                                synergies,
                                offerTable.Get<bool>("is_sold"),
                                GetOptionalInt(offerTable, "max_health") ?? 0,
                                GetOptionalInt(offerTable, "max_block_count")
                                    ?? 0,
                                GetOptionalInt(offerTable, "damage") ?? 0,
                                GetOptionalDouble(
                                    offerTable,
                                    "attack_interval_seconds") ?? 0,
                                GetOptionalString(
                                    offerTable,
                                    "feature_description")));
                        }
                        finally
                        {
                            offerTable.Dispose();
                        }
                    }

                    LuaTable rarityWeightsTable =
                        snapshotTable.Get<LuaTable>("rarity_weights");
                    try
                    {
                        List<ShopRarityWeightSnapshot> rarityWeights =
                            new List<ShopRarityWeightSnapshot>(rarityWeightsTable.Length);

                        for (int index = 1; index <= rarityWeightsTable.Length; index++)
                        {
                            LuaTable weightTable =
                                rarityWeightsTable.Get<int, LuaTable>(index);
                            if (weightTable == null)
                            {
                                throw new InvalidOperationException(
                                    $"Shop rarity weight at Lua array index {index} must be a table.");
                            }

                            try
                            {
                                rarityWeights.Add(new ShopRarityWeightSnapshot(
                                    weightTable.Get<int>("rarity"),
                                    weightTable.Get<int>("weight")));
                            }
                            finally
                            {
                                weightTable.Dispose();
                            }
                        }

                        return new ShopSnapshot(
                            snapshotTable.Get<int>("player_id"),
                            snapshotTable.Get<int>("level"),
                            snapshotTable.Get<int>("max_level"),
                            snapshotTable.Get<bool>("can_upgrade"),
                            snapshotTable.Get<int>("upgrade_cost"),
                            snapshotTable.Get<int>("refresh_cost"),
                            snapshotTable.Get<bool>("is_locked"),
                            rarityWeights,
                            offers);
                    }
                    finally
                    {
                        rarityWeightsTable.Dispose();
                    }
                }
                finally
                {
                    offersTable.Dispose();
                }
            }
            finally
            {
                snapshotTable.Dispose();
            }
        }

        public IReadOnlyList<MatchEvent> DrainMatchEvents()
        {
            if (!IsStarted)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            object[] results = CallEntryFunction("drain_match_events");
            if (results.Length != 1 || !(results[0] is LuaTable eventsTable))
            {
                throw new InvalidOperationException(
                    "Lua function drain_match_events must return one table.");
            }

            try
            {
                List<MatchEvent> events = new List<MatchEvent>(eventsTable.Length);

                for (int index = 1; index <= eventsTable.Length; index++)
                {
                    LuaTable eventTable = eventsTable.Get<int, LuaTable>(index);
                    if (eventTable == null)
                    {
                        throw new InvalidOperationException(
                            $"Match event at Lua array index {index} must be a table.");
                    }

                    try
                    {
                        int? pieceInstanceId =
                            GetOptionalInt(eventTable, "piece_instance_id")
                            ?? GetOptionalInt(
                                eventTable,
                                "survivor_piece_instance_id");

                        events.Add(new MatchEvent(
                            eventTable.Get<string>("type"),
                            GetOptionalInt(eventTable, "wave"),
                            GetOptionalString(eventTable, "phase"),
                            GetOptionalString(eventTable, "enemy_id"),
                            GetOptionalInt(eventTable, "spawn_index"),
                            GetOptionalInt(eventTable, "enemy_instance_id"),
                            GetOptionalString(eventTable, "result"),
                            pieceInstanceId,
                            GetOptionalInt(eventTable, "source_piece_instance_id"),
                            GetOptionalInt(eventTable, "source_enemy_instance_id"),
                            GetOptionalInt(eventTable, "player_id"),
                            GetOptionalInt(eventTable, "target_player_id"),
                            GetOptionalInt(eventTable, "defender_player_id"),
                            GetOptionalInt(eventTable, "leak_owner_player_id"),
                            GetOptionalInt(eventTable, "initial_leak_count"),
                            GetOptionalInt(eventTable, "rescued_count"),
                            GetOptionalInt(eventTable, "final_leak_count"),
                            GetOptionalInt(eventTable, "damage"),
                            GetOptionalInt(eventTable, "leak_count"),
                            GetOptionalInt(eventTable, "health"),
                            GetOptionalInt(eventTable, "leaking_player_count"),
                            GetOptionalInt(eventTable, "transferred_enemy_count"),
                            GetOptionalInt(eventTable, "previous_target_player_id"),
                            GetOptionalInt(eventTable, "max_health"),
                            GetOptionalBool(eventTable, "is_boss"),
                            GetOptionalString(eventTable, "projectile_id"),
                            GetOptionalDouble(eventTable, "cast_lock_seconds")));
                    }
                    finally
                    {
                        eventTable.Dispose();
                    }
                }

                return events;
            }
            finally
            {
                eventsTable.Dispose();
            }
        }

        public void Tick(float deltaTime)
        {
            if (!IsStarted || _update == null)
            {
                return;
            }

            Environment.Tick();
            _simulationClock.Advance(deltaTime, _runSimulationStep);
        }

        public void ReloadModule(string moduleName)
        {
            if (!IsStarted || _reloadModule == null)
            {
                throw new InvalidOperationException("The Lua runtime has not started.");
            }

            if (string.IsNullOrWhiteSpace(moduleName))
            {
                throw new ArgumentException("A Lua module name is required.", nameof(moduleName));
            }

            _reloadModule(moduleName);
        }

        public void Dispose()
        {
            if (_environment == null)
            {
                return;
            }

            try
            {
                ShutdownAndReleaseEntryModule();
            }
            finally
            {
                DisposeEnvironment();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ShutdownAndReleaseEntryModule()
        {
            try
            {
                if (IsStarted)
                {
                    _shutdown?.Invoke();
                }
            }
            finally
            {
                IsStarted = false;
                ReleaseCallback(_buildWelcome);
                ReleaseCallback(_start);
                ReleaseCallback(_update);
                ReleaseCallback(_shutdown);
                ReleaseCallback(_reloadModule);
                _buildWelcome = null;
                _start = null;
                _update = null;
                _shutdown = null;
                _reloadModule = null;
                _entryModule?.Dispose();
                _entryModule = null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DisposeEnvironment()
        {
            LuaEnv environment = _environment;
            _environment = null;
            environment.Dispose();
        }

        private static void ReleaseCallback(Delegate callback)
        {
            if (callback?.Target is LuaBase luaCallback)
            {
                luaCallback.Dispose();
            }
        }

        private void RunSimulationStep(double stepSeconds)
        {
            _update((float)stepSeconds);
        }

        private static string GetOptionalString(LuaTable table, string key)
        {
            return table.ContainsKey(key) ? table.Get<string>(key) : null;
        }

        private static int? GetOptionalInt(LuaTable table, string key)
        {
            return table.ContainsKey(key) ? table.Get<int>(key) : (int?)null;
        }

        private static double? GetOptionalDouble(LuaTable table, string key)
        {
            return table.ContainsKey(key) ? table.Get<double>(key) : (double?)null;
        }

        private static bool? GetOptionalBool(LuaTable table, string key)
        {
            return table.ContainsKey(key) ? table.Get<bool>(key) : (bool?)null;
        }
    }
}
