using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using ProtectTree.Core.Match;
using ProtectTree.Core.Simulation;
using ProtectTree.Runtime.Lua;
using XLua;

namespace ProtectTree.Runtime.Tests
{
    public sealed class LuaRuntimeTests
    {
        // 准备阶段时长属于玩法配置；测试中的时间假设集中在这里，避免配置调整后产生大面积连锁失败。
        private const double PreparationSeconds = 30d;
        private const int PreparationStepCount = 300;
        private const int SingleWaveSettlementStepBudget = 250;
        private const int SingleWavePostSpawnCompletionStepBudget = 250;
        private const int ThreeWaveDefeatStepBudget = 2000;
        private const int ReadyBattleSettlementStepBudget = 800;

        [Test]
        public void Start_LoadsBootstrapModule()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                Assert.That(runtime.IsStarted, Is.True);
                Assert.That(runtime.SimulationTick, Is.EqualTo(0));

                runtime.Tick(0.04f);
                Assert.That(runtime.SimulationTick, Is.EqualTo(0));

                runtime.Tick(0.06f);
                Assert.That(runtime.SimulationTick, Is.EqualTo(1));

                runtime.ReloadModule("Bootstrap.LifecycleDemo");
                runtime.Tick(0.1f);
                Assert.That(runtime.SimulationTick, Is.EqualTo(2));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void BoardSnapshot_ExposesAuthorityLayoutAndRoutes()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");

                BoardSnapshot snapshot = runtime.GetBoardSnapshot();
                Assert.That(snapshot.BoardId, Is.EqualTo("PersonalDefense"));
                Assert.That(snapshot.BoardKind, Is.EqualTo("Personal"));
                Assert.That(snapshot.Width, Is.EqualTo(11));
                Assert.That(snapshot.Height, Is.EqualTo(7));
                Assert.That(snapshot.OriginGridX, Is.Zero);
                Assert.That(snapshot.OriginGridY, Is.Zero);
                Assert.That(snapshot.ReserveCapacity, Is.EqualTo(11));
                Assert.That(snapshot.TemporaryReserveCapacity, Is.EqualTo(11));

                Assert.That(snapshot.Cells.Count, Is.EqualTo(77));
                BoardCellSnapshot legacyHighGround = FindBoardCell(snapshot, 6, 3);
                Assert.That(legacyHighGround.CellId, Is.EqualTo(101));
                Assert.That(legacyHighGround.Terrain, Is.EqualTo("HighGround"));
                Assert.That(legacyHighGround.Zone, Is.EqualTo("Battlefield"));
                Assert.That(legacyHighGround.VisualX, Is.EqualTo(6));
                Assert.That(legacyHighGround.VisualY, Is.EqualTo(3));
                Assert.That(legacyHighGround.VisualHeight, Is.EqualTo(1));
                Assert.That(legacyHighGround.VisualKey, Is.EqualTo("high_ground"));
                Assert.That(legacyHighGround.AllowsBattleDeployment, Is.True);
                Assert.That(legacyHighGround.RoutePositions, Is.Empty);

                BoardCellSnapshot routeOneGround = FindBoardCell(snapshot, 6, 2);
                Assert.That(routeOneGround.Terrain, Is.EqualTo("Ground"));
                Assert.That(routeOneGround.Zone, Is.EqualTo("Battlefield"));
                Assert.That(routeOneGround.VisualHeight, Is.Zero);
                Assert.That(routeOneGround.VisualKey, Is.EqualTo("ground"));
                Assert.That(routeOneGround.AllowsBattleDeployment, Is.True);
                Assert.That(routeOneGround.RoutePositions.Count, Is.EqualTo(1));
                Assert.That(routeOneGround.RoutePositions[0].RouteId, Is.EqualTo(1));
                Assert.That(
                    routeOneGround.RoutePositions[0].PathProgress,
                    Is.EqualTo(0.7142857d).Within(0.0001d));

                BoardCellSnapshot legacySharedGround = FindBoardCell(snapshot, 3, 3);
                Assert.That(legacySharedGround.CellId, Is.EqualTo(102));
                Assert.That(legacySharedGround.Terrain, Is.EqualTo("Ground"));
                Assert.That(legacySharedGround.Zone, Is.EqualTo("Battlefield"));

                BoardCellSnapshot sharedRouteGround = FindBoardCell(snapshot, 4, 2);
                Assert.That(sharedRouteGround.RoutePositions.Count, Is.EqualTo(2));
                Assert.That(sharedRouteGround.GetRoutePosition(1), Is.Not.Null);
                Assert.That(sharedRouteGround.GetRoutePosition(2), Is.Not.Null);

                BoardCellSnapshot reserve = FindBoardCell(snapshot, 0, 0);
                Assert.That(reserve.Zone, Is.EqualTo("Reserve"));
                Assert.That(reserve.AcceptsReservePiece, Is.True);
                Assert.That(reserve.AllowsBattleDeployment, Is.False);

                BoardCellSnapshot temporaryReserve = FindBoardCell(snapshot, 0, 1);
                Assert.That(temporaryReserve.Zone, Is.EqualTo("TemporaryReserve"));
                Assert.That(temporaryReserve.AcceptsReservePiece, Is.False);
                Assert.That(temporaryReserve.AcceptsOverflowPiece, Is.True);
                Assert.That(temporaryReserve.AutoSellOnBattleStart, Is.True);

                BoardCellSnapshot spawn = FindBoardCell(snapshot, 10, 5);
                Assert.That(spawn.Zone, Is.EqualTo("Spawn"));
                Assert.That(spawn.AllowsBattleDeployment, Is.False);
                Assert.That(spawn.AllowsEnemyRoute, Is.True);

                Assert.That(snapshot.Routes.Count, Is.EqualTo(2));
                Assert.That(snapshot.Routes[0].RouteId, Is.EqualTo(1));
                Assert.That(snapshot.Routes[0].StartProgress, Is.Zero);
                Assert.That(snapshot.Routes[0].EndpointProgress, Is.EqualTo(1d));
                Assert.That(snapshot.Routes[0].Samples.Count, Is.EqualTo(15));
                Assert.That(snapshot.Routes[0].Samples[0].GridX, Is.EqualTo(10));
                Assert.That(snapshot.Routes[0].Samples[0].GridY, Is.EqualTo(2));
                Assert.That(
                    snapshot.Routes[0].Samples[0].PathProgress,
                    Is.Zero);
                Assert.That(snapshot.Routes[0].Samples[14].GridX, Is.EqualTo(2));
                Assert.That(snapshot.Routes[0].Samples[14].GridY, Is.EqualTo(2));
                Assert.That(snapshot.Routes[1].Samples.Count, Is.EqualTo(12));
                Assert.That(snapshot.Routes[1].Samples[0].GridX, Is.EqualTo(10));
                Assert.That(snapshot.Routes[1].Samples[0].GridY, Is.EqualTo(5));
                Assert.That(snapshot.Routes[1].Samples[6].GridX, Is.EqualTo(4));
                Assert.That(snapshot.Routes[1].Samples[6].GridY, Is.EqualTo(5));
                Assert.That(snapshot.Routes[1].Samples[7].GridX, Is.EqualTo(4));
                Assert.That(snapshot.Routes[1].Samples[7].GridY, Is.EqualTo(4));
                Assert.That(snapshot.Routes[1].Samples[8].GridX, Is.EqualTo(4));
                Assert.That(snapshot.Routes[1].Samples[8].GridY, Is.EqualTo(3));
                Assert.That(snapshot.Routes[1].Samples[11].GridX, Is.EqualTo(2));
                Assert.That(snapshot.Routes[1].Samples[11].GridY, Is.EqualTo(2));
                Assert.That(
                    snapshot.Routes[1].Samples[11].PathProgress,
                    Is.EqualTo(1d));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void FileSystemLoader_PrefersFirstAvailableRoot()
        {
            string testRoot = Path.Combine(
                Path.GetTempPath(),
                "ProtectTreeLuaLoaderTests",
                Guid.NewGuid().ToString("N"));
            string updateRoot = Path.Combine(testRoot, "Update");
            string packagedRoot = Path.Combine(testRoot, "Packaged");
            string relativePath = Path.Combine("Bootstrap", "Main.lua.txt");

            try
            {
                WriteScript(Path.Combine(packagedRoot, relativePath), "return 'packaged'");
                FileSystemLuaLoader loader = new FileSystemLuaLoader(updateRoot, packagedRoot);

                string moduleName = "Bootstrap.Main";
                Assert.That(
                    Encoding.UTF8.GetString(loader.Load(ref moduleName)),
                    Is.EqualTo("return 'packaged'"));

                WriteScript(Path.Combine(updateRoot, relativePath), "return 'updated'");
                moduleName = "Bootstrap.Main";
                Assert.That(
                    Encoding.UTF8.GetString(loader.Load(ref moduleName)),
                    Is.EqualTo("return 'updated'"));
            }
            finally
            {
                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, true);
                }
            }
        }

        [Test]
        public void MatchFlow_AdvancesThroughConfiguredPhases()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable flow = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                flow = RequireTable(runtime, "Match.Flow");
                CallModuleFunction(flow, "start");

                for (int wave = 1; wave <= 5; wave++)
                {
                    AssertSnapshot(runtime, "Preparation", wave, PreparationSeconds, false);
                    CallModuleFunction(flow, "update", PreparationSeconds);
                    AssertSnapshot(runtime, "Battle", wave, 0d, false);
                    CallModuleFunction(flow, "complete_battle");
                    AssertSnapshot(runtime, "Settlement", wave, 2d, false);
                    CallModuleFunction(flow, "update", 2d);
                }

                AssertSnapshot(runtime, "BossPreparation", 6, PreparationSeconds, false);
                CallModuleFunction(flow, "update", PreparationSeconds);
                AssertSnapshot(runtime, "BossBattle", 6, 0d, false);
                CallModuleFunction(flow, "complete_boss", "Victory");
                AssertSnapshot(runtime, "End", 6, 0d, true, "Victory");

                LuaTable events = CallModuleTableFunction(flow, "drain_events");
                try
                {
                    LuaTable endEvent = events.Get<int, LuaTable>(events.Length);
                    try
                    {
                        Assert.That(endEvent.Get<string>("phase"), Is.EqualTo("End"));
                        Assert.That(endEvent.Get<string>("result"), Is.EqualTo("Victory"));
                    }
                    finally
                    {
                        endEvent.Dispose();
                    }
                }
                finally
                {
                    events.Dispose();
                }
            }
            finally
            {
                flow?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_AllPlayersEliminatedEndsMatchInDefeat()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                AdvanceUntilPhase(
                    runtime,
                    "End",
                    maxStepCount: ThreeWaveDefeatStepBudget);

                AssertSnapshot(runtime, "End", 3, 0d, true, "Defeat");
                Assert.That(runtime.GetPlayerRosterSnapshot().AliveCount, Is.Zero);
                Assert.That(
                    runtime.GetPlayerRosterSnapshot().Players[0].Status,
                    Is.EqualTo("Eliminated"));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_StrongBoardDefeatsBossAndEndsInVictory()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                DeployBossVictoryBoard(runtime);

                AdvanceUntilPhase(runtime, "End", maxStepCount: 2500);

                AssertSnapshot(runtime, "End", 6, 0d, true, "Victory");
                EnemyRosterSnapshot enemies = runtime.GetEnemyRosterSnapshot();
                EnemySnapshot boss = enemies.Enemies[enemies.Enemies.Count - 1];
                Assert.That(boss.EnemyId, Is.EqualTo("AncientGuardian"));
                Assert.That(boss.IsBoss, Is.True);
                Assert.That(boss.Status, Is.EqualTo("Defeated"));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchEvents_AreDrainedOnceAndMayBeReadInBatches()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");

                AssertEvents(
                    runtime.DrainMatchEvents(),
                    new MatchEvent("PhaseChanged", 1, "Preparation"));
                Assert.That(runtime.DrainMatchEvents(), Is.Empty);

                AdvanceSimulation(runtime, 1);
                Assert.That(runtime.DrainMatchEvents(), Is.Empty);

                AdvanceSimulation(runtime, PreparationStepCount - 1);
                AssertEvents(
                    runtime.DrainMatchEvents(),
                    new MatchEvent("PhaseChanged", 1, "Battle"),
                    new MatchEvent("EnemySpawnRequested", 1, enemyId: "Crab", spawnIndex: 1),
                    new MatchEvent(
                        "EnemyCreated",
                        1,
                        enemyId: "Crab",
                        spawnIndex: 1,
                        enemyInstanceId: 1));
                Assert.That(runtime.DrainMatchEvents(), Is.Empty);

                AdvanceUntilPhase(
                    runtime,
                    "Settlement",
                    maxStepCount: SingleWavePostSpawnCompletionStepBudget);
                AssertEvents(
                    runtime.DrainMatchEvents(),
                    new MatchEvent("EnemySpawnRequested", 1, enemyId: "Crab", spawnIndex: 2),
                    new MatchEvent(
                        "EnemyCreated",
                        1,
                        enemyId: "Crab",
                        spawnIndex: 2,
                        enemyInstanceId: 2),
                    new MatchEvent("EnemySpawnRequested", 1, enemyId: "Crab", spawnIndex: 3),
                    new MatchEvent(
                        "EnemyCreated",
                        1,
                        enemyId: "Crab",
                        spawnIndex: 3,
                        enemyInstanceId: 3),
                    new MatchEvent("WaveSpawnCompleted", 1),
                    new MatchEvent(
                        "EnemyReachedEndpoint",
                        1,
                        enemyId: "Crab",
                        spawnIndex: 1,
                        enemyInstanceId: 1),
                    new MatchEvent(
                        "EnemyReachedEndpoint",
                        1,
                        enemyId: "Crab",
                        spawnIndex: 2,
                        enemyInstanceId: 2),
                    new MatchEvent(
                        "EnemyReachedEndpoint",
                        1,
                        enemyId: "Crab",
                        spawnIndex: 3,
                        enemyInstanceId: 3),
                    new MatchEvent("PlayerLeakResolved", 1),
                    new MatchEvent("PlayerDamaged", 1),
                    new MatchEvent("GoldGranted", 1),
                    new MatchEvent("ShopRefreshed"),
                    new MatchEvent("PhaseChanged", 1, "Settlement"));

                Assert.That(runtime.DrainMatchEvents(), Is.Empty);

                AdvanceSimulation(runtime, 20);
                AssertEvents(
                    runtime.DrainMatchEvents(),
                    new MatchEvent("PhaseChanged", 2, "Preparation"));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_BattleWaitsForSpawnerAndAliveEnemies()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                runtime.DrainMatchEvents();

                AdvanceSimulation(runtime, PreparationStepCount);
                AssertSnapshot(runtime, "Battle", 1, 0d, false);

                AdvanceSimulation(runtime, 20);

                WaveSpawnerSnapshot completedSpawner = runtime.GetWaveSpawnerSnapshot();
                Assert.That(completedSpawner.IsActive, Is.False);
                Assert.That(completedSpawner.RemainingCount, Is.Zero);
                Assert.That(runtime.GetEnemyRosterSnapshot().AliveCount, Is.EqualTo(3));
                AssertSnapshot(runtime, "Battle", 1, 0d, false);

                AdvanceUntilEnemyAliveCount(
                    runtime,
                    expectedAliveCount: 1,
                    maxStepCount: SingleWavePostSpawnCompletionStepBudget);

                Assert.That(runtime.GetEnemyRosterSnapshot().AliveCount, Is.EqualTo(1));
                AssertSnapshot(runtime, "Battle", 1, 0d, false);

                AdvanceUntilPhase(
                    runtime,
                    "Settlement",
                    maxStepCount: SingleWavePostSpawnCompletionStepBudget);

                Assert.That(runtime.GetEnemyRosterSnapshot().AliveCount, Is.Zero);
                AssertSnapshot(runtime, "Settlement", 1, 2d, false);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_ResolvesSinglePlayerLeaksBeforeSettlement()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable session = null;
            LuaTable resolver = null;
            LuaTable playerRoster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                runtime.DrainMatchEvents();

                session = RequireTable(runtime, "Match.Session");
                resolver = RequireTable(runtime, "Match.LeakResolver");
                playerRoster = RequireTable(runtime, "Match.PlayerRoster");

                AdvanceUntilPhase(
                    runtime,
                    "Settlement",
                    maxStepCount: PreparationStepCount + SingleWaveSettlementStepBudget);

                AssertLeakResolverSnapshot(
                    resolver,
                    expectedWave: 1,
                    expectedResolved: true,
                    expectedInitialLeaks: 3,
                    expectedRescued: 0,
                    expectedFinalLeaks: 3);
                AssertPlayerRosterSnapshot(
                    playerRoster,
                    expectedHealth: 7,
                    expectedStatus: "Alive",
                    expectedAliveCount: 1,
                    expectedGold: 15);

                LuaTable events = CallModuleTableFunction(session, "drain_events");
                try
                {
                    Assert.That(events.Length, Is.EqualTo(16));

                    for (int index = 9; index <= 11; index++)
                    {
                        LuaTable endpointEvent = events.Get<int, LuaTable>(index);
                        try
                        {
                            Assert.That(
                                endpointEvent.Get<string>("type"),
                                Is.EqualTo("EnemyReachedEndpoint"));
                            Assert.That(endpointEvent.Get<int>("target_player_id"), Is.EqualTo(1));
                            Assert.That(
                                endpointEvent.Get<int>("enemy_instance_id"),
                                Is.EqualTo(index - 8));
                        }
                        finally
                        {
                            endpointEvent.Dispose();
                        }
                    }

                    LuaTable resolvedEvent = events.Get<int, LuaTable>(12);
                    try
                    {
                        Assert.That(
                            resolvedEvent.Get<string>("type"),
                            Is.EqualTo("PlayerLeakResolved"));
                        Assert.That(resolvedEvent.Get<int>("player_id"), Is.EqualTo(1));
                        Assert.That(resolvedEvent.Get<int>("initial_leak_count"), Is.EqualTo(3));
                        Assert.That(resolvedEvent.Get<int>("rescued_count"), Is.Zero);
                        Assert.That(resolvedEvent.Get<int>("final_leak_count"), Is.EqualTo(3));
                    }
                    finally
                    {
                        resolvedEvent.Dispose();
                    }

                    LuaTable damagedEvent = events.Get<int, LuaTable>(13);
                    try
                    {
                        Assert.That(
                            damagedEvent.Get<string>("type"),
                            Is.EqualTo("PlayerDamaged"));
                        Assert.That(damagedEvent.Get<int>("player_id"), Is.EqualTo(1));
                        Assert.That(damagedEvent.Get<int>("damage"), Is.EqualTo(3));
                        Assert.That(damagedEvent.Get<int>("health"), Is.EqualTo(7));
                    }
                    finally
                    {
                        damagedEvent.Dispose();
                    }

                    LuaTable goldEvent = events.Get<int, LuaTable>(14);
                    try
                    {
                        Assert.That(goldEvent.Get<string>("type"), Is.EqualTo("GoldGranted"));
                        Assert.That(goldEvent.Get<int>("amount"), Is.EqualTo(5));
                        Assert.That(goldEvent.Get<int>("gold"), Is.EqualTo(15));
                    }
                    finally
                    {
                        goldEvent.Dispose();
                    }

                    LuaTable shopRefreshEvent = events.Get<int, LuaTable>(15);
                    try
                    {
                        Assert.That(
                            shopRefreshEvent.Get<string>("type"),
                            Is.EqualTo("ShopRefreshed"));
                        Assert.That(
                            shopRefreshEvent.Get<string>("reason"),
                            Is.EqualTo("Settlement"));
                    }
                    finally
                    {
                        shopRefreshEvent.Dispose();
                    }

                    LuaTable settlementEvent = events.Get<int, LuaTable>(16);
                    try
                    {
                        Assert.That(
                            settlementEvent.Get<string>("type"),
                            Is.EqualTo("PhaseChanged"));
                        Assert.That(settlementEvent.Get<string>("phase"), Is.EqualTo("Settlement"));
                    }
                    finally
                    {
                        settlementEvent.Dispose();
                    }
                }
                finally
                {
                    events.Dispose();
                }
            }
            finally
            {
                session?.Dispose();
                resolver?.Dispose();
                playerRoster?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_SpawnRequestsCreateAuthoritativeEnemies()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                runtime.DrainMatchEvents();

                AdvanceSimulation(runtime, PreparationStepCount);

                EnemyRosterSnapshot firstSpawn = runtime.GetEnemyRosterSnapshot();
                Assert.That(firstSpawn.AliveCount, Is.EqualTo(1));
                Assert.That(firstSpawn.Enemies.Count, Is.EqualTo(1));
                AssertEnemy(firstSpawn.Enemies[0], 1, 1, 1);

                AdvanceSimulation(runtime, 20);

                EnemyRosterSnapshot completedWave = runtime.GetEnemyRosterSnapshot();
                Assert.That(completedWave.AliveCount, Is.EqualTo(3));
                Assert.That(completedWave.Enemies.Count, Is.EqualTo(3));

                for (int index = 0; index < completedWave.Enemies.Count; index++)
                {
                    int expected = index + 1;
                    AssertEnemy(completedWave.Enemies[index], expected, 1, expected);
                }
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_NewEnemyStartsAtZeroAndMovesOnFollowingTick()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                runtime.DrainMatchEvents();

                AdvanceSimulation(runtime, PreparationStepCount);

                EnemySnapshot spawned = runtime.GetEnemyRosterSnapshot().Enemies[0];
                Assert.That(spawned.PathSpeed, Is.EqualTo(0.05d).Within(0.0001d));
                Assert.That(spawned.PathProgress, Is.Zero);

                AdvanceSimulation(runtime, 1);

                EnemySnapshot moved = runtime.GetEnemyRosterSnapshot().Enemies[0];
                Assert.That(moved.PathProgress, Is.EqualTo(0.005d).Within(0.0001d));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_BattlePhaseDrivesWaveSpawner()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                runtime.DrainMatchEvents();

                AdvanceSimulation(runtime, PreparationStepCount);

                WaveSpawnerSnapshot active = runtime.GetWaveSpawnerSnapshot();
                Assert.That(active.IsActive, Is.True);
                Assert.That(active.Wave, Is.EqualTo(1));
                Assert.That(active.EnemyId, Is.EqualTo("Crab"));
                Assert.That(active.RouteId, Is.EqualTo(1));
                Assert.That(active.RemainingCount, Is.EqualTo(2));
                Assert.That(active.NextSpawnIndex, Is.EqualTo(2));
                Assert.That(active.SecondsUntilNextSpawn, Is.EqualTo(0.9d).Within(0.0001d));

                AdvanceSimulation(runtime, 20);

                WaveSpawnerSnapshot completed = runtime.GetWaveSpawnerSnapshot();
                Assert.That(completed.IsActive, Is.False);
                Assert.That(completed.Wave, Is.EqualTo(1));
                Assert.That(completed.RemainingCount, Is.Zero);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void WaveAndEnemyConfigs_CreateThreeDistinctEnemyArchetypes()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable spawner = null;
            LuaTable enemyRoster = null;
            string[] expectedEnemyIds = { "Crab", "Skitter", "Shellback" };

            try
            {
                runtime.Start("Bootstrap.Main");
                spawner = RequireTable(runtime, "Match.WaveSpawner");
                enemyRoster = RequireTable(runtime, "Match.EnemyRoster");
                CallModuleFunction(spawner, "start");
                CallModuleFunction(enemyRoster, "start");

                for (int wave = 1; wave <= expectedEnemyIds.Length; wave++)
                {
                    LuaTable phaseEvent = runtime.Environment.NewTable();
                    phaseEvent.Set("type", "PhaseChanged");
                    phaseEvent.Set("phase", "Battle");
                    phaseEvent.Set("wave", wave);

                    try
                    {
                        CallModuleFunction(spawner, "handle_event", phaseEvent);
                    }
                    finally
                    {
                        phaseEvent.Dispose();
                    }

                    LuaTable spawnerSnapshot =
                        CallModuleTableFunction(spawner, "get_snapshot");
                    try
                    {
                        Assert.That(
                            spawnerSnapshot.Get<string>("enemy_id"),
                            Is.EqualTo(expectedEnemyIds[wave - 1]));
                        Assert.That(
                            spawnerSnapshot.Get<int>("route_id"),
                            Is.EqualTo(wave == 2 ? 2 : 1));
                    }
                    finally
                    {
                        spawnerSnapshot.Dispose();
                    }

                    LuaTable spawnEvent = CreateEvent(
                        runtime,
                        "EnemySpawnRequested",
                        wave,
                        expectedEnemyIds[wave - 1],
                        spawnIndex: 1);
                    try
                    {
                        CallModuleFunction(enemyRoster, "handle_event", spawnEvent);
                    }
                    finally
                    {
                        spawnEvent.Dispose();
                    }
                }

                LuaTable enemySnapshot =
                    CallModuleTableFunction(enemyRoster, "get_snapshot");
                try
                {
                    LuaTable enemies = enemySnapshot.Get<LuaTable>("enemies");
                    try
                    {
                        Assert.That(enemies.Length, Is.EqualTo(3));
                        AssertEnemyArchetype(
                            enemies,
                            index: 1,
                            enemyId: "Crab",
                            maxHealth: 10,
                            pathSpeed: 0.05d,
                            attackDamage: 3,
                            attackIntervalSeconds: 1d);
                        AssertEnemyArchetype(
                            enemies,
                            index: 2,
                            enemyId: "Skitter",
                            maxHealth: 6,
                            pathSpeed: 0.4d,
                            attackDamage: 2,
                            attackIntervalSeconds: 0.7d);
                        AssertEnemyArchetype(
                            enemies,
                            index: 3,
                            enemyId: "Shellback",
                            maxHealth: 24,
                            pathSpeed: 0.15d,
                            attackDamage: 5,
                            attackIntervalSeconds: 1.5d);
                    }
                    finally
                    {
                        enemies.Dispose();
                    }
                }
                finally
                {
                    enemySnapshot.Dispose();
                }
            }
            finally
            {
                enemyRoster?.Dispose();
                spawner?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void EnemyRoster_CreatesDeterministicRecordsAndReturnsCopies()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable roster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                roster = RequireTable(runtime, "Match.EnemyRoster");
                CallModuleFunction(roster, "start");

                for (int spawnIndex = 1; spawnIndex <= 3; spawnIndex++)
                {
                    LuaTable spawnEvent = CreateEvent(
                        runtime,
                        "EnemySpawnRequested",
                        wave: 1,
                        enemyId: "Crab",
                        spawnIndex: spawnIndex);

                    try
                    {
                        CallModuleFunction(roster, "handle_event", spawnEvent);
                    }
                    finally
                    {
                        spawnEvent.Dispose();
                    }
                }

                AssertEnemyRosterSnapshot(roster, expectedCount: 3, expectedFirstHealth: 10);

                LuaTable mutableSnapshot = CallModuleTableFunction(roster, "get_snapshot");
                try
                {
                    LuaTable enemies = mutableSnapshot.Get<LuaTable>("enemies");
                    try
                    {
                        LuaTable firstEnemy = enemies.Get<int, LuaTable>(1);
                        try
                        {
                            firstEnemy.Set("health", 0);
                        }
                        finally
                        {
                            firstEnemy.Dispose();
                        }
                    }
                    finally
                    {
                        enemies.Dispose();
                    }
                }
                finally
                {
                    mutableSnapshot.Dispose();
                }

                AssertEnemyRosterSnapshot(roster, expectedCount: 3, expectedFirstHealth: 10);

                LuaTable createdEvents = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(createdEvents.Length, Is.EqualTo(3));

                    for (int index = 1; index <= createdEvents.Length; index++)
                    {
                        LuaTable createdEvent = createdEvents.Get<int, LuaTable>(index);
                        try
                        {
                            Assert.That(createdEvent.Get<string>("type"), Is.EqualTo("EnemyCreated"));
                            Assert.That(createdEvent.Get<int>("enemy_instance_id"), Is.EqualTo(index));
                            Assert.That(createdEvent.Get<int>("spawn_index"), Is.EqualTo(index));
                            Assert.That(createdEvent.Get<int>("target_player_id"), Is.EqualTo(1));
                        }
                        finally
                        {
                            createdEvent.Dispose();
                        }
                    }
                }
                finally
                {
                    createdEvents.Dispose();
                }

                LuaTable drainedAgain = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(drainedAgain.Length, Is.Zero);
                }
                finally
                {
                    drainedAgain.Dispose();
                }
            }
            finally
            {
                if (roster != null)
                {
                    CallModuleFunction(roster, "shutdown");
                    roster.Dispose();
                }

                runtime.Dispose();
            }
        }

        [Test]
        public void EnemyRoster_BossResultDistinguishesVictoryAndDefeat()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable roster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                roster = RequireTable(runtime, "Match.EnemyRoster");
                CallModuleFunction(roster, "start");

                LuaTable firstBossSpawn = CreateEvent(
                    runtime,
                    "EnemySpawnRequested",
                    wave: 6,
                    enemyId: "AncientGuardian",
                    spawnIndex: 1);
                try
                {
                    CallModuleFunction(roster, "handle_event", firstBossSpawn);
                }
                finally
                {
                    firstBossSpawn.Dispose();
                }

                CallModuleFunction(roster, "update", 20d);
                Assert.That(
                    CallModuleStringFunction(roster, "get_boss_result", 6),
                    Is.EqualTo("Defeat"));

                CallModuleFunction(roster, "start");
                LuaTable secondBossSpawn = CreateEvent(
                    runtime,
                    "EnemySpawnRequested",
                    wave: 6,
                    enemyId: "AncientGuardian",
                    spawnIndex: 1);
                try
                {
                    CallModuleFunction(roster, "handle_event", secondBossSpawn);
                }
                finally
                {
                    secondBossSpawn.Dispose();
                }

                HandleEnemyDamageRequested(runtime, roster, 6, 1, 1, 120);
                Assert.That(
                    CallModuleStringFunction(roster, "get_boss_result", 6),
                    Is.EqualTo("Victory"));
            }
            finally
            {
                roster?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void EnemyRoster_ReachingEndpointClampsAndEmitsSingleShotEvents()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable roster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                roster = RequireTable(runtime, "Match.EnemyRoster");
                CallModuleFunction(roster, "start");

                for (int spawnIndex = 1; spawnIndex <= 2; spawnIndex++)
                {
                    LuaTable spawnEvent = CreateEvent(
                        runtime,
                        "EnemySpawnRequested",
                        wave: 1,
                        enemyId: "Crab",
                        spawnIndex: spawnIndex);

                    try
                    {
                        CallModuleFunction(roster, "handle_event", spawnEvent);
                    }
                    finally
                    {
                        spawnEvent.Dispose();
                    }
                }

                LuaTable createdEvents = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(createdEvents.Length, Is.EqualTo(2));
                }
                finally
                {
                    createdEvents.Dispose();
                }

                CallModuleFunction(roster, "update", 20d);
                AssertReachedEndpointSnapshot(roster, expectedEnemyCount: 2);

                LuaTable reachedEvents = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(reachedEvents.Length, Is.EqualTo(2));

                    for (int index = 1; index <= reachedEvents.Length; index++)
                    {
                        LuaTable reachedEvent = reachedEvents.Get<int, LuaTable>(index);
                        try
                        {
                            Assert.That(
                                reachedEvent.Get<string>("type"),
                                Is.EqualTo("EnemyReachedEndpoint"));
                            Assert.That(
                                reachedEvent.Get<int>("enemy_instance_id"),
                                Is.EqualTo(index));
                            Assert.That(
                                reachedEvent.Get<int>("target_player_id"),
                                Is.EqualTo(1));
                        }
                        finally
                        {
                            reachedEvent.Dispose();
                        }
                    }
                }
                finally
                {
                    reachedEvents.Dispose();
                }

                CallModuleFunction(roster, "update", 1d);
                AssertReachedEndpointSnapshot(roster, expectedEnemyCount: 2);

                LuaTable drainedAgain = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(drainedAgain.Length, Is.Zero);
                }
                finally
                {
                    drainedAgain.Dispose();
                }
            }
            finally
            {
                if (roster != null)
                {
                    CallModuleFunction(roster, "shutdown");
                    roster.Dispose();
                }

                runtime.Dispose();
            }
        }

        [Test]
        public void EnemyRoster_DamageDefeatsAndPreventsEndpoint()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable roster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                roster = RequireTable(runtime, "Match.EnemyRoster");
                CallModuleFunction(roster, "start");

                LuaTable spawnEvent = CreateEvent(
                    runtime,
                    "EnemySpawnRequested",
                    wave: 1,
                    enemyId: "Crab",
                    spawnIndex: 1);

                try
                {
                    CallModuleFunction(roster, "handle_event", spawnEvent);
                }
                finally
                {
                    spawnEvent.Dispose();
                }

                LuaTable createdEvents = CallModuleTableFunction(roster, "drain_events");
                createdEvents.Dispose();

                HandleEnemyDamageRequested(
                    runtime,
                    roster,
                    wave: 1,
                    enemyInstanceId: 1,
                    sourcePieceInstanceId: 7,
                    damage: 4);
                AssertEnemyCombatSnapshot(
                    roster,
                    expectedHealth: 6,
                    expectedStatus: "Alive",
                    expectedAliveCount: 1,
                    expectedPathProgress: 0d);

                LuaTable firstDamageEvents = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(firstDamageEvents.Length, Is.EqualTo(1));

                    LuaTable damagedEvent = firstDamageEvents.Get<int, LuaTable>(1);
                    try
                    {
                        Assert.That(damagedEvent.Get<string>("type"), Is.EqualTo("EnemyDamaged"));
                        Assert.That(damagedEvent.Get<int>("wave"), Is.EqualTo(1));
                        Assert.That(damagedEvent.Get<string>("enemy_id"), Is.EqualTo("Crab"));
                        Assert.That(damagedEvent.Get<int>("enemy_instance_id"), Is.EqualTo(1));
                        Assert.That(damagedEvent.Get<int>("target_player_id"), Is.EqualTo(1));
                        Assert.That(damagedEvent.Get<int>("source_piece_instance_id"), Is.EqualTo(7));
                        Assert.That(damagedEvent.Get<int>("damage"), Is.EqualTo(4));
                        Assert.That(damagedEvent.Get<int>("health"), Is.EqualTo(6));
                    }
                    finally
                    {
                        damagedEvent.Dispose();
                    }
                }
                finally
                {
                    firstDamageEvents.Dispose();
                }

                HandleEnemyDamageRequested(
                    runtime,
                    roster,
                    wave: 1,
                    enemyInstanceId: 1,
                    sourcePieceInstanceId: 7,
                    damage: 10);
                AssertEnemyCombatSnapshot(
                    roster,
                    expectedHealth: 0,
                    expectedStatus: "Defeated",
                    expectedAliveCount: 0,
                    expectedPathProgress: 0d);

                LuaTable lethalEvents = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(lethalEvents.Length, Is.EqualTo(2));

                    LuaTable damagedEvent = lethalEvents.Get<int, LuaTable>(1);
                    try
                    {
                        Assert.That(damagedEvent.Get<string>("type"), Is.EqualTo("EnemyDamaged"));
                        Assert.That(damagedEvent.Get<int>("damage"), Is.EqualTo(6));
                        Assert.That(damagedEvent.Get<int>("health"), Is.Zero);
                    }
                    finally
                    {
                        damagedEvent.Dispose();
                    }

                    LuaTable defeatedEvent = lethalEvents.Get<int, LuaTable>(2);
                    try
                    {
                        Assert.That(defeatedEvent.Get<string>("type"), Is.EqualTo("EnemyDefeated"));
                        Assert.That(defeatedEvent.Get<int>("wave"), Is.EqualTo(1));
                        Assert.That(defeatedEvent.Get<string>("enemy_id"), Is.EqualTo("Crab"));
                        Assert.That(defeatedEvent.Get<int>("enemy_instance_id"), Is.EqualTo(1));
                        Assert.That(defeatedEvent.Get<int>("target_player_id"), Is.EqualTo(1));
                        Assert.That(defeatedEvent.Get<int>("source_piece_instance_id"), Is.EqualTo(7));
                        Assert.That(defeatedEvent.Get<int>("health"), Is.Zero);
                    }
                    finally
                    {
                        defeatedEvent.Dispose();
                    }
                }
                finally
                {
                    lethalEvents.Dispose();
                }

                HandleEnemyDamageRequested(
                    runtime,
                    roster,
                    wave: 1,
                    enemyInstanceId: 1,
                    sourcePieceInstanceId: 8,
                    damage: 3);
                CallModuleFunction(roster, "update", 10d);

                AssertEnemyCombatSnapshot(
                    roster,
                    expectedHealth: 0,
                    expectedStatus: "Defeated",
                    expectedAliveCount: 0,
                    expectedPathProgress: 0d);

                LuaTable eventsAfterDefeat = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(eventsAfterDefeat.Length, Is.Zero);
                }
                finally
                {
                    eventsAfterDefeat.Dispose();
                }
            }
            finally
            {
                if (roster != null)
                {
                    CallModuleFunction(roster, "shutdown");
                    roster.Dispose();
                }

                runtime.Dispose();
            }
        }

        [Test]
        public void EnemyRoster_UpdatesProgressAndStartResetsAllState()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable roster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                roster = RequireTable(runtime, "Match.EnemyRoster");
                CallModuleFunction(roster, "start");

                LuaTable spawnEvent = CreateEvent(
                    runtime,
                    "EnemySpawnRequested",
                    wave: 1,
                    enemyId: "Crab",
                    spawnIndex: 1);

                try
                {
                    CallModuleFunction(roster, "handle_event", spawnEvent);
                }
                finally
                {
                    spawnEvent.Dispose();
                }

                LuaTable createdEvents = CallModuleTableFunction(roster, "drain_events");
                createdEvents.Dispose();

                CallModuleFunction(roster, "update", 1d);
                AssertAliveProgressSnapshot(roster, expectedProgress: 0.05d);

                LuaTable noEndpointEvents = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(noEndpointEvents.Length, Is.Zero);
                }
                finally
                {
                    noEndpointEvents.Dispose();
                }

                CallModuleFunction(roster, "update", 19d);
                AssertReachedEndpointSnapshot(roster, expectedEnemyCount: 1);

                CallModuleFunction(roster, "start");
                LuaTable resetSnapshot = CallModuleTableFunction(roster, "get_snapshot");
                try
                {
                    Assert.That(resetSnapshot.Get<int>("alive_count"), Is.Zero);

                    LuaTable enemies = resetSnapshot.Get<LuaTable>("enemies");
                    try
                    {
                        Assert.That(enemies.Length, Is.Zero);
                    }
                    finally
                    {
                        enemies.Dispose();
                    }
                }
                finally
                {
                    resetSnapshot.Dispose();
                }
            }
            finally
            {
                if (roster != null)
                {
                    CallModuleFunction(roster, "shutdown");
                    roster.Dispose();
                }

                runtime.Dispose();
            }
        }

        [Test]
        public void EnemyRoster_ReachedRecordsStayFixedWhileLaterEnemiesAdvance()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable roster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                roster = RequireTable(runtime, "Match.EnemyRoster");
                CallModuleFunction(roster, "start");

                LuaTable firstSpawn = CreateEvent(
                    runtime,
                    "EnemySpawnRequested",
                    wave: 1,
                    enemyId: "Crab",
                    spawnIndex: 1);
                try
                {
                    CallModuleFunction(roster, "handle_event", firstSpawn);
                }
                finally
                {
                    firstSpawn.Dispose();
                }

                CallModuleFunction(roster, "update", 20d);

                LuaTable secondSpawn = CreateEvent(
                    runtime,
                    "EnemySpawnRequested",
                    wave: 1,
                    enemyId: "Crab",
                    spawnIndex: 2);
                try
                {
                    CallModuleFunction(roster, "handle_event", secondSpawn);
                }
                finally
                {
                    secondSpawn.Dispose();
                }

                CallModuleFunction(roster, "update", 1d);

                LuaTable snapshot = CallModuleTableFunction(roster, "get_snapshot");
                try
                {
                    Assert.That(snapshot.Get<int>("alive_count"), Is.EqualTo(1));

                    LuaTable enemies = snapshot.Get<LuaTable>("enemies");
                    try
                    {
                        Assert.That(enemies.Length, Is.EqualTo(2));
                        AssertEnemyPathState(
                            enemies,
                            index: 1,
                            status: "ReachedEndpoint",
                            expectedProgress: 1d);
                        AssertEnemyPathState(
                            enemies,
                            index: 2,
                            status: "Alive",
                            expectedProgress: 0.05d);
                    }
                    finally
                    {
                        enemies.Dispose();
                    }
                }
                finally
                {
                    snapshot.Dispose();
                }
            }
            finally
            {
                if (roster != null)
                {
                    CallModuleFunction(roster, "shutdown");
                    roster.Dispose();
                }

                runtime.Dispose();
            }
        }

        [Test]
        public void PieceRoster_GrantsAndDeploysToFixedCells()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable roster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                roster = RequireTable(runtime, "Match.PieceRoster");
                CallModuleFunction(roster, "start");

                int firstPieceInstanceId = CallModuleIntFunction(
                    roster,
                    "grant_piece",
                    1,
                    "Sprout");
                int secondPieceInstanceId = CallModuleIntFunction(
                    roster,
                    "grant_piece",
                    1,
                    "Sprout");

                Assert.That(firstPieceInstanceId, Is.EqualTo(1));
                Assert.That(secondPieceInstanceId, Is.EqualTo(2));
                AssertPieceRosterSnapshot(
                    roster,
                    firstLocation: "Bench",
                    firstCellId: 1001,
                    secondLocation: "Bench",
                    secondCellId: 1002);

                LuaTable grantedEvents = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(grantedEvents.Length, Is.EqualTo(2));

                    for (int index = 1; index <= grantedEvents.Length; index++)
                    {
                        LuaTable grantedEvent = grantedEvents.Get<int, LuaTable>(index);
                        try
                        {
                            Assert.That(
                                grantedEvent.Get<string>("type"),
                                Is.EqualTo("PieceGranted"));
                            Assert.That(grantedEvent.Get<int>("player_id"), Is.EqualTo(1));
                            Assert.That(grantedEvent.Get<string>("piece_id"), Is.EqualTo("Sprout"));
                            Assert.That(
                                grantedEvent.Get<int>("piece_instance_id"),
                                Is.EqualTo(index));
                            Assert.That(
                                grantedEvent.Get<int>("cell_id"),
                                Is.EqualTo(1000 + index));
                        }
                        finally
                        {
                            grantedEvent.Dispose();
                        }
                    }
                }
                finally
                {
                    grantedEvents.Dispose();
                }

                HandlePieceDeployRequested(
                    runtime,
                    roster,
                    playerId: 1,
                    pieceInstanceId: firstPieceInstanceId,
                    cellId: 101);
                AssertPieceRosterSnapshot(
                    roster,
                    firstLocation: "Board",
                    firstCellId: 101,
                    secondLocation: "Bench",
                    secondCellId: 1002);

                LuaTable occupiedCellRequest = CreatePieceDeployRequested(
                    runtime,
                    playerId: 1,
                    pieceInstanceId: secondPieceInstanceId,
                    cellId: 101);
                try
                {
                    Assert.Throws<LuaException>(
                        () => CallModuleFunction(roster, "handle_event", occupiedCellRequest));
                }
                finally
                {
                    occupiedCellRequest.Dispose();
                }

                AssertPieceRosterSnapshot(
                    roster,
                    firstLocation: "Board",
                    firstCellId: 101,
                    secondLocation: "Bench",
                    secondCellId: 1002);

                LuaTable obstacleRequest = CreatePieceDeployRequested(
                    runtime,
                    playerId: 1,
                    pieceInstanceId: secondPieceInstanceId,
                    cellId: 104);
                try
                {
                    Assert.Throws<LuaException>(
                        () => CallModuleFunction(roster, "handle_event", obstacleRequest));
                }
                finally
                {
                    obstacleRequest.Dispose();
                }

                LuaTable reserveRequest = CreatePieceDeployRequested(
                    runtime,
                    playerId: 1,
                    pieceInstanceId: secondPieceInstanceId,
                    cellId: 1001);
                try
                {
                    Assert.Throws<LuaException>(
                        () => CallModuleFunction(roster, "handle_event", reserveRequest));
                }
                finally
                {
                    reserveRequest.Dispose();
                }

                HandlePieceDeployRequested(
                    runtime,
                    roster,
                    playerId: 1,
                    pieceInstanceId: secondPieceInstanceId,
                    cellId: 102);
                HandlePieceDeployRequested(
                    runtime,
                    roster,
                    playerId: 1,
                    pieceInstanceId: firstPieceInstanceId,
                    cellId: 103);

                AssertPieceRosterSnapshot(
                    roster,
                    firstLocation: "Board",
                    firstCellId: 103,
                    secondLocation: "Board",
                    secondCellId: 102);

                LuaTable mutableSnapshot = CallModuleTableFunction(roster, "get_snapshot");
                try
                {
                    LuaTable pieces = mutableSnapshot.Get<LuaTable>("pieces");
                    try
                    {
                        LuaTable firstPiece = pieces.Get<int, LuaTable>(1);
                        try
                        {
                            firstPiece.Set("cell_id", 999);
                        }
                        finally
                        {
                            firstPiece.Dispose();
                        }
                    }
                    finally
                    {
                        pieces.Dispose();
                    }
                }
                finally
                {
                    mutableSnapshot.Dispose();
                }

                AssertPieceRosterSnapshot(
                    roster,
                    firstLocation: "Board",
                    firstCellId: 103,
                    secondLocation: "Board",
                    secondCellId: 102);

                LuaTable deployedEvents = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(deployedEvents.Length, Is.EqualTo(3));
                    AssertPieceDeployedEvent(
                        deployedEvents,
                        index: 1,
                        expectedPieceInstanceId: 1,
                        expectedCellId: 101,
                        expectedPreviousCellId: 1001);
                    AssertPieceDeployedEvent(
                        deployedEvents,
                        index: 2,
                        expectedPieceInstanceId: 2,
                        expectedCellId: 102,
                        expectedPreviousCellId: 1002);
                    AssertPieceDeployedEvent(
                        deployedEvents,
                        index: 3,
                        expectedPieceInstanceId: 1,
                        expectedCellId: 103,
                        expectedPreviousCellId: 101);
                }
                finally
                {
                    deployedEvents.Dispose();
                }
            }
            finally
            {
                if (roster != null)
                {
                    CallModuleFunction(roster, "shutdown");
                    roster.Dispose();
                }

                runtime.Dispose();
            }
        }

        [Test]
        public void PieceRoster_EnforcesBenchAndDeploymentCapacity()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable roster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                roster = RequireTable(runtime, "Match.PieceRoster");
                CallModuleFunction(roster, "start");

                List<int> benchPieceIds = FillBenchWithMergedSprouts(runtime, roster);

                AssertPieceCapacitySnapshot(
                    roster,
                    expectedBenchCount: 11,
                    expectedBoardCount: 0);
                Assert.Throws<LuaException>(
                    () => CallModuleIntFunction(roster, "grant_piece", 1, "Sprout"));
                CallModuleTableFunction(roster, "drain_events").Dispose();

                HandlePieceDeployRequested(runtime, roster, 1, benchPieceIds[0], 101);
                HandlePieceDeployRequested(runtime, roster, 1, benchPieceIds[1], 102);
                AssertPieceCapacitySnapshot(
                    roster,
                    expectedBenchCount: 9,
                    expectedBoardCount: 2);

                LuaTable overLimitRequest =
                    CreatePieceDeployRequested(runtime, 1, benchPieceIds[2], 103);
                try
                {
                    Assert.Throws<LuaException>(
                        () => CallModuleFunction(roster, "handle_event", overLimitRequest));
                }
                finally
                {
                    overLimitRequest.Dispose();
                }

                HandlePieceBenchRequested(runtime, roster, 1, benchPieceIds[0]);
                HandlePieceDeployRequested(runtime, roster, 1, benchPieceIds[2], 103);
                AssertPieceCapacitySnapshot(
                    roster,
                    expectedBenchCount: 9,
                    expectedBoardCount: 2);

                LuaTable events = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(events.Length, Is.EqualTo(4));
                    AssertLuaEventType(events, 1, "PieceDeployed");
                    AssertLuaEventType(events, 2, "PieceDeployed");
                    AssertLuaEventType(events, 3, "PieceBenched");
                    AssertLuaEventType(events, 4, "PieceDeployed");

                    LuaTable benchedEvent = events.Get<int, LuaTable>(3);
                    try
                    {
                        Assert.That(
                            benchedEvent.Get<int>("piece_instance_id"),
                            Is.EqualTo(benchPieceIds[0]));
                        Assert.That(benchedEvent.Get<int>("previous_cell_id"), Is.EqualTo(101));
                    }
                    finally
                    {
                        benchedEvent.Dispose();
                    }
                }
                finally
                {
                    events.Dispose();
                }
            }
            finally
            {
                roster?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void PieceRoster_MergesThreeMatchingPiecesAndCascades()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable roster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                roster = RequireTable(runtime, "Match.PieceRoster");
                CallModuleFunction(roster, "start");

                int firstPieceId =
                    CallModuleIntFunction(roster, "grant_piece", 1, "Sprout");
                HandlePieceDeployRequested(runtime, roster, 1, firstPieceId, 101);
                CallModuleIntFunction(roster, "grant_piece", 1, "Sprout");
                int firstMergeResult =
                    CallModuleIntFunction(roster, "grant_piece", 1, "Sprout");
                Assert.That(firstMergeResult, Is.EqualTo(firstPieceId));

                for (int index = 4; index <= 8; index++)
                {
                    CallModuleIntFunction(roster, "grant_piece", 1, "Sprout");
                }

                int cascadeResult =
                    CallModuleIntFunction(roster, "grant_piece", 1, "Sprout");
                Assert.That(cascadeResult, Is.EqualTo(firstPieceId));

                LuaTable snapshot = CallModuleTableFunction(roster, "get_snapshot");
                try
                {
                    LuaTable pieces = snapshot.Get<LuaTable>("pieces");
                    try
                    {
                        Assert.That(pieces.Length, Is.EqualTo(1));
                        LuaTable piece = pieces.Get<int, LuaTable>(1);
                        try
                        {
                            Assert.That(piece.Get<int>("instance_id"), Is.EqualTo(firstPieceId));
                            Assert.That(piece.Get<int>("level"), Is.EqualTo(3));
                            Assert.That(piece.Get<string>("location"), Is.EqualTo("Board"));
                            Assert.That(piece.Get<int>("cell_id"), Is.EqualTo(101));
                            Assert.That(piece.Get<int>("health"), Is.EqualTo(48));
                            Assert.That(piece.Get<int>("max_health"), Is.EqualTo(48));
                            Assert.That(piece.Get<int>("max_block_count"), Is.EqualTo(3));
                            Assert.That(piece.Get<int>("damage"), Is.EqualTo(16));
                            Assert.That(piece.Get<int>("sell_value"), Is.EqualTo(27));
                        }
                        finally
                        {
                            piece.Dispose();
                        }
                    }
                    finally
                    {
                        pieces.Dispose();
                    }

                    AssertPieceCapacityFields(
                        snapshot,
                        expectedBenchCount: 0,
                        expectedBoardCount: 1);
                }
                finally
                {
                    snapshot.Dispose();
                }

                LuaTable events = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(events.Length, Is.EqualTo(14));
                    AssertLuaEventType(events, 1, "PieceGranted");
                    AssertLuaEventType(events, 2, "PieceDeployed");
                    AssertLuaEventType(events, 5, "PiecesMerged");
                    AssertLuaEventType(events, 9, "PiecesMerged");
                    AssertLuaEventType(events, 13, "PiecesMerged");
                    AssertLuaEventType(events, 14, "PiecesMerged");

                    LuaTable finalMerge = events.Get<int, LuaTable>(14);
                    try
                    {
                        Assert.That(
                            finalMerge.Get<int>("survivor_piece_instance_id"),
                            Is.EqualTo(firstPieceId));
                        Assert.That(finalMerge.Get<int>("previous_level"), Is.EqualTo(2));
                        Assert.That(finalMerge.Get<int>("level"), Is.EqualTo(3));
                    }
                    finally
                    {
                        finalMerge.Dispose();
                    }
                }
                finally
                {
                    events.Dispose();
                }
            }
            finally
            {
                roster?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_BoardCompositionActivatesAndRemovesSynergies()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");

                int sproutId = runtime.GrantPiece(1, "Sprout");
                int brambleId = runtime.GrantPiece(1, "Bramble");
                int bloomId = runtime.GrantPiece(1, "Bloom");

                runtime.DeployPiece(1, sproutId, 101);
                runtime.DeployPiece(1, brambleId, 102);

                PieceRosterSnapshot arcanePairSnapshot = runtime.GetPieceRosterSnapshot();
                Assert.That(arcanePairSnapshot.ActiveSynergies.Count, Is.EqualTo(1));
                Assert.That(
                    arcanePairSnapshot.ActiveSynergies[0].SynergyId,
                    Is.EqualTo("Arcane"));
                Assert.That(arcanePairSnapshot.ActiveSynergies[0].UniquePieceCount, Is.EqualTo(2));
                Assert.That(arcanePairSnapshot.ActiveSynergies[0].DamageBonus, Is.EqualTo(3));
                Assert.That(arcanePairSnapshot.Pieces[0].BaseDamage, Is.EqualTo(4));
                Assert.That(arcanePairSnapshot.Pieces[0].Damage, Is.EqualTo(7));
                Assert.That(arcanePairSnapshot.Pieces[1].BaseDamage, Is.EqualTo(3));
                Assert.That(arcanePairSnapshot.Pieces[1].Damage, Is.EqualTo(6));
                Assert.That(arcanePairSnapshot.Pieces[2].Damage, Is.EqualTo(6));

                runtime.BenchPiece(1, brambleId);

                PieceRosterSnapshot removedSnapshot = runtime.GetPieceRosterSnapshot();
                Assert.That(removedSnapshot.ActiveSynergies, Is.Empty);
                Assert.That(removedSnapshot.Pieces[0].Damage, Is.EqualTo(4));
                Assert.That(removedSnapshot.Pieces[1].Damage, Is.EqualTo(3));

                runtime.DeployPiece(1, bloomId, 103);

                PieceRosterSnapshot arcaneSnapshot = runtime.GetPieceRosterSnapshot();
                Assert.That(arcaneSnapshot.ActiveSynergies.Count, Is.EqualTo(1));
                Assert.That(
                    arcaneSnapshot.ActiveSynergies[0].SynergyId,
                    Is.EqualTo("Arcane"));
                Assert.That(arcaneSnapshot.ActiveSynergies[0].DamageBonus, Is.EqualTo(3));
                Assert.That(arcaneSnapshot.Pieces[0].Damage, Is.EqualTo(7));
                Assert.That(arcaneSnapshot.Pieces[1].Damage, Is.EqualTo(3));
                Assert.That(arcaneSnapshot.Pieces[2].BaseDamage, Is.EqualTo(6));
                Assert.That(arcaneSnapshot.Pieces[2].Damage, Is.EqualTo(9));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void PieceAttackPlanner_UsesBoardPiecesCooldownsAndDeterministicTargets()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable pieceRoster = null;
            LuaTable enemyRoster = null;
            LuaTable planner = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                pieceRoster = RequireTable(runtime, "Match.PieceRoster");
                enemyRoster = RequireTable(runtime, "Match.EnemyRoster");
                planner = RequireTable(runtime, "Match.PieceAttackPlanner");

                CallModuleFunction(pieceRoster, "start");
                CallModuleFunction(enemyRoster, "start");
                CallModuleFunction(planner, "start");

                int boardPieceId = CallModuleIntFunction(
                    pieceRoster,
                    "grant_piece",
                    1,
                    "Sprout");
                CallModuleIntFunction(
                    pieceRoster,
                    "grant_piece",
                    1,
                    "Sprout");
                HandlePieceDeployRequested(
                    runtime,
                    pieceRoster,
                    playerId: 1,
                    pieceInstanceId: boardPieceId,
                    cellId: 1031);
                CallModuleTableFunction(pieceRoster, "drain_events").Dispose();

                LuaTable firstSpawn = CreateEvent(
                    runtime,
                    "EnemySpawnRequested",
                    wave: 1,
                    enemyId: "Crab",
                    spawnIndex: 1);
                try
                {
                    CallModuleFunction(enemyRoster, "handle_event", firstSpawn);
                }
                finally
                {
                    firstSpawn.Dispose();
                }

                CallModuleFunction(enemyRoster, "update", 1.6d);

                for (int spawnIndex = 2; spawnIndex <= 3; spawnIndex++)
                {
                    LuaTable spawnEvent = CreateEvent(
                        runtime,
                        "EnemySpawnRequested",
                        wave: 1,
                        enemyId: "Crab",
                        spawnIndex: spawnIndex);
                    try
                    {
                        CallModuleFunction(enemyRoster, "handle_event", spawnEvent);
                    }
                    finally
                    {
                        spawnEvent.Dispose();
                    }
                }

                CallModuleTableFunction(enemyRoster, "drain_events").Dispose();
                CallModuleFunction(planner, "begin_battle", 1);

                UpdatePieceAttackPlanner(planner, pieceRoster, enemyRoster, 0.1d);
                LuaTable firstAttackEvents = CallModuleTableFunction(planner, "drain_events");
                try
                {
                    Assert.That(firstAttackEvents.Length, Is.EqualTo(1));
                    AssertEnemyDamageRequested(
                        firstAttackEvents,
                        index: 1,
                        expectedWave: 1,
                        expectedEnemyInstanceId: 1,
                        expectedSourcePieceInstanceId: 1,
                        expectedDamage: 4);
                }
                finally
                {
                    firstAttackEvents.Dispose();
                }

                UpdatePieceAttackPlanner(planner, pieceRoster, enemyRoster, 0.5d);
                LuaTable coolingDownEvents = CallModuleTableFunction(planner, "drain_events");
                try
                {
                    Assert.That(coolingDownEvents.Length, Is.Zero);
                }
                finally
                {
                    coolingDownEvents.Dispose();
                }

                HandleEnemyDamageRequested(
                    runtime,
                    enemyRoster,
                    wave: 1,
                    enemyInstanceId: 1,
                    sourcePieceInstanceId: 99,
                    damage: 10);
                CallModuleTableFunction(enemyRoster, "drain_events").Dispose();
                CallModuleFunction(enemyRoster, "update", 1.6d);

                UpdatePieceAttackPlanner(planner, pieceRoster, enemyRoster, 0.5d);
                LuaTable retargetedEvents = CallModuleTableFunction(planner, "drain_events");
                try
                {
                    Assert.That(retargetedEvents.Length, Is.EqualTo(1));
                    AssertEnemyDamageRequested(
                        retargetedEvents,
                        index: 1,
                        expectedWave: 1,
                        expectedEnemyInstanceId: 2,
                        expectedSourcePieceInstanceId: 1,
                        expectedDamage: 4);
                }
                finally
                {
                    retargetedEvents.Dispose();
                }

                CallModuleFunction(planner, "end_battle");
                UpdatePieceAttackPlanner(planner, pieceRoster, enemyRoster, 10d);

                LuaTable inactiveEvents = CallModuleTableFunction(planner, "drain_events");
                try
                {
                    Assert.That(inactiveEvents.Length, Is.Zero);
                }
                finally
                {
                    inactiveEvents.Dispose();
                }
            }
            finally
            {
                if (planner != null)
                {
                    CallModuleFunction(planner, "shutdown");
                    planner.Dispose();
                }

                if (enemyRoster != null)
                {
                    CallModuleFunction(enemyRoster, "shutdown");
                    enemyRoster.Dispose();
                }

                if (pieceRoster != null)
                {
                    CallModuleFunction(pieceRoster, "shutdown");
                    pieceRoster.Dispose();
                }

                runtime.Dispose();
            }
        }

        [Test]
        public void PieceAttackPlanner_RespectsFacingAndConfiguredGridRange()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable pieceRoster = null;
            LuaTable enemyRoster = null;
            LuaTable planner = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                pieceRoster = RequireTable(runtime, "Match.PieceRoster");
                enemyRoster = RequireTable(runtime, "Match.EnemyRoster");
                planner = RequireTable(runtime, "Match.PieceAttackPlanner");
                CallModuleFunction(pieceRoster, "start");
                CallModuleFunction(enemyRoster, "start");
                CallModuleFunction(planner, "start");

                int pieceId = CallModuleIntFunction(pieceRoster, "grant_piece", 1, "Sprout");
                HandlePieceDeployRequested(runtime, pieceRoster, 1, pieceId, 1051);

                LuaTable spawnEvent = CreateEvent(
                    runtime,
                    "EnemySpawnRequested",
                    wave: 1,
                    enemyId: "Crab",
                    spawnIndex: 1);
                try
                {
                    CallModuleFunction(enemyRoster, "handle_event", spawnEvent);
                }
                finally
                {
                    spawnEvent.Dispose();
                }

                CallModuleFunction(enemyRoster, "update", 14.4d);
                CallModuleFunction(planner, "begin_battle", 1);
                UpdatePieceAttackPlanner(planner, pieceRoster, enemyRoster, 0.1d);
                LuaTable outOfRangeEvents =
                    CallModuleTableFunction(planner, "drain_events");
                try
                {
                    Assert.That(outOfRangeEvents.Length, Is.Zero);
                }
                finally
                {
                    outOfRangeEvents.Dispose();
                }

                HandlePieceFacingRequested(runtime, pieceRoster, 1, pieceId, "Down");
                UpdatePieceAttackPlanner(planner, pieceRoster, enemyRoster, 0.1d);
                LuaTable attackEvents = CallModuleTableFunction(planner, "drain_events");
                try
                {
                    Assert.That(attackEvents.Length, Is.EqualTo(1));
                    AssertEnemyDamageRequested(
                        attackEvents,
                        1,
                        expectedWave: 1,
                        expectedEnemyInstanceId: 1,
                        expectedSourcePieceInstanceId: 1,
                        expectedDamage: 4);
                }
                finally
                {
                    attackEvents.Dispose();
                }
            }
            finally
            {
                planner?.Dispose();
                enemyRoster?.Dispose();
                pieceRoster?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void BlockResolver_GroundPieceBlocksToCapacityAndEnemyAttacksIt()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable pieceRoster = null;
            LuaTable enemyRoster = null;
            LuaTable blockResolver = null;
            LuaTable enemyAttackPlanner = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                pieceRoster = RequireTable(runtime, "Match.PieceRoster");
                enemyRoster = RequireTable(runtime, "Match.EnemyRoster");
                blockResolver = RequireTable(runtime, "Match.BlockResolver");
                enemyAttackPlanner = RequireTable(runtime, "Match.EnemyAttackPlanner");

                CallModuleFunction(pieceRoster, "start");
                CallModuleFunction(enemyRoster, "start");
                CallModuleFunction(blockResolver, "start");
                CallModuleFunction(enemyAttackPlanner, "start");

                int groundPieceId = CallModuleIntFunction(
                    pieceRoster,
                    "grant_piece",
                    1,
                    "Sprout");
                int highGroundPieceId = CallModuleIntFunction(
                    pieceRoster,
                    "grant_piece",
                    1,
                    "Sprout");
                HandlePieceDeployRequested(runtime, pieceRoster, 1, groundPieceId, 1041);
                HandlePieceDeployRequested(runtime, pieceRoster, 1, highGroundPieceId, 103);
                CallModuleTableFunction(pieceRoster, "drain_events").Dispose();

                for (int spawnIndex = 1; spawnIndex <= 2; spawnIndex++)
                {
                    LuaTable spawnEvent = CreateEvent(
                        runtime,
                        "EnemySpawnRequested",
                        wave: 1,
                        enemyId: "Crab",
                        spawnIndex: spawnIndex);
                    try
                    {
                        CallModuleFunction(enemyRoster, "handle_event", spawnEvent);
                    }
                    finally
                    {
                        spawnEvent.Dispose();
                    }
                }

                CallModuleTableFunction(enemyRoster, "drain_events").Dispose();
                CallModuleFunction(enemyRoster, "update", 11.5d);
                CallModuleFunction(blockResolver, "begin_battle");

                UpdateBlockResolver(blockResolver, pieceRoster, enemyRoster, 11.5d);
                LuaTable blockEvents = CallModuleTableFunction(blockResolver, "drain_events");
                try
                {
                    Assert.That(blockEvents.Length, Is.EqualTo(1));
                    LuaTable blockEvent = blockEvents.Get<int, LuaTable>(1);
                    try
                    {
                        Assert.That(
                            blockEvent.Get<string>("type"),
                            Is.EqualTo("EnemyBlockRequested"));
                        Assert.That(blockEvent.Get<int>("enemy_instance_id"), Is.EqualTo(1));
                        Assert.That(blockEvent.Get<int>("piece_instance_id"), Is.EqualTo(1));
                        Assert.That(
                            blockEvent.Get<double>("block_progress"),
                            Is.EqualTo(0.5714286d).Within(0.0001d));

                        CallModuleFunction(enemyRoster, "handle_event", blockEvent);
                        CallModuleFunction(pieceRoster, "handle_event", blockEvent);
                    }
                    finally
                    {
                        blockEvent.Dispose();
                    }
                }
                finally
                {
                    blockEvents.Dispose();
                }

                CallModuleTableFunction(enemyRoster, "drain_events").Dispose();
                CallModuleFunction(enemyRoster, "update", 0.4d);

                LuaTable enemySnapshot = CallModuleTableFunction(enemyRoster, "get_snapshot");
                try
                {
                    LuaTable enemies = enemySnapshot.Get<LuaTable>("enemies");
                    try
                    {
                        LuaTable blockedEnemy = enemies.Get<int, LuaTable>(1);
                        LuaTable passingEnemy = enemies.Get<int, LuaTable>(2);
                        try
                        {
                            Assert.That(
                                blockedEnemy.Get<double>("path_progress"),
                                Is.EqualTo(0.5714286d).Within(0.0001d));
                            Assert.That(
                                blockedEnemy.Get<int>("blocked_by_piece_instance_id"),
                                Is.EqualTo(1));
                            Assert.That(
                                passingEnemy.Get<double>("path_progress"),
                                Is.EqualTo(0.595d).Within(0.0001d));
                            Assert.That(
                                passingEnemy.Get<object>("blocked_by_piece_instance_id"),
                                Is.Null);
                        }
                        finally
                        {
                            passingEnemy.Dispose();
                            blockedEnemy.Dispose();
                        }
                    }
                    finally
                    {
                        enemies.Dispose();
                    }
                }
                finally
                {
                    enemySnapshot.Dispose();
                }

                CallModuleFunction(enemyAttackPlanner, "begin_battle");
                UpdateEnemyAttackPlanner(
                    enemyAttackPlanner,
                    pieceRoster,
                    enemyRoster,
                    0.1d);
                LuaTable attackEvents =
                    CallModuleTableFunction(enemyAttackPlanner, "drain_events");
                try
                {
                    Assert.That(attackEvents.Length, Is.EqualTo(1));
                    LuaTable attackEvent = attackEvents.Get<int, LuaTable>(1);
                    try
                    {
                        Assert.That(
                            attackEvent.Get<string>("type"),
                            Is.EqualTo("PieceDamageRequested"));
                        Assert.That(attackEvent.Get<int>("piece_instance_id"), Is.EqualTo(1));
                        Assert.That(
                            attackEvent.Get<int>("source_enemy_instance_id"),
                            Is.EqualTo(1));
                        Assert.That(attackEvent.Get<int>("damage"), Is.EqualTo(3));
                        CallModuleFunction(pieceRoster, "handle_event", attackEvent);
                    }
                    finally
                    {
                        attackEvent.Dispose();
                    }
                }
                finally
                {
                    attackEvents.Dispose();
                }

                AssertSinglePieceCombatSnapshot(
                    pieceRoster,
                    pieceIndex: 1,
                    expectedHealth: 9,
                    expectedStatus: "Active",
                    expectedBlockedCount: 1,
                    expectedRecoverySeconds: 0d);
                AssertSinglePieceCombatSnapshot(
                    pieceRoster,
                    pieceIndex: 2,
                    expectedHealth: 12,
                    expectedStatus: "Active",
                    expectedBlockedCount: 0,
                    expectedRecoverySeconds: 0d);

                HandlePieceDamageRequested(runtime, pieceRoster, groundPieceId, 1, 9);
                CallModuleTableFunction(pieceRoster, "drain_events").Dispose();
                UpdateBlockResolver(blockResolver, pieceRoster, enemyRoster, 0d);

                LuaTable unblockEvents =
                    CallModuleTableFunction(blockResolver, "drain_events");
                try
                {
                    Assert.That(unblockEvents.Length, Is.EqualTo(1));
                    LuaTable unblockEvent = unblockEvents.Get<int, LuaTable>(1);
                    try
                    {
                        Assert.That(
                            unblockEvent.Get<string>("type"),
                            Is.EqualTo("EnemyUnblockRequested"));
                        CallModuleFunction(enemyRoster, "handle_event", unblockEvent);
                        CallModuleFunction(pieceRoster, "handle_event", unblockEvent);
                    }
                    finally
                    {
                        unblockEvent.Dispose();
                    }
                }
                finally
                {
                    unblockEvents.Dispose();
                }

                AssertSinglePieceCombatSnapshot(
                    pieceRoster,
                    pieceIndex: 1,
                    expectedHealth: 0,
                    expectedStatus: "Downed",
                    expectedBlockedCount: 0,
                    expectedRecoverySeconds: 3d);

                CallModuleTableFunction(enemyRoster, "drain_events").Dispose();
                CallModuleFunction(enemyRoster, "update", 0.1d);
                LuaTable releasedEnemySnapshot =
                    CallModuleTableFunction(enemyRoster, "get_snapshot");
                try
                {
                    LuaTable enemies = releasedEnemySnapshot.Get<LuaTable>("enemies");
                    try
                    {
                        LuaTable releasedEnemy = enemies.Get<int, LuaTable>(1);
                        try
                        {
                            Assert.That(
                                releasedEnemy.Get<object>("blocked_by_piece_instance_id"),
                                Is.Null);
                            Assert.That(
                                releasedEnemy.Get<double>("path_progress"),
                                Is.EqualTo(0.5764286d).Within(0.0001d));
                        }
                        finally
                        {
                            releasedEnemy.Dispose();
                        }
                    }
                    finally
                    {
                        enemies.Dispose();
                    }
                }
                finally
                {
                    releasedEnemySnapshot.Dispose();
                }
            }
            finally
            {
                enemyAttackPlanner?.Dispose();
                blockResolver?.Dispose();
                enemyRoster?.Dispose();
                pieceRoster?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void BlockResolver_SharedCellBlocksEnemiesFromEitherRoute()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable pieceRoster = null;
            LuaTable enemyRoster = null;
            LuaTable blockResolver = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                pieceRoster = RequireTable(runtime, "Match.PieceRoster");
                enemyRoster = RequireTable(runtime, "Match.EnemyRoster");
                blockResolver = RequireTable(runtime, "Match.BlockResolver");
                CallModuleFunction(pieceRoster, "start");
                CallModuleFunction(enemyRoster, "start");
                CallModuleFunction(blockResolver, "start");

                int pieceId = CallModuleIntFunction(
                    pieceRoster,
                    "grant_piece",
                    1,
                    "Sprout");
                HandlePieceDeployRequested(runtime, pieceRoster, 1, pieceId, 1027);

                LuaTable spawnEvent = CreateEvent(
                    runtime,
                    "EnemySpawnRequested",
                    wave: 1,
                    enemyId: "Crab",
                    spawnIndex: 1);
                spawnEvent.Set("route_id", 2);
                try
                {
                    CallModuleFunction(enemyRoster, "handle_event", spawnEvent);
                }
                finally
                {
                    spawnEvent.Dispose();
                }

                CallModuleFunction(enemyRoster, "update", 16.4d);
                CallModuleFunction(blockResolver, "begin_battle");
                UpdateBlockResolver(blockResolver, pieceRoster, enemyRoster, 0.1d);

                LuaTable blockEvents =
                    CallModuleTableFunction(blockResolver, "drain_events");
                try
                {
                    Assert.That(blockEvents.Length, Is.EqualTo(1));
                    LuaTable blockEvent = blockEvents.Get<int, LuaTable>(1);
                    try
                    {
                        Assert.That(
                            blockEvent.Get<double>("block_progress"),
                            Is.EqualTo(0.8181818d).Within(0.0001d));
                    }
                    finally
                    {
                        blockEvent.Dispose();
                    }
                }
                finally
                {
                    blockEvents.Dispose();
                }
            }
            finally
            {
                blockResolver?.Dispose();
                enemyRoster?.Dispose();
                pieceRoster?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void PieceRoster_DownedPieceRecoversByTimerAndBattleEnd()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable pieceRoster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                pieceRoster = RequireTable(runtime, "Match.PieceRoster");
                CallModuleFunction(pieceRoster, "start");

                int pieceInstanceId = CallModuleIntFunction(
                    pieceRoster,
                    "grant_piece",
                    1,
                    "Sprout");
                HandlePieceDeployRequested(runtime, pieceRoster, 1, pieceInstanceId, 101);
                CallModuleTableFunction(pieceRoster, "drain_events").Dispose();
                CallModuleFunction(pieceRoster, "begin_battle");

                HandlePieceDamageRequested(runtime, pieceRoster, pieceInstanceId, 7, 12);
                AssertSinglePieceCombatSnapshot(
                    pieceRoster,
                    pieceIndex: 1,
                    expectedHealth: 0,
                    expectedStatus: "Downed",
                    expectedBlockedCount: 0,
                    expectedRecoverySeconds: 3d);
                CallModuleTableFunction(pieceRoster, "drain_events").Dispose();

                CallModuleFunction(pieceRoster, "update", 2.9d);
                AssertSinglePieceCombatSnapshot(
                    pieceRoster,
                    pieceIndex: 1,
                    expectedHealth: 0,
                    expectedStatus: "Downed",
                    expectedBlockedCount: 0,
                    expectedRecoverySeconds: 0.1d);

                CallModuleFunction(pieceRoster, "update", 0.1d);
                AssertSinglePieceCombatSnapshot(
                    pieceRoster,
                    pieceIndex: 1,
                    expectedHealth: 12,
                    expectedStatus: "Active",
                    expectedBlockedCount: 0,
                    expectedRecoverySeconds: 0d);
                CallModuleTableFunction(pieceRoster, "drain_events").Dispose();

                HandlePieceDamageRequested(runtime, pieceRoster, pieceInstanceId, 8, 12);
                CallModuleTableFunction(pieceRoster, "drain_events").Dispose();
                CallModuleFunction(pieceRoster, "end_battle");

                AssertSinglePieceCombatSnapshot(
                    pieceRoster,
                    pieceIndex: 1,
                    expectedHealth: 12,
                    expectedStatus: "Active",
                    expectedBlockedCount: 0,
                    expectedRecoverySeconds: 0d);
            }
            finally
            {
                pieceRoster?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_DeployedPieceAttacksSpawnedEnemy()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable session = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                runtime.DrainMatchEvents();
                session = RequireTable(runtime, "Match.Session");

                int pieceInstanceId = runtime.GrantPiece(1, "Sprout");
                runtime.DeployPiece(1, pieceInstanceId, 1031);

                PieceRosterSnapshot pieceSnapshot = runtime.GetPieceRosterSnapshot();
                Assert.That(pieceSnapshot.Pieces.Count, Is.EqualTo(1));
                Assert.That(pieceSnapshot.Pieces[0].InstanceId, Is.EqualTo(1));
                Assert.That(pieceSnapshot.Pieces[0].Location, Is.EqualTo("Board"));
                Assert.That(pieceSnapshot.Pieces[0].CellId, Is.EqualTo(1031));
                Assert.That(pieceSnapshot.Pieces[0].Terrain, Is.EqualTo("Ground"));
                Assert.That(pieceSnapshot.Pieces[0].Facing, Is.EqualTo("Right"));
                Assert.That(pieceSnapshot.Pieces[0].Health, Is.EqualTo(12));
                Assert.That(pieceSnapshot.Pieces[0].MaxHealth, Is.EqualTo(12));
                Assert.That(pieceSnapshot.Pieces[0].Status, Is.EqualTo("Active"));
                Assert.That(pieceSnapshot.Pieces[0].MaxBlockCount, Is.EqualTo(1));
                Assert.That(pieceSnapshot.Pieces[0].BlockedEnemyInstanceIds, Is.Empty);

                PlayerRosterSnapshot playerSnapshot = runtime.GetPlayerRosterSnapshot();
                Assert.That(playerSnapshot.AliveCount, Is.EqualTo(1));
                Assert.That(playerSnapshot.Players.Count, Is.EqualTo(1));
                Assert.That(playerSnapshot.Players[0].PlayerId, Is.EqualTo(1));
                Assert.That(playerSnapshot.Players[0].Health, Is.EqualTo(10));
                Assert.That(playerSnapshot.Players[0].MaxHealth, Is.EqualTo(10));
                Assert.That(playerSnapshot.Players[0].Status, Is.EqualTo("Alive"));

                AdvanceSimulation(runtime, PreparationStepCount + 15);

                EnemyRosterSnapshot enemySnapshot = runtime.GetEnemyRosterSnapshot();
                Assert.That(enemySnapshot.Enemies.Count, Is.EqualTo(2));
                Assert.That(enemySnapshot.Enemies[0].Health, Is.EqualTo(2));
                Assert.That(enemySnapshot.Enemies[0].AttackDamage, Is.EqualTo(3));
                Assert.That(enemySnapshot.Enemies[0].AttackType, Is.EqualTo("Melee"));
                Assert.That(enemySnapshot.Enemies[0].RouteId, Is.EqualTo(1));
                Assert.That(enemySnapshot.Enemies[0].BlockedByPieceInstanceId, Is.Null);

                LuaTable events = CallModuleTableFunction(session, "drain_events");
                try
                {
                    Assert.That(events.Length, Is.EqualTo(11));
                    AssertLuaEventTypeCount(events, "PieceGranted", 1);
                    AssertLuaEventTypeCount(events, "PieceDeployed", 1);
                    AssertLuaEventTypeCount(events, "PhaseChanged", 1);
                    AssertLuaEventTypeCount(events, "EnemySpawnRequested", 2);
                    AssertLuaEventTypeCount(events, "EnemyCreated", 2);
                    AssertLuaEventTypeCount(events, "EnemyDamageRequested", 2);
                    AssertLuaEventTypeCount(events, "EnemyDamaged", 2);

                    int firstCreatedIndex =
                        FindLuaEventIndex(events, "EnemyCreated", occurrence: 1);
                    int firstDamageRequestIndex =
                        FindLuaEventIndex(events, "EnemyDamageRequested", occurrence: 1);
                    int firstDamagedIndex =
                        FindLuaEventIndex(events, "EnemyDamaged", occurrence: 1);
                    int secondDamageRequestIndex =
                        FindLuaEventIndex(events, "EnemyDamageRequested", occurrence: 2);
                    int secondDamagedIndex =
                        FindLuaEventIndex(events, "EnemyDamaged", occurrence: 2);

                    Assert.That(firstDamageRequestIndex, Is.GreaterThan(firstCreatedIndex));
                    Assert.That(firstDamagedIndex, Is.EqualTo(firstDamageRequestIndex + 1));
                    Assert.That(secondDamageRequestIndex, Is.GreaterThan(firstDamagedIndex));
                    Assert.That(secondDamagedIndex, Is.EqualTo(secondDamageRequestIndex + 1));

                    LuaTable firstDamageRequest =
                        events.Get<int, LuaTable>(firstDamageRequestIndex);
                    try
                    {
                        Assert.That(
                            firstDamageRequest.Get<int>("enemy_instance_id"),
                            Is.EqualTo(1));
                        Assert.That(
                            firstDamageRequest.Get<int>("source_piece_instance_id"),
                            Is.EqualTo(1));
                        Assert.That(firstDamageRequest.Get<int>("damage"), Is.EqualTo(4));
                    }
                    finally
                    {
                        firstDamageRequest.Dispose();
                    }

                    LuaTable firstDamagedEvent =
                        events.Get<int, LuaTable>(firstDamagedIndex);
                    try
                    {
                        Assert.That(
                            firstDamagedEvent.Get<int>("enemy_instance_id"),
                            Is.EqualTo(1));
                        Assert.That(firstDamagedEvent.Get<int>("health"), Is.EqualTo(6));
                    }
                    finally
                    {
                        firstDamagedEvent.Dispose();
                    }

                    LuaTable secondDamageRequest =
                        events.Get<int, LuaTable>(secondDamageRequestIndex);
                    LuaTable secondDamagedEvent =
                        events.Get<int, LuaTable>(secondDamagedIndex);
                    try
                    {
                        Assert.That(
                            secondDamageRequest.Get<int>("enemy_instance_id"),
                            Is.EqualTo(1));
                        Assert.That(
                            secondDamageRequest.Get<int>("source_piece_instance_id"),
                            Is.EqualTo(1));
                        Assert.That(secondDamageRequest.Get<int>("damage"), Is.EqualTo(4));
                        Assert.That(
                            secondDamagedEvent.Get<int>("enemy_instance_id"),
                            Is.EqualTo(1));
                        Assert.That(secondDamagedEvent.Get<int>("health"), Is.EqualTo(2));
                    }
                    finally
                    {
                        secondDamagedEvent.Dispose();
                        secondDamageRequest.Dispose();
                    }
                }
                finally
                {
                    events.Dispose();
                }
            }
            finally
            {
                session?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_DeploymentAndFacingCanOnlyChangeDuringPreparation()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                int pieceInstanceId = runtime.GrantPiece(1, "Sprout");
                runtime.DeployPiece(1, pieceInstanceId, 101);
                runtime.SetPieceFacing(1, pieceInstanceId, "Up");

                PieceSnapshot prepared = runtime.GetPieceRosterSnapshot().Pieces[0];
                Assert.That(prepared.CellId, Is.EqualTo(101));
                Assert.That(prepared.Facing, Is.EqualTo("Up"));

                AdvanceSimulation(runtime, PreparationStepCount);

                Assert.Throws<LuaException>(
                    () => runtime.DeployPiece(1, pieceInstanceId, 102));
                Assert.Throws<LuaException>(
                    () => runtime.BenchPiece(1, pieceInstanceId));
                Assert.Throws<LuaException>(
                    () => runtime.SellPiece(1, pieceInstanceId));
                Assert.Throws<LuaException>(
                    () => runtime.SetPieceFacing(1, pieceInstanceId, "Right"));
                Assert.Throws<LuaException>(
                    () => runtime.PlacePiece(1, pieceInstanceId, 102, "Right"));
                Assert.Throws<LuaException>(() => runtime.UpgradeShop(1));

                PieceSnapshot locked = runtime.GetPieceRosterSnapshot().Pieces[0];
                Assert.That(locked.CellId, Is.EqualTo(101));
                Assert.That(locked.Facing, Is.EqualTo("Up"));
                Assert.That(runtime.GetShopSnapshot(1).Level, Is.EqualTo(1));
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(10));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_PlacePieceCommitsTargetAndFacingAtomically()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                int pieceInstanceId = runtime.GrantPiece(1, "Sprout");

                Assert.Throws<LuaException>(
                    () => runtime.PlacePiece(1, pieceInstanceId, 102, "Diagonal"));

                PieceSnapshot unchanged = runtime.GetPieceRosterSnapshot().Pieces[0];
                Assert.That(unchanged.Location, Is.EqualTo("Bench"));
                Assert.That(unchanged.CellId, Is.EqualTo(1001));
                Assert.That(unchanged.Facing, Is.EqualTo("Right"));
                Assert.That(
                    unchanged.DeployableTerrains,
                    Is.EquivalentTo(new[] { "Ground", "HighGround" }));

                runtime.PlacePiece(1, pieceInstanceId, 102, "Up");

                PieceSnapshot deployed = runtime.GetPieceRosterSnapshot().Pieces[0];
                Assert.That(deployed.Location, Is.EqualTo("Board"));
                Assert.That(deployed.CellId, Is.EqualTo(102));
                Assert.That(deployed.Facing, Is.EqualTo("Up"));

                runtime.PlacePiece(1, pieceInstanceId, 1005, null);

                PieceSnapshot benched = runtime.GetPieceRosterSnapshot().Pieces[0];
                Assert.That(benched.Location, Is.EqualTo("Bench"));
                Assert.That(benched.CellId, Is.EqualTo(1005));
                Assert.That(benched.Facing, Is.EqualTo("Up"));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_AllAlivePlayersReadyEndsEachPreparationEarly()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                DeployBossVictoryBoard(runtime);

                for (int wave = 1; wave <= 5; wave++)
                {
                    AssertSnapshot(
                        runtime,
                        "Preparation",
                        wave,
                        PreparationSeconds,
                        false);
                    Assert.That(
                        runtime.GetPlayerRosterSnapshot().Players[0].IsReady,
                        Is.False);

                    runtime.SetPlayerReady(1, true);

                    AssertSnapshot(runtime, "Battle", wave, 0d, false);
                    Assert.That(
                        runtime.GetPlayerRosterSnapshot().Players[0].IsReady,
                        Is.True);
                    Assert.Throws<LuaException>(
                        () => runtime.SetPlayerReady(1, false));

                    AdvanceUntilPhase(
                        runtime,
                        "Settlement",
                        maxStepCount: ReadyBattleSettlementStepBudget);
                    string nextPreparation =
                        wave < 5 ? "Preparation" : "BossPreparation";
                    AdvanceUntilPhase(runtime, nextPreparation, maxStepCount: 30);
                }

                AssertSnapshot(
                    runtime,
                    "BossPreparation",
                    6,
                    PreparationSeconds,
                    false);
                Assert.That(
                    runtime.GetPlayerRosterSnapshot().Players[0].IsReady,
                    Is.False);

                runtime.SetPlayerReady(1, true);
                AssertSnapshot(runtime, "BossBattle", 6, 0d, false);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_LocalMultiplayerUsesPrivateShopsAndSpawnsForEachAlivePlayer()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                runtime.StartLocalMultiplayer(2);

                PlayerRosterSnapshot players = runtime.GetPlayerRosterSnapshot();
                Assert.That(players.AliveCount, Is.EqualTo(2));
                Assert.That(players.Players.Count, Is.EqualTo(2));
                Assert.That(players.Players[0].PlayerId, Is.EqualTo(1));
                Assert.That(players.Players[1].PlayerId, Is.EqualTo(2));

                PieceRosterSnapshot pieces = runtime.GetPieceRosterSnapshot();
                Assert.That(pieces.Players.Count, Is.EqualTo(2));

                ShopSnapshot playerOneShop = runtime.GetShopSnapshot(1);
                ShopSnapshot playerTwoShop = runtime.GetShopSnapshot(2);
                Assert.That(playerOneShop.PlayerId, Is.EqualTo(1));
                Assert.That(playerTwoShop.PlayerId, Is.EqualTo(2));

                runtime.PurchaseShopOffer(1, 1);
                Assert.That(runtime.GetShopSnapshot(1).Offers[0].IsSold, Is.True);
                Assert.That(runtime.GetShopSnapshot(2).Offers[0].IsSold, Is.False);
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(7));
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[1].Gold, Is.EqualTo(10));

                runtime.SetPlayerReady(1, true);
                AssertSnapshot(runtime, "Preparation", 1, PreparationSeconds, false);
                Assert.That(
                    runtime.GetPlayerRosterSnapshot().Players[0].IsReady,
                    Is.True);
                Assert.That(
                    runtime.GetPlayerRosterSnapshot().Players[1].IsReady,
                    Is.False);

                runtime.SetPlayerReady(2, true);
                AssertSnapshot(runtime, "Battle", 1, 0d, false);

                runtime.Tick((float)SimulationSettings.StepSeconds);
                EnemyRosterSnapshot enemies = runtime.GetEnemyRosterSnapshot();
                Assert.That(enemies.AliveCount, Is.EqualTo(2));
                Assert.That(enemies.Enemies.Count, Is.EqualTo(2));
                Assert.That(enemies.Enemies[0].TargetPlayerId, Is.EqualTo(1));
                Assert.That(enemies.Enemies[1].TargetPlayerId, Is.EqualTo(2));
                Assert.That(enemies.Enemies[0].SpawnIndex, Is.EqualTo(1));
                Assert.That(enemies.Enemies[1].SpawnIndex, Is.EqualTo(1));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_ShopPurchaseRefreshAndRoundRewardUseGold()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");

                ShopSnapshot initialShop = runtime.GetShopSnapshot(1);
                Assert.That(initialShop.RefreshCost, Is.EqualTo(2));
                Assert.That(initialShop.Offers.Count, Is.EqualTo(2));
                Assert.That(initialShop.Offers[0].PieceId, Is.EqualTo("Sprout"));
                Assert.That(initialShop.Offers[0].DisplayName, Is.EqualTo("奥术士兵"));
                Assert.That(initialShop.Offers[0].Portrait, Is.EqualTo("soldier_arcane"));
                Assert.That(initialShop.Offers[0].ClassId, Is.EqualTo("sword"));
                Assert.That(initialShop.Offers[0].Synergies.Count, Is.EqualTo(1));
                Assert.That(initialShop.Offers[0].Cost, Is.EqualTo(3));
                Assert.That(initialShop.Offers[0].IsSold, Is.False);
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(10));

                int purchasedPieceId = runtime.PurchaseShopOffer(1, 1);
                Assert.That(purchasedPieceId, Is.EqualTo(1));
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(7));
                Assert.That(runtime.GetShopSnapshot(1).Offers[0].IsSold, Is.True);
                Assert.That(runtime.GetPieceRosterSnapshot().Pieces.Count, Is.EqualTo(1));

                runtime.RefreshShop(1);
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(5));
                Assert.That(runtime.GetShopSnapshot(1).Offers[0].IsSold, Is.False);

                AdvanceSimulation(runtime, PreparationStepCount);
                Assert.Throws<LuaException>(() => runtime.RefreshShop(1));
                Assert.Throws<LuaException>(() => runtime.PurchaseShopOffer(1, 1));

                AdvanceUntilPhase(
                    runtime,
                    "Settlement",
                    maxStepCount: PreparationStepCount + SingleWaveSettlementStepBudget);
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(10));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_FullBenchRejectsPurchaseWithoutSpendingGold()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");

                FillBenchWithMergedSprouts(runtime);

                Assert.Throws<LuaException>(() => runtime.PurchaseShopOffer(1, 1));
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(10));
                Assert.That(runtime.GetShopSnapshot(1).Offers[0].IsSold, Is.False);

                PieceRosterSnapshot pieces = runtime.GetPieceRosterSnapshot();
                Assert.That(pieces.Pieces.Count, Is.EqualTo(11));
                Assert.That(pieces.Players.Count, Is.EqualTo(1));
                Assert.That(pieces.Players[0].BenchCount, Is.EqualTo(11));
                Assert.That(pieces.Players[0].BenchCapacity, Is.EqualTo(11));
                Assert.That(pieces.Players[0].TemporaryBenchCount, Is.Zero);
                Assert.That(pieces.Players[0].TemporaryBenchCapacity, Is.EqualTo(11));
                Assert.That(pieces.Players[0].BoardCount, Is.Zero);
                Assert.That(pieces.Players[0].DeploymentLimit, Is.EqualTo(2));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_OverflowGrantUsesTemporaryReserveAndCanBeResolved()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                FillBenchWithMergedSprouts(runtime);

                int overflowPieceId = runtime.GrantOverflowPiece(1, "Bloom");
                PieceRosterSnapshot overflowSnapshot = runtime.GetPieceRosterSnapshot();
                PieceSnapshot overflowPiece = FindPiece(overflowSnapshot, overflowPieceId);
                Assert.That(overflowPiece, Is.Not.Null);
                Assert.That(overflowPiece.Location, Is.EqualTo("TemporaryReserve"));
                Assert.That(overflowPiece.CellId, Is.EqualTo(1012));
                Assert.That(overflowSnapshot.Players[0].BenchCount, Is.EqualTo(11));
                Assert.That(overflowSnapshot.Players[0].TemporaryBenchCount, Is.EqualTo(1));

                PieceSnapshot normalPiece = overflowSnapshot.Pieces[0];
                int freedReserveCellId = normalPiece.CellId.Value;
                runtime.DeployPiece(1, normalPiece.InstanceId, 101);
                runtime.PlacePiece(1, overflowPieceId, freedReserveCellId, null);

                PieceRosterSnapshot resolved = runtime.GetPieceRosterSnapshot();
                PieceSnapshot resolvedPiece = FindPiece(resolved, overflowPieceId);
                Assert.That(resolvedPiece.Location, Is.EqualTo("Bench"));
                Assert.That(resolvedPiece.CellId, Is.EqualTo(freedReserveCellId));
                Assert.That(resolved.Players[0].BenchCount, Is.EqualTo(11));
                Assert.That(resolved.Players[0].TemporaryBenchCount, Is.Zero);
                Assert.That(resolved.Players[0].BoardCount, Is.EqualTo(1));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_BattleStartAutoSellsTemporaryReservePieces()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable session = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                FillBenchWithMergedSprouts(runtime);
                int overflowPieceId = runtime.GrantOverflowPiece(1, "Bloom");
                runtime.DrainMatchEvents();
                session = RequireTable(runtime, "Match.Session");

                runtime.SetPlayerReady(1, true);

                PieceRosterSnapshot pieces = runtime.GetPieceRosterSnapshot();
                Assert.That(FindPiece(pieces, overflowPieceId), Is.Null);
                Assert.That(pieces.Players[0].BenchCount, Is.EqualTo(11));
                Assert.That(pieces.Players[0].TemporaryBenchCount, Is.Zero);
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(15));

                LuaTable events = CallModuleTableFunction(session, "drain_events");
                try
                {
                    AssertLuaEventTypeCount(events, "PieceSold", 1);
                    AssertLuaEventTypeCount(events, "GoldGranted", 1);
                    AssertLuaEventTypeCount(events, "PhaseChanged", 1);

                    LuaTable soldEvent = events.Get<int, LuaTable>(
                        FindLuaEventIndex(events, "PieceSold", 1));
                    LuaTable goldEvent = events.Get<int, LuaTable>(
                        FindLuaEventIndex(events, "GoldGranted", 1));
                    try
                    {
                        Assert.That(
                            soldEvent.Get<string>("reason"),
                            Is.EqualTo("TemporaryReserveAutoSell"));
                        Assert.That(
                            goldEvent.Get<string>("reason"),
                            Is.EqualTo("TemporaryReserveAutoSell"));
                        Assert.That(goldEvent.Get<int>("amount"), Is.EqualTo(5));
                    }
                    finally
                    {
                        goldEvent.Dispose();
                        soldEvent.Dispose();
                    }
                }
                finally
                {
                    events.Dispose();
                }
            }
            finally
            {
                session?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_SettlementRefreshesUnlockedShopForFree()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                runtime.PurchaseShopOffer(1, 1);

                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(7));
                Assert.That(runtime.GetShopSnapshot(1).Offers[0].IsSold, Is.True);

                AdvanceUntilPhase(
                    runtime,
                    "Settlement",
                    maxStepCount: PreparationStepCount + SingleWaveSettlementStepBudget);

                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(12));
                Assert.That(runtime.GetShopSnapshot(1).Offers[0].IsSold, Is.False);
                Assert.That(runtime.GetShopSnapshot(1).IsLocked, Is.False);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_LockedShopSkipsSettlementRefresh()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");
                runtime.PurchaseShopOffer(1, 1);

                Assert.That(runtime.ToggleShopLock(1), Is.True);
                Assert.That(runtime.GetShopSnapshot(1).IsLocked, Is.True);

                AdvanceUntilPhase(
                    runtime,
                    "Settlement",
                    maxStepCount: PreparationStepCount + SingleWaveSettlementStepBudget);

                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(12));
                Assert.That(runtime.GetShopSnapshot(1).Offers[0].IsSold, Is.True);
                Assert.That(runtime.GetShopSnapshot(1).IsLocked, Is.True);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_ManualRefreshPreservesShopLock()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");

                Assert.That(runtime.ToggleShopLock(1), Is.True);
                runtime.RefreshShop(1);

                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(8));
                Assert.That(runtime.GetShopSnapshot(1).IsLocked, Is.True);
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_ShopUpgradeIncreasesDeploymentLimit()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();

            try
            {
                runtime.Start("Bootstrap.Main");

                runtime.GrantPiece(1, "Sprout");
                runtime.GrantPiece(1, "Sprout");
                int levelTwoPieceId = runtime.GrantPiece(1, "Sprout");
                int firstLevelOnePieceId = runtime.GrantPiece(1, "Sprout");
                int secondLevelOnePieceId = runtime.GrantPiece(1, "Sprout");

                runtime.DeployPiece(1, levelTwoPieceId, 101);
                runtime.DeployPiece(1, firstLevelOnePieceId, 102);
                Assert.Throws<LuaException>(
                    () => runtime.DeployPiece(1, secondLevelOnePieceId, 103));

                ShopSnapshot levelOne = runtime.GetShopSnapshot(1);
                Assert.That(levelOne.Level, Is.EqualTo(1));
                Assert.That(levelOne.MaxLevel, Is.EqualTo(3));
                Assert.That(levelOne.CanUpgrade, Is.True);
                Assert.That(levelOne.UpgradeCost, Is.EqualTo(4));

                runtime.UpgradeShop(1);

                ShopSnapshot levelTwo = runtime.GetShopSnapshot(1);
                Assert.That(levelTwo.Level, Is.EqualTo(2));
                Assert.That(levelTwo.CanUpgrade, Is.True);
                Assert.That(levelTwo.UpgradeCost, Is.EqualTo(6));
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(6));
                Assert.That(
                    runtime.GetPieceRosterSnapshot().Players[0].DeploymentLimit,
                    Is.EqualTo(3));

                runtime.DeployPiece(1, secondLevelOnePieceId, 103);
                Assert.That(
                    runtime.GetPieceRosterSnapshot().Players[0].BoardCount,
                    Is.EqualTo(3));

                runtime.UpgradeShop(1);

                ShopSnapshot levelThree = runtime.GetShopSnapshot(1);
                Assert.That(levelThree.Level, Is.EqualTo(3));
                Assert.That(levelThree.CanUpgrade, Is.False);
                Assert.That(levelThree.UpgradeCost, Is.Zero);
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.Zero);
                Assert.That(
                    runtime.GetPieceRosterSnapshot().Players[0].DeploymentLimit,
                    Is.EqualTo(4));

                Assert.Throws<LuaException>(() => runtime.UpgradeShop(1));
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.Zero);
                Assert.That(runtime.GetShopSnapshot(1).Level, Is.EqualTo(3));
            }
            finally
            {
                runtime.Dispose();
            }
        }

        [Test]
        public void ShopRoster_ShopLevelChangesRarityWeightsOnLaterRefreshes()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable shopRoster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                shopRoster = RequireTable(runtime, "Match.ShopRoster");

                ShopSnapshot levelOne = runtime.GetShopSnapshot(1);
                Assert.That(levelOne.RarityWeights.Count, Is.EqualTo(6));
                Assert.That(levelOne.RarityWeights[0].Weight, Is.EqualTo(100));
                Assert.That(levelOne.RarityWeights[1].Weight, Is.Zero);
                Assert.That(levelOne.RarityWeights[2].Weight, Is.Zero);
                Assert.That(levelOne.RarityWeights[5].Weight, Is.Zero);
                Assert.That(levelOne.Offers.Count, Is.EqualTo(2));
                Assert.That(levelOne.Offers[0].PieceId, Is.EqualTo("Sprout"));
                Assert.That(levelOne.Offers[0].Rarity, Is.EqualTo(1));

                CallModuleFunction(shopRoster, "upgrade", 1);

                ShopSnapshot levelTwoBeforeRefresh = runtime.GetShopSnapshot(1);
                Assert.That(levelTwoBeforeRefresh.Level, Is.EqualTo(2));
                Assert.That(levelTwoBeforeRefresh.RarityWeights[0].Weight, Is.EqualTo(75));
                Assert.That(levelTwoBeforeRefresh.RarityWeights[1].Weight, Is.EqualTo(25));
                Assert.That(levelTwoBeforeRefresh.RarityWeights[2].Weight, Is.Zero);
                Assert.That(levelTwoBeforeRefresh.Offers[0].PieceId, Is.EqualTo("Sprout"));

                CallModuleFunction(shopRoster, "refresh", 1);

                ShopSnapshot levelTwoAfterRefresh = runtime.GetShopSnapshot(1);
                Assert.That(levelTwoAfterRefresh.Offers.Count, Is.EqualTo(3));
                Assert.That(levelTwoAfterRefresh.Offers[0].PieceId, Is.EqualTo("Bramble"));
                Assert.That(levelTwoAfterRefresh.Offers[0].Rarity, Is.EqualTo(2));

                CallModuleFunction(shopRoster, "upgrade", 1);

                ShopSnapshot levelThreeBeforeRefresh = runtime.GetShopSnapshot(1);
                Assert.That(levelThreeBeforeRefresh.Level, Is.EqualTo(3));
                Assert.That(levelThreeBeforeRefresh.RarityWeights[0].Weight, Is.EqualTo(60));
                Assert.That(levelThreeBeforeRefresh.RarityWeights[1].Weight, Is.EqualTo(30));
                Assert.That(levelThreeBeforeRefresh.RarityWeights[2].Weight, Is.EqualTo(6));
                Assert.That(levelThreeBeforeRefresh.RarityWeights[3].Weight, Is.EqualTo(2));
                Assert.That(levelThreeBeforeRefresh.RarityWeights[4].Weight, Is.EqualTo(1));
                Assert.That(levelThreeBeforeRefresh.RarityWeights[5].Weight, Is.EqualTo(1));
                Assert.That(levelThreeBeforeRefresh.Offers[0].PieceId, Is.EqualTo("Bramble"));

                CallModuleFunction(shopRoster, "refresh", 1);
                CallModuleFunction(shopRoster, "refresh", 1);

                ShopSnapshot levelThreeAfterRefresh = runtime.GetShopSnapshot(1);
                Assert.That(levelThreeAfterRefresh.Offers.Count, Is.EqualTo(4));
                Assert.That(levelThreeAfterRefresh.Offers[2].PieceId, Is.EqualTo("Bloom"));
                Assert.That(levelThreeAfterRefresh.Offers[2].Rarity, Is.EqualTo(3));
            }
            finally
            {
                shopRoster?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void MatchSession_SellsBenchAndBoardPiecesForGold()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable session = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                runtime.DrainMatchEvents();
                session = RequireTable(runtime, "Match.Session");

                int benchPieceId = runtime.PurchaseShopOffer(1, 1);
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(7));
                Assert.That(runtime.SellPiece(1, benchPieceId), Is.EqualTo(3));
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(10));

                PieceRosterSnapshot afterBenchSale = runtime.GetPieceRosterSnapshot();
                Assert.That(afterBenchSale.Pieces, Is.Empty);
                Assert.That(afterBenchSale.Players[0].BenchCount, Is.Zero);
                Assert.That(afterBenchSale.Players[0].BoardCount, Is.Zero);

                int boardPieceId = runtime.PurchaseShopOffer(1, 2);
                runtime.DeployPiece(1, boardPieceId, 101);
                Assert.That(runtime.SellPiece(1, boardPieceId), Is.EqualTo(3));

                PieceRosterSnapshot afterBoardSale = runtime.GetPieceRosterSnapshot();
                Assert.That(afterBoardSale.Pieces, Is.Empty);
                Assert.That(afterBoardSale.Players[0].BenchCount, Is.Zero);
                Assert.That(afterBoardSale.Players[0].BoardCount, Is.Zero);
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(10));

                Assert.Throws<LuaException>(() => runtime.SellPiece(1, boardPieceId));
                Assert.That(runtime.GetPlayerRosterSnapshot().Players[0].Gold, Is.EqualTo(10));

                int nextPieceId = runtime.GrantPiece(1, "Sprout");
                Assert.That(nextPieceId, Is.EqualTo(3));
                Assert.That(runtime.GetPieceRosterSnapshot().Pieces[0].SellValue, Is.EqualTo(3));

                LuaTable events = CallModuleTableFunction(session, "drain_events");
                try
                {
                    Assert.That(events.Length, Is.EqualTo(12));
                    AssertLuaEventType(events, 1, "GoldSpent");
                    AssertLuaEventType(events, 2, "ShopOfferPurchased");
                    AssertLuaEventType(events, 3, "PieceGranted");
                    AssertLuaEventType(events, 4, "PieceSold");
                    AssertLuaEventType(events, 5, "GoldGranted");
                    AssertLuaEventType(events, 6, "GoldSpent");
                    AssertLuaEventType(events, 7, "ShopOfferPurchased");
                    AssertLuaEventType(events, 8, "PieceGranted");
                    AssertLuaEventType(events, 9, "PieceDeployed");
                    AssertLuaEventType(events, 10, "PieceSold");
                    AssertLuaEventType(events, 11, "GoldGranted");
                    AssertLuaEventType(events, 12, "PieceGranted");
                }
                finally
                {
                    events.Dispose();
                }
            }
            finally
            {
                session?.Dispose();
                runtime.Dispose();
            }
        }

        [Test]
        public void PlayerRoster_CreatesConfiguredPlayersAndReturnsCopies()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable roster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                roster = RequireTable(runtime, "Match.PlayerRoster");
                CallModuleFunction(roster, "start");

                AssertPlayerRosterSnapshot(
                    roster,
                    expectedHealth: 10,
                    expectedStatus: "Alive",
                    expectedAliveCount: 1);
                Assert.That(CallModuleIntFunction(roster, "get_alive_count"), Is.EqualTo(1));

                LuaTable mutableSnapshot = CallModuleTableFunction(roster, "get_snapshot");
                try
                {
                    LuaTable players = mutableSnapshot.Get<LuaTable>("players");
                    try
                    {
                        LuaTable firstPlayer = players.Get<int, LuaTable>(1);
                        try
                        {
                            firstPlayer.Set("health", 0);
                        }
                        finally
                        {
                            firstPlayer.Dispose();
                        }
                    }
                    finally
                    {
                        players.Dispose();
                    }
                }
                finally
                {
                    mutableSnapshot.Dispose();
                }

                AssertPlayerRosterSnapshot(
                    roster,
                    expectedHealth: 10,
                    expectedStatus: "Alive",
                    expectedAliveCount: 1);
            }
            finally
            {
                if (roster != null)
                {
                    CallModuleFunction(roster, "shutdown");
                    roster.Dispose();
                }

                runtime.Dispose();
            }
        }

        [Test]
        public void PlayerRoster_ResolvedLeaksDamageAndEliminateOnce()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable roster = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                roster = RequireTable(runtime, "Match.PlayerRoster");
                CallModuleFunction(roster, "start");

                LuaTable endpointEvent = CreatePlayerEndpointEvent(
                    runtime,
                    playerId: 1,
                    wave: 1,
                    enemyInstanceId: 1);
                try
                {
                    CallModuleFunction(roster, "handle_event", endpointEvent);
                }
                finally
                {
                    endpointEvent.Dispose();
                }

                AssertPlayerRosterSnapshot(
                    roster,
                    expectedHealth: 10,
                    expectedStatus: "Alive",
                    expectedAliveCount: 1);

                LuaTable noEndpointDamageEvents = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(noEndpointDamageEvents.Length, Is.Zero);
                }
                finally
                {
                    noEndpointDamageEvents.Dispose();
                }

                HandlePlayerLeakResolvedEvent(
                    runtime,
                    roster,
                    playerId: 1,
                    wave: 1,
                    initialLeakCount: 5,
                    rescuedCount: 5,
                    finalLeakCount: 0);
                HandlePlayerLeakResolvedEvent(
                    runtime,
                    roster,
                    playerId: 1,
                    wave: 2,
                    initialLeakCount: 5,
                    rescuedCount: 2,
                    finalLeakCount: 3);
                HandlePlayerLeakResolvedEvent(
                    runtime,
                    roster,
                    playerId: 1,
                    wave: 3,
                    initialLeakCount: 10,
                    rescuedCount: 0,
                    finalLeakCount: 10);
                HandlePlayerLeakResolvedEvent(
                    runtime,
                    roster,
                    playerId: 1,
                    wave: 4,
                    initialLeakCount: 1,
                    rescuedCount: 0,
                    finalLeakCount: 1);

                AssertPlayerRosterSnapshot(
                    roster,
                    expectedHealth: 0,
                    expectedStatus: "Eliminated",
                    expectedAliveCount: 0);
                Assert.That(CallModuleIntFunction(roster, "get_alive_count"), Is.Zero);

                LuaTable events = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(events.Length, Is.EqualTo(3));

                    LuaTable firstDamageEvent = events.Get<int, LuaTable>(1);
                    try
                    {
                        Assert.That(
                            firstDamageEvent.Get<string>("type"),
                            Is.EqualTo("PlayerDamaged"));
                        Assert.That(firstDamageEvent.Get<int>("player_id"), Is.EqualTo(1));
                        Assert.That(firstDamageEvent.Get<int>("damage"), Is.EqualTo(3));
                        Assert.That(firstDamageEvent.Get<int>("leak_count"), Is.EqualTo(3));
                        Assert.That(firstDamageEvent.Get<int>("health"), Is.EqualTo(7));
                        Assert.That(firstDamageEvent.Get<int>("wave"), Is.EqualTo(2));
                    }
                    finally
                    {
                        firstDamageEvent.Dispose();
                    }

                    LuaTable lethalDamageEvent = events.Get<int, LuaTable>(2);
                    try
                    {
                        Assert.That(
                            lethalDamageEvent.Get<string>("type"),
                            Is.EqualTo("PlayerDamaged"));
                        Assert.That(lethalDamageEvent.Get<int>("player_id"), Is.EqualTo(1));
                        Assert.That(lethalDamageEvent.Get<int>("damage"), Is.EqualTo(7));
                        Assert.That(lethalDamageEvent.Get<int>("leak_count"), Is.EqualTo(10));
                        Assert.That(lethalDamageEvent.Get<int>("health"), Is.Zero);
                        Assert.That(lethalDamageEvent.Get<int>("wave"), Is.EqualTo(3));
                    }
                    finally
                    {
                        lethalDamageEvent.Dispose();
                    }

                    LuaTable eliminatedEvent = events.Get<int, LuaTable>(3);
                    try
                    {
                        Assert.That(
                            eliminatedEvent.Get<string>("type"),
                            Is.EqualTo("PlayerEliminated"));
                        Assert.That(eliminatedEvent.Get<int>("player_id"), Is.EqualTo(1));
                        Assert.That(eliminatedEvent.Get<int>("health"), Is.Zero);
                        Assert.That(eliminatedEvent.Get<int>("wave"), Is.EqualTo(3));
                    }
                    finally
                    {
                        eliminatedEvent.Dispose();
                    }
                }
                finally
                {
                    events.Dispose();
                }

                LuaTable drainedAgain = CallModuleTableFunction(roster, "drain_events");
                try
                {
                    Assert.That(drainedAgain.Length, Is.Zero);
                }
                finally
                {
                    drainedAgain.Dispose();
                }

                CallModuleFunction(roster, "start");
                AssertPlayerRosterSnapshot(
                    roster,
                    expectedHealth: 10,
                    expectedStatus: "Alive",
                    expectedAliveCount: 1);
            }
            finally
            {
                if (roster != null)
                {
                    CallModuleFunction(roster, "shutdown");
                    roster.Dispose();
                }

                runtime.Dispose();
            }
        }

        [Test]
        public void LeakResolver_RecordsRescuesAndEmitsResolvedResultsOnce()
        {
            LuaRuntime runtime = LuaRuntime.CreateForProjectScripts();
            LuaTable resolver = null;

            try
            {
                runtime.Start("Bootstrap.Main");
                resolver = RequireTable(runtime, "Match.LeakResolver");
                CallModuleFunction(resolver, "start");
                CallModuleFunction(resolver, "begin_wave", 1);

                for (int enemyInstanceId = 1; enemyInstanceId <= 5; enemyInstanceId++)
                {
                    LuaTable endpointEvent = CreatePlayerEndpointEvent(
                        runtime,
                        playerId: 1,
                        wave: 1,
                        enemyInstanceId: enemyInstanceId);

                    try
                    {
                        CallModuleFunction(resolver, "handle_event", endpointEvent);
                    }
                    finally
                    {
                        endpointEvent.Dispose();
                    }
                }

                HandleLeakedEnemyRescuedEvent(runtime, resolver, wave: 1, enemyInstanceId: 2);
                HandleLeakedEnemyRescuedEvent(runtime, resolver, wave: 1, enemyInstanceId: 5);

                AssertLeakResolverSnapshot(
                    resolver,
                    expectedWave: 1,
                    expectedResolved: false,
                    expectedInitialLeaks: 5,
                    expectedRescued: 2,
                    expectedFinalLeaks: 3);

                LuaTable mutableSnapshot = CallModuleTableFunction(resolver, "get_snapshot");
                try
                {
                    LuaTable players = mutableSnapshot.Get<LuaTable>("players");
                    try
                    {
                        LuaTable firstPlayer = players.Get<int, LuaTable>(1);
                        try
                        {
                            firstPlayer.Set("final_leak_count", 0);
                        }
                        finally
                        {
                            firstPlayer.Dispose();
                        }
                    }
                    finally
                    {
                        players.Dispose();
                    }
                }
                finally
                {
                    mutableSnapshot.Dispose();
                }

                AssertLeakResolverSnapshot(
                    resolver,
                    expectedWave: 1,
                    expectedResolved: false,
                    expectedInitialLeaks: 5,
                    expectedRescued: 2,
                    expectedFinalLeaks: 3);

                CallModuleFunction(resolver, "resolve_wave");
                AssertLeakResolverSnapshot(
                    resolver,
                    expectedWave: 1,
                    expectedResolved: true,
                    expectedInitialLeaks: 5,
                    expectedRescued: 2,
                    expectedFinalLeaks: 3);

                LuaTable resolvedEvents = CallModuleTableFunction(resolver, "drain_events");
                try
                {
                    Assert.That(resolvedEvents.Length, Is.EqualTo(1));

                    LuaTable resolvedEvent = resolvedEvents.Get<int, LuaTable>(1);
                    try
                    {
                        Assert.That(
                            resolvedEvent.Get<string>("type"),
                            Is.EqualTo("PlayerLeakResolved"));
                        Assert.That(resolvedEvent.Get<int>("player_id"), Is.EqualTo(1));
                        Assert.That(resolvedEvent.Get<int>("wave"), Is.EqualTo(1));
                        Assert.That(resolvedEvent.Get<int>("initial_leak_count"), Is.EqualTo(5));
                        Assert.That(resolvedEvent.Get<int>("rescued_count"), Is.EqualTo(2));
                        Assert.That(resolvedEvent.Get<int>("final_leak_count"), Is.EqualTo(3));
                    }
                    finally
                    {
                        resolvedEvent.Dispose();
                    }
                }
                finally
                {
                    resolvedEvents.Dispose();
                }

                CallModuleFunction(resolver, "resolve_wave");
                LuaTable resolvedAgain = CallModuleTableFunction(resolver, "drain_events");
                try
                {
                    Assert.That(resolvedAgain.Length, Is.Zero);
                }
                finally
                {
                    resolvedAgain.Dispose();
                }

                CallModuleFunction(resolver, "begin_wave", 2);
                AssertLeakResolverSnapshot(
                    resolver,
                    expectedWave: 2,
                    expectedResolved: false,
                    expectedInitialLeaks: 0,
                    expectedRescued: 0,
                    expectedFinalLeaks: 0);
            }
            finally
            {
                if (resolver != null)
                {
                    CallModuleFunction(resolver, "shutdown");
                    resolver.Dispose();
                }

                runtime.Dispose();
            }
        }

        private static void AdvanceSimulation(LuaRuntime runtime, int stepCount)
        {
            for (int step = 0; step < stepCount; step++)
            {
                runtime.Tick((float)SimulationSettings.StepSeconds);
            }
        }

        private static void AdvanceUntilPhase(
            LuaRuntime runtime,
            string phase,
            int maxStepCount)
        {
            for (int step = 0; step < maxStepCount; step++)
            {
                runtime.Tick((float)SimulationSettings.StepSeconds);
                if (runtime.GetMatchFlowSnapshot().Phase == phase)
                {
                    return;
                }
            }

            Assert.Fail($"Phase {phase} was not reached within {maxStepCount} steps.");
        }

        private static void AdvanceUntilEnemyAliveCount(
            LuaRuntime runtime,
            int expectedAliveCount,
            int maxStepCount)
        {
            for (int step = 0; step < maxStepCount; step++)
            {
                runtime.Tick((float)SimulationSettings.StepSeconds);
                if (runtime.GetEnemyRosterSnapshot().AliveCount == expectedAliveCount)
                {
                    return;
                }
            }

            Assert.Fail(
                $"Enemy alive count {expectedAliveCount} was not reached within {maxStepCount} steps.");
        }

        private static void AssertSnapshot(
            LuaRuntime runtime,
            string phase,
            int wave,
            double remainingSeconds,
            bool isFinished,
            string result = "InProgress")
        {
            MatchFlowSnapshot snapshot = runtime.GetMatchFlowSnapshot();

            Assert.That(snapshot.Phase, Is.EqualTo(phase));
            Assert.That(snapshot.Wave, Is.EqualTo(wave));
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(remainingSeconds).Within(0.0001d));
            Assert.That(snapshot.IsFinished, Is.EqualTo(isFinished));
            Assert.That(snapshot.Result, Is.EqualTo(result));
        }

        private static void AssertEvents(
            IReadOnlyList<MatchEvent> actual,
            params MatchEvent[] expected)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Length));

            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(actual[index].Type, Is.EqualTo(expected[index].Type));
                Assert.That(actual[index].Wave, Is.EqualTo(expected[index].Wave));
                Assert.That(actual[index].Phase, Is.EqualTo(expected[index].Phase));
                Assert.That(actual[index].EnemyId, Is.EqualTo(expected[index].EnemyId));
                Assert.That(actual[index].SpawnIndex, Is.EqualTo(expected[index].SpawnIndex));
                Assert.That(
                    actual[index].EnemyInstanceId,
                    Is.EqualTo(expected[index].EnemyInstanceId));
                Assert.That(actual[index].Result, Is.EqualTo(expected[index].Result));
            }
        }

        private static void AssertEnemy(
            EnemySnapshot enemy,
            int instanceId,
            int wave,
            int spawnIndex)
        {
            Assert.That(enemy.InstanceId, Is.EqualTo(instanceId));
            Assert.That(enemy.EnemyId, Is.EqualTo("Crab"));
            Assert.That(enemy.Wave, Is.EqualTo(wave));
            Assert.That(enemy.SpawnIndex, Is.EqualTo(spawnIndex));
            Assert.That(enemy.TargetPlayerId, Is.EqualTo(1));
            Assert.That(enemy.Health, Is.EqualTo(10));
            Assert.That(enemy.MaxHealth, Is.EqualTo(10));
            Assert.That(enemy.Status, Is.EqualTo("Alive"));
            Assert.That(enemy.AttackDamage, Is.EqualTo(3));
            Assert.That(enemy.AttackIntervalSeconds, Is.EqualTo(1d).Within(0.0001d));
            Assert.That(enemy.AttackType, Is.EqualTo("Melee"));
            Assert.That(enemy.IsBoss, Is.False);
            Assert.That(enemy.RouteId, Is.EqualTo(1));
            Assert.That(enemy.PathSpeed, Is.EqualTo(0.05d).Within(0.0001d));
            Assert.That(enemy.BlockedByPieceInstanceId, Is.Null);
        }

        private static void AssertEnemyArchetype(
            LuaTable enemies,
            int index,
            string enemyId,
            int maxHealth,
            double pathSpeed,
            int attackDamage,
            double attackIntervalSeconds)
        {
            LuaTable enemy = enemies.Get<int, LuaTable>(index);

            try
            {
                Assert.That(enemy.Get<string>("enemy_id"), Is.EqualTo(enemyId));
                Assert.That(enemy.Get<int>("health"), Is.EqualTo(maxHealth));
                Assert.That(enemy.Get<int>("max_health"), Is.EqualTo(maxHealth));
                Assert.That(enemy.Get<double>("path_speed"), Is.EqualTo(pathSpeed).Within(0.0001d));
                Assert.That(enemy.Get<int>("attack_damage"), Is.EqualTo(attackDamage));
                Assert.That(
                    enemy.Get<double>("attack_interval_seconds"),
                    Is.EqualTo(attackIntervalSeconds).Within(0.0001d));
                Assert.That(enemy.Get<string>("attack_type"), Is.EqualTo("Melee"));
                Assert.That(enemy.Get<bool>("is_boss"), Is.False);
            }
            finally
            {
                enemy.Dispose();
            }
        }

        private static void AssertEnemyRosterSnapshot(
            LuaTable roster,
            int expectedCount,
            int expectedFirstHealth)
        {
            LuaTable snapshot = CallModuleTableFunction(roster, "get_snapshot");
            try
            {
                Assert.That(snapshot.Get<int>("alive_count"), Is.EqualTo(expectedCount));

                LuaTable enemies = snapshot.Get<LuaTable>("enemies");
                try
                {
                    Assert.That(enemies.Length, Is.EqualTo(expectedCount));

                    for (int index = 1; index <= enemies.Length; index++)
                    {
                        LuaTable enemy = enemies.Get<int, LuaTable>(index);
                        try
                        {
                            Assert.That(enemy.Get<int>("instance_id"), Is.EqualTo(index));
                            Assert.That(enemy.Get<string>("enemy_id"), Is.EqualTo("Crab"));
                            Assert.That(enemy.Get<int>("spawn_index"), Is.EqualTo(index));
                            Assert.That(enemy.Get<int>("target_player_id"), Is.EqualTo(1));
                        }
                        finally
                        {
                            enemy.Dispose();
                        }
                    }

                    LuaTable firstEnemy = enemies.Get<int, LuaTable>(1);
                    try
                    {
                        Assert.That(firstEnemy.Get<int>("health"), Is.EqualTo(expectedFirstHealth));
                        Assert.That(firstEnemy.Get<int>("max_health"), Is.EqualTo(10));
                        Assert.That(firstEnemy.Get<string>("status"), Is.EqualTo("Alive"));
                        Assert.That(firstEnemy.Get<int>("attack_damage"), Is.EqualTo(3));
                        Assert.That(
                            firstEnemy.Get<double>("attack_interval_seconds"),
                            Is.EqualTo(1d).Within(0.0001d));
                        Assert.That(firstEnemy.Get<string>("attack_type"), Is.EqualTo("Melee"));
                        Assert.That(firstEnemy.Get<int>("route_id"), Is.EqualTo(1));
                        Assert.That(
                            firstEnemy.Get<object>("blocked_by_piece_instance_id"),
                            Is.Null);
                    }
                    finally
                    {
                        firstEnemy.Dispose();
                    }
                }
                finally
                {
                    enemies.Dispose();
                }
            }
            finally
            {
                snapshot.Dispose();
            }
        }

        private static void AssertPlayerRosterSnapshot(
            LuaTable roster,
            int expectedHealth,
            string expectedStatus,
            int expectedAliveCount,
            int expectedGold = 10)
        {
            LuaTable snapshot = CallModuleTableFunction(roster, "get_snapshot");
            try
            {
                Assert.That(snapshot.Get<int>("alive_count"), Is.EqualTo(expectedAliveCount));

                LuaTable players = snapshot.Get<LuaTable>("players");
                try
                {
                    Assert.That(players.Length, Is.EqualTo(1));

                    LuaTable player = players.Get<int, LuaTable>(1);
                    try
                    {
                        Assert.That(player.Get<int>("player_id"), Is.EqualTo(1));
                        Assert.That(player.Get<int>("health"), Is.EqualTo(expectedHealth));
                        Assert.That(player.Get<int>("max_health"), Is.EqualTo(10));
                        Assert.That(player.Get<int>("gold"), Is.EqualTo(expectedGold));
                        Assert.That(player.Get<string>("status"), Is.EqualTo(expectedStatus));
                    }
                    finally
                    {
                        player.Dispose();
                    }
                }
                finally
                {
                    players.Dispose();
                }
            }
            finally
            {
                snapshot.Dispose();
            }
        }

        private static void AssertPieceRosterSnapshot(
            LuaTable roster,
            string firstLocation,
            int? firstCellId,
            string secondLocation,
            int? secondCellId)
        {
            LuaTable snapshot = CallModuleTableFunction(roster, "get_snapshot");
            try
            {
                LuaTable pieces = snapshot.Get<LuaTable>("pieces");
                try
                {
                    Assert.That(pieces.Length, Is.EqualTo(2));
                    AssertPieceSnapshot(
                        pieces,
                        index: 1,
                        expectedLocation: firstLocation,
                        expectedCellId: firstCellId);
                    AssertPieceSnapshot(
                        pieces,
                        index: 2,
                        expectedLocation: secondLocation,
                        expectedCellId: secondCellId);
                }
                finally
                {
                    pieces.Dispose();
                }

                int expectedBenchCount =
                    (firstLocation == "Bench" ? 1 : 0)
                    + (secondLocation == "Bench" ? 1 : 0);
                AssertPieceCapacityFields(
                    snapshot,
                    expectedBenchCount,
                    expectedBoardCount: 2 - expectedBenchCount);
            }
            finally
            {
                snapshot.Dispose();
            }
        }

        private static void AssertPieceCapacitySnapshot(
            LuaTable roster,
            int expectedBenchCount,
            int expectedBoardCount)
        {
            LuaTable snapshot = CallModuleTableFunction(roster, "get_snapshot");
            try
            {
                AssertPieceCapacityFields(
                    snapshot,
                    expectedBenchCount,
                    expectedBoardCount);
            }
            finally
            {
                snapshot.Dispose();
            }
        }

        private static void AssertPieceCapacityFields(
            LuaTable snapshot,
            int expectedBenchCount,
            int expectedBoardCount,
            int expectedTemporaryBenchCount = 0)
        {
            LuaTable players = snapshot.Get<LuaTable>("players");
            try
            {
                Assert.That(players.Length, Is.EqualTo(1));
                LuaTable player = players.Get<int, LuaTable>(1);
                try
                {
                    Assert.That(player.Get<int>("player_id"), Is.EqualTo(1));
                    Assert.That(player.Get<int>("bench_count"), Is.EqualTo(expectedBenchCount));
                    Assert.That(player.Get<int>("bench_capacity"), Is.EqualTo(11));
                    Assert.That(
                        player.Get<int>("temporary_bench_count"),
                        Is.EqualTo(expectedTemporaryBenchCount));
                    Assert.That(
                        player.Get<int>("temporary_bench_capacity"),
                        Is.EqualTo(11));
                    Assert.That(player.Get<int>("board_count"), Is.EqualTo(expectedBoardCount));
                    Assert.That(player.Get<int>("deployment_limit"), Is.EqualTo(2));
                }
                finally
                {
                    player.Dispose();
                }
            }
            finally
            {
                players.Dispose();
            }
        }

        private static PieceSnapshot FindPiece(
            PieceRosterSnapshot snapshot,
            int pieceInstanceId)
        {
            foreach (PieceSnapshot piece in snapshot.Pieces)
            {
                if (piece.InstanceId == pieceInstanceId)
                {
                    return piece;
                }
            }

            return null;
        }

        private static void AssertPieceSnapshot(
            LuaTable pieces,
            int index,
            string expectedLocation,
            int? expectedCellId)
        {
            LuaTable piece = pieces.Get<int, LuaTable>(index);
            try
            {
                Assert.That(piece.Get<int>("instance_id"), Is.EqualTo(index));
                Assert.That(piece.Get<string>("piece_id"), Is.EqualTo("Sprout"));
                Assert.That(piece.Get<int>("owner_player_id"), Is.EqualTo(1));
                Assert.That(piece.Get<int>("level"), Is.EqualTo(1));
                Assert.That(piece.Get<string>("location"), Is.EqualTo(expectedLocation));
                Assert.That(piece.Get<string>("facing"), Is.EqualTo("Right"));
                Assert.That(piece.Get<int>("health"), Is.EqualTo(12));
                Assert.That(piece.Get<int>("max_health"), Is.EqualTo(12));
                Assert.That(piece.Get<string>("status"), Is.EqualTo("Active"));
                Assert.That(piece.Get<int>("max_block_count"), Is.EqualTo(1));
                Assert.That(
                    piece.Get<double>("recovery_seconds_remaining"),
                    Is.EqualTo(0d).Within(0.0001d));
                Assert.That(piece.Get<int>("damage"), Is.EqualTo(4));
                Assert.That(
                    piece.Get<double>("attack_interval_seconds"),
                    Is.EqualTo(1d).Within(0.0001d));
                Assert.That(piece.Get<int>("sell_value"), Is.EqualTo(3));

                if (expectedCellId.HasValue)
                {
                    Assert.That(piece.Get<int>("cell_id"), Is.EqualTo(expectedCellId.Value));
                    string expectedTerrain = expectedLocation == "Bench"
                        || expectedCellId.Value == 101
                        || expectedCellId.Value == 103
                        ? "HighGround"
                        : "Ground";
                    Assert.That(piece.Get<string>("terrain"), Is.EqualTo(expectedTerrain));
                }
                else
                {
                    Assert.That(piece.Get<object>("cell_id"), Is.Null);
                    Assert.That(piece.Get<object>("terrain"), Is.Null);
                }

                LuaTable blockedEnemies =
                    piece.Get<LuaTable>("blocked_enemy_instance_ids");
                try
                {
                    Assert.That(blockedEnemies.Length, Is.Zero);
                }
                finally
                {
                    blockedEnemies.Dispose();
                }
            }
            finally
            {
                piece.Dispose();
            }
        }

        private static void AssertSinglePieceCombatSnapshot(
            LuaTable roster,
            int pieceIndex,
            int expectedHealth,
            string expectedStatus,
            int expectedBlockedCount,
            double expectedRecoverySeconds)
        {
            LuaTable snapshot = CallModuleTableFunction(roster, "get_snapshot");
            try
            {
                LuaTable pieces = snapshot.Get<LuaTable>("pieces");
                try
                {
                    LuaTable piece = pieces.Get<int, LuaTable>(pieceIndex);
                    try
                    {
                        Assert.That(piece.Get<int>("health"), Is.EqualTo(expectedHealth));
                        Assert.That(piece.Get<string>("status"), Is.EqualTo(expectedStatus));
                        Assert.That(
                            piece.Get<double>("recovery_seconds_remaining"),
                            Is.EqualTo(expectedRecoverySeconds).Within(0.0001d));

                        LuaTable blockedEnemies =
                            piece.Get<LuaTable>("blocked_enemy_instance_ids");
                        try
                        {
                            Assert.That(
                                blockedEnemies.Length,
                                Is.EqualTo(expectedBlockedCount));
                        }
                        finally
                        {
                            blockedEnemies.Dispose();
                        }
                    }
                    finally
                    {
                        piece.Dispose();
                    }
                }
                finally
                {
                    pieces.Dispose();
                }
            }
            finally
            {
                snapshot.Dispose();
            }
        }

        private static void AssertPieceDeployedEvent(
            LuaTable events,
            int index,
            int expectedPieceInstanceId,
            int expectedCellId,
            int? expectedPreviousCellId)
        {
            LuaTable deployedEvent = events.Get<int, LuaTable>(index);
            try
            {
                Assert.That(deployedEvent.Get<string>("type"), Is.EqualTo("PieceDeployed"));
                Assert.That(deployedEvent.Get<int>("player_id"), Is.EqualTo(1));
                Assert.That(deployedEvent.Get<string>("piece_id"), Is.EqualTo("Sprout"));
                Assert.That(
                    deployedEvent.Get<int>("piece_instance_id"),
                    Is.EqualTo(expectedPieceInstanceId));
                Assert.That(deployedEvent.Get<int>("cell_id"), Is.EqualTo(expectedCellId));

                if (expectedPreviousCellId.HasValue)
                {
                    Assert.That(
                        deployedEvent.Get<int>("previous_cell_id"),
                        Is.EqualTo(expectedPreviousCellId.Value));
                }
                else
                {
                    Assert.That(deployedEvent.Get<object>("previous_cell_id"), Is.Null);
                }
            }
            finally
            {
                deployedEvent.Dispose();
            }
        }

        private static void AssertEnemyDamageRequested(
            LuaTable events,
            int index,
            int expectedWave,
            int expectedEnemyInstanceId,
            int expectedSourcePieceInstanceId,
            int expectedDamage)
        {
            LuaTable attackEvent = events.Get<int, LuaTable>(index);
            try
            {
                Assert.That(
                    attackEvent.Get<string>("type"),
                    Is.EqualTo("EnemyDamageRequested"));
                Assert.That(attackEvent.Get<int>("wave"), Is.EqualTo(expectedWave));
                Assert.That(
                    attackEvent.Get<int>("enemy_instance_id"),
                    Is.EqualTo(expectedEnemyInstanceId));
                Assert.That(
                    attackEvent.Get<int>("source_piece_instance_id"),
                    Is.EqualTo(expectedSourcePieceInstanceId));
                Assert.That(attackEvent.Get<int>("damage"), Is.EqualTo(expectedDamage));
            }
            finally
            {
                attackEvent.Dispose();
            }
        }

        private static void AssertLuaEventType(
            LuaTable events,
            int index,
            string expectedType)
        {
            LuaTable eventTable = events.Get<int, LuaTable>(index);
            try
            {
                Assert.That(eventTable.Get<string>("type"), Is.EqualTo(expectedType));
            }
            finally
            {
                eventTable.Dispose();
            }
        }

        private static void AssertLuaEventTypeCount(
            LuaTable events,
            string eventType,
            int expectedCount)
        {
            int count = 0;

            for (int index = 1; index <= events.Length; index++)
            {
                LuaTable eventTable = events.Get<int, LuaTable>(index);
                try
                {
                    if (eventTable.Get<string>("type") == eventType)
                    {
                        count++;
                    }
                }
                finally
                {
                    eventTable.Dispose();
                }
            }

            Assert.That(
                count,
                Is.EqualTo(expectedCount),
                $"Unexpected event count for {eventType}.");
        }

        private static int FindLuaEventIndex(
            LuaTable events,
            string eventType,
            int occurrence)
        {
            int foundCount = 0;

            for (int index = 1; index <= events.Length; index++)
            {
                LuaTable eventTable = events.Get<int, LuaTable>(index);
                try
                {
                    if (eventTable.Get<string>("type") == eventType)
                    {
                        foundCount++;
                        if (foundCount == occurrence)
                        {
                            return index;
                        }
                    }
                }
                finally
                {
                    eventTable.Dispose();
                }
            }

            throw new AssertionException(
                $"Event {eventType} occurrence {occurrence} was not found.");
        }

        private static void AssertLeakResolverSnapshot(
            LuaTable resolver,
            int expectedWave,
            bool expectedResolved,
            int expectedInitialLeaks,
            int expectedRescued,
            int expectedFinalLeaks)
        {
            LuaTable snapshot = CallModuleTableFunction(resolver, "get_snapshot");
            try
            {
                Assert.That(snapshot.Get<int>("wave"), Is.EqualTo(expectedWave));
                Assert.That(snapshot.Get<bool>("is_resolved"), Is.EqualTo(expectedResolved));

                LuaTable players = snapshot.Get<LuaTable>("players");
                try
                {
                    Assert.That(players.Length, Is.EqualTo(1));

                    LuaTable player = players.Get<int, LuaTable>(1);
                    try
                    {
                        Assert.That(player.Get<int>("player_id"), Is.EqualTo(1));
                        Assert.That(
                            player.Get<int>("initial_leak_count"),
                            Is.EqualTo(expectedInitialLeaks));
                        Assert.That(
                            player.Get<int>("rescued_count"),
                            Is.EqualTo(expectedRescued));
                        Assert.That(
                            player.Get<int>("final_leak_count"),
                            Is.EqualTo(expectedFinalLeaks));
                    }
                    finally
                    {
                        player.Dispose();
                    }
                }
                finally
                {
                    players.Dispose();
                }
            }
            finally
            {
                snapshot.Dispose();
            }
        }

        private static void AssertEnemyPathState(
            LuaTable enemies,
            int index,
            string status,
            double expectedProgress)
        {
            LuaTable enemy = enemies.Get<int, LuaTable>(index);
            try
            {
                Assert.That(enemy.Get<string>("status"), Is.EqualTo(status));
                Assert.That(
                    enemy.Get<double>("path_progress"),
                    Is.EqualTo(expectedProgress).Within(0.0001d));
            }
            finally
            {
                enemy.Dispose();
            }
        }

        private static void AssertAliveProgressSnapshot(
            LuaTable roster,
            double expectedProgress)
        {
            LuaTable snapshot = CallModuleTableFunction(roster, "get_snapshot");
            try
            {
                Assert.That(snapshot.Get<int>("alive_count"), Is.EqualTo(1));

                LuaTable enemies = snapshot.Get<LuaTable>("enemies");
                try
                {
                    Assert.That(enemies.Length, Is.EqualTo(1));

                    LuaTable enemy = enemies.Get<int, LuaTable>(1);
                    try
                    {
                        Assert.That(enemy.Get<string>("status"), Is.EqualTo("Alive"));
                        Assert.That(
                            enemy.Get<double>("path_progress"),
                            Is.EqualTo(expectedProgress).Within(0.0001d));
                    }
                    finally
                    {
                        enemy.Dispose();
                    }
                }
                finally
                {
                    enemies.Dispose();
                }
            }
            finally
            {
                snapshot.Dispose();
            }
        }

        private static BoardCellSnapshot FindBoardCell(
            BoardSnapshot snapshot,
            int gridX,
            int gridY)
        {
            foreach (BoardCellSnapshot cell in snapshot.Cells)
            {
                if (cell.GridX == gridX && cell.GridY == gridY)
                {
                    return cell;
                }
            }

            throw new AssertionException(
                $"Board cell not found at ({gridX}, {gridY}).");
        }

        private static void AssertEnemyCombatSnapshot(
            LuaTable roster,
            int expectedHealth,
            string expectedStatus,
            int expectedAliveCount,
            double expectedPathProgress)
        {
            LuaTable snapshot = CallModuleTableFunction(roster, "get_snapshot");
            try
            {
                Assert.That(snapshot.Get<int>("alive_count"), Is.EqualTo(expectedAliveCount));

                LuaTable enemies = snapshot.Get<LuaTable>("enemies");
                try
                {
                    Assert.That(enemies.Length, Is.EqualTo(1));

                    LuaTable enemy = enemies.Get<int, LuaTable>(1);
                    try
                    {
                        Assert.That(enemy.Get<int>("health"), Is.EqualTo(expectedHealth));
                        Assert.That(enemy.Get<string>("status"), Is.EqualTo(expectedStatus));
                        Assert.That(
                            enemy.Get<double>("path_progress"),
                            Is.EqualTo(expectedPathProgress).Within(0.0001d));
                    }
                    finally
                    {
                        enemy.Dispose();
                    }
                }
                finally
                {
                    enemies.Dispose();
                }
            }
            finally
            {
                snapshot.Dispose();
            }
        }

        private static void AssertReachedEndpointSnapshot(
            LuaTable roster,
            int expectedEnemyCount)
        {
            LuaTable snapshot = CallModuleTableFunction(roster, "get_snapshot");
            try
            {
                Assert.That(snapshot.Get<int>("alive_count"), Is.Zero);

                LuaTable enemies = snapshot.Get<LuaTable>("enemies");
                try
                {
                    Assert.That(enemies.Length, Is.EqualTo(expectedEnemyCount));

                    for (int index = 1; index <= enemies.Length; index++)
                    {
                        LuaTable enemy = enemies.Get<int, LuaTable>(index);
                        try
                        {
                            Assert.That(enemy.Get<int>("instance_id"), Is.EqualTo(index));
                            Assert.That(enemy.Get<int>("target_player_id"), Is.EqualTo(1));
                            Assert.That(enemy.Get<string>("status"), Is.EqualTo("ReachedEndpoint"));
                            Assert.That(enemy.Get<double>("path_progress"), Is.EqualTo(1d));
                        }
                        finally
                        {
                            enemy.Dispose();
                        }
                    }
                }
                finally
                {
                    enemies.Dispose();
                }
            }
            finally
            {
                snapshot.Dispose();
            }
        }

        private static LuaTable CreateEvent(
            LuaRuntime runtime,
            string type,
            int wave,
            string enemyId,
            int spawnIndex)
        {
            LuaTable eventTable = runtime.Environment.NewTable();
            eventTable.Set("type", type);
            eventTable.Set("wave", wave);
            eventTable.Set("enemy_id", enemyId);
            eventTable.Set("spawn_index", spawnIndex);
            eventTable.Set("target_player_id", 1);
            return eventTable;
        }

        private static LuaTable CreatePieceDeployRequested(
            LuaRuntime runtime,
            int playerId,
            int pieceInstanceId,
            int cellId)
        {
            LuaTable eventTable = runtime.Environment.NewTable();
            eventTable.Set("type", "PieceDeployRequested");
            eventTable.Set("player_id", playerId);
            eventTable.Set("piece_instance_id", pieceInstanceId);
            eventTable.Set("cell_id", cellId);
            return eventTable;
        }

        private static void UpdatePieceAttackPlanner(
            LuaTable planner,
            LuaTable pieceRoster,
            LuaTable enemyRoster,
            double deltaTime)
        {
            LuaTable pieceSnapshot = CallModuleTableFunction(pieceRoster, "get_snapshot");
            LuaTable enemySnapshot = CallModuleTableFunction(enemyRoster, "get_snapshot");

            try
            {
                CallModuleFunction(
                    planner,
                    "update",
                    deltaTime,
                    pieceSnapshot,
                    enemySnapshot);
            }
            finally
            {
                pieceSnapshot.Dispose();
                enemySnapshot.Dispose();
            }
        }

        private static void UpdateBlockResolver(
            LuaTable resolver,
            LuaTable pieceRoster,
            LuaTable enemyRoster,
            double deltaTime)
        {
            LuaTable pieceSnapshot = CallModuleTableFunction(pieceRoster, "get_snapshot");
            LuaTable enemySnapshot = CallModuleTableFunction(enemyRoster, "get_snapshot");

            try
            {
                CallModuleFunction(
                    resolver,
                    "update",
                    deltaTime,
                    pieceSnapshot,
                    enemySnapshot);
            }
            finally
            {
                enemySnapshot.Dispose();
                pieceSnapshot.Dispose();
            }
        }

        private static void UpdateEnemyAttackPlanner(
            LuaTable planner,
            LuaTable pieceRoster,
            LuaTable enemyRoster,
            double deltaTime)
        {
            LuaTable pieceSnapshot = CallModuleTableFunction(pieceRoster, "get_snapshot");
            LuaTable enemySnapshot = CallModuleTableFunction(enemyRoster, "get_snapshot");

            try
            {
                CallModuleFunction(
                    planner,
                    "update",
                    deltaTime,
                    pieceSnapshot,
                    enemySnapshot);
            }
            finally
            {
                enemySnapshot.Dispose();
                pieceSnapshot.Dispose();
            }
        }

        private static void HandlePieceDeployRequested(
            LuaRuntime runtime,
            LuaTable roster,
            int playerId,
            int pieceInstanceId,
            int cellId)
        {
            LuaTable eventTable = CreatePieceDeployRequested(
                runtime,
                playerId,
                pieceInstanceId,
                cellId);

            try
            {
                CallModuleFunction(roster, "handle_event", eventTable);
            }
            finally
            {
                eventTable.Dispose();
            }
        }

        private static void HandlePieceBenchRequested(
            LuaRuntime runtime,
            LuaTable roster,
            int playerId,
            int pieceInstanceId)
        {
            LuaTable eventTable = runtime.Environment.NewTable();
            eventTable.Set("type", "PieceBenchRequested");
            eventTable.Set("player_id", playerId);
            eventTable.Set("piece_instance_id", pieceInstanceId);

            try
            {
                CallModuleFunction(roster, "handle_event", eventTable);
            }
            finally
            {
                eventTable.Dispose();
            }
        }

        private static List<int> FillBenchWithMergedSprouts(
            LuaRuntime runtime,
            LuaTable roster)
        {
            List<int> pieceIds = new List<int>();

            for (int maxLevelIndex = 0; maxLevelIndex < 4; maxLevelIndex++)
            {
                int resultingPieceId = 0;
                for (int copyIndex = 0; copyIndex < 9; copyIndex++)
                {
                    resultingPieceId =
                        CallModuleIntFunction(roster, "grant_piece", 1, "Sprout");
                }

                pieceIds.Add(resultingPieceId);
                if (maxLevelIndex < 2)
                {
                    HandlePieceDeployRequested(
                        runtime,
                        roster,
                        1,
                        resultingPieceId,
                        maxLevelIndex == 0 ? 101 : 102);
                }
            }

            for (int levelTwoIndex = 0; levelTwoIndex < 2; levelTwoIndex++)
            {
                int resultingPieceId = 0;
                for (int copyIndex = 0; copyIndex < 3; copyIndex++)
                {
                    resultingPieceId =
                        CallModuleIntFunction(roster, "grant_piece", 1, "Sprout");
                }

                pieceIds.Add(resultingPieceId);
            }

            pieceIds.Add(CallModuleIntFunction(roster, "grant_piece", 1, "Sprout"));
            pieceIds.Add(CallModuleIntFunction(roster, "grant_piece", 1, "Sprout"));
            pieceIds.Add(CallModuleIntFunction(roster, "grant_piece", 1, "Bramble"));
            pieceIds.Add(CallModuleIntFunction(roster, "grant_piece", 1, "Bramble"));
            pieceIds.Add(CallModuleIntFunction(roster, "grant_piece", 1, "Bloom"));
            HandlePieceBenchRequested(runtime, roster, 1, pieceIds[0]);
            HandlePieceBenchRequested(runtime, roster, 1, pieceIds[1]);
            return pieceIds;
        }

        private static void FillBenchWithMergedSprouts(LuaRuntime runtime)
        {
            List<int> pieceIds = new List<int>();

            for (int maxLevelIndex = 0; maxLevelIndex < 4; maxLevelIndex++)
            {
                int resultingPieceId = 0;
                for (int copyIndex = 0; copyIndex < 9; copyIndex++)
                {
                    resultingPieceId = runtime.GrantPiece(1, "Sprout");
                }

                pieceIds.Add(resultingPieceId);
                if (maxLevelIndex < 2)
                {
                    runtime.DeployPiece(
                        1,
                        resultingPieceId,
                        maxLevelIndex == 0 ? 101 : 102);
                }
            }

            for (int levelTwoIndex = 0; levelTwoIndex < 2; levelTwoIndex++)
            {
                int resultingPieceId = 0;
                for (int copyIndex = 0; copyIndex < 3; copyIndex++)
                {
                    resultingPieceId = runtime.GrantPiece(1, "Sprout");
                }

                pieceIds.Add(resultingPieceId);
            }

            pieceIds.Add(runtime.GrantPiece(1, "Sprout"));
            pieceIds.Add(runtime.GrantPiece(1, "Sprout"));
            pieceIds.Add(runtime.GrantPiece(1, "Bramble"));
            pieceIds.Add(runtime.GrantPiece(1, "Bramble"));
            pieceIds.Add(runtime.GrantPiece(1, "Bloom"));
            runtime.BenchPiece(1, pieceIds[0]);
            runtime.BenchPiece(1, pieceIds[1]);
        }

        private static int GrantMaxLevelPiece(LuaRuntime runtime, string pieceId)
        {
            int resultingPieceId = 0;

            for (int copyIndex = 0; copyIndex < 9; copyIndex++)
            {
                resultingPieceId = runtime.GrantPiece(1, pieceId);
            }

            return resultingPieceId;
        }

        private static void DeployBossVictoryBoard(LuaRuntime runtime)
        {
            int sproutId = GrantMaxLevelPiece(runtime, "Sprout");
            int brambleId = GrantMaxLevelPiece(runtime, "Bramble");
            int bloomId = GrantMaxLevelPiece(runtime, "Bloom");

            runtime.UpgradeShop(1);

            // 这套布阵覆盖两条既定路线的汇合区域，同时保留近终点阻挡位，
            // 用于验证“强力棋盘可以稳定推进到 Boss 并取胜”这一回合主流程。
            runtime.DeployPiece(1, sproutId, 1041);
            runtime.DeployPiece(1, brambleId, 102);
            runtime.DeployPiece(1, bloomId, 103);

            runtime.SetPieceFacing(1, brambleId, "Down");
            runtime.SetPieceFacing(1, bloomId, "Down");
        }

        private static void HandlePieceFacingRequested(
            LuaRuntime runtime,
            LuaTable roster,
            int playerId,
            int pieceInstanceId,
            string facing)
        {
            LuaTable eventTable = runtime.Environment.NewTable();
            eventTable.Set("type", "PieceFacingRequested");
            eventTable.Set("player_id", playerId);
            eventTable.Set("piece_instance_id", pieceInstanceId);
            eventTable.Set("facing", facing);

            try
            {
                CallModuleFunction(roster, "handle_event", eventTable);
            }
            finally
            {
                eventTable.Dispose();
            }
        }

        private static void HandleEnemyDamageRequested(
            LuaRuntime runtime,
            LuaTable roster,
            int wave,
            int enemyInstanceId,
            int sourcePieceInstanceId,
            int damage)
        {
            LuaTable eventTable = runtime.Environment.NewTable();
            eventTable.Set("type", "EnemyDamageRequested");
            eventTable.Set("wave", wave);
            eventTable.Set("enemy_instance_id", enemyInstanceId);
            eventTable.Set("source_piece_instance_id", sourcePieceInstanceId);
            eventTable.Set("damage", damage);

            try
            {
                CallModuleFunction(roster, "handle_event", eventTable);
            }
            finally
            {
                eventTable.Dispose();
            }
        }

        private static void HandlePieceDamageRequested(
            LuaRuntime runtime,
            LuaTable roster,
            int pieceInstanceId,
            int sourceEnemyInstanceId,
            int damage)
        {
            LuaTable eventTable = runtime.Environment.NewTable();
            eventTable.Set("type", "PieceDamageRequested");
            eventTable.Set("piece_instance_id", pieceInstanceId);
            eventTable.Set("source_enemy_instance_id", sourceEnemyInstanceId);
            eventTable.Set("damage", damage);

            try
            {
                CallModuleFunction(roster, "handle_event", eventTable);
            }
            finally
            {
                eventTable.Dispose();
            }
        }

        private static void HandleLeakedEnemyRescuedEvent(
            LuaRuntime runtime,
            LuaTable resolver,
            int wave,
            int enemyInstanceId)
        {
            LuaTable eventTable = runtime.Environment.NewTable();
            eventTable.Set("type", "LeakedEnemyRescued");
            eventTable.Set("wave", wave);
            eventTable.Set("enemy_instance_id", enemyInstanceId);

            try
            {
                CallModuleFunction(resolver, "handle_event", eventTable);
            }
            finally
            {
                eventTable.Dispose();
            }
        }

        private static void HandlePlayerLeakResolvedEvent(
            LuaRuntime runtime,
            LuaTable roster,
            int playerId,
            int wave,
            int initialLeakCount,
            int rescuedCount,
            int finalLeakCount)
        {
            LuaTable eventTable = runtime.Environment.NewTable();
            eventTable.Set("type", "PlayerLeakResolved");
            eventTable.Set("player_id", playerId);
            eventTable.Set("wave", wave);
            eventTable.Set("initial_leak_count", initialLeakCount);
            eventTable.Set("rescued_count", rescuedCount);
            eventTable.Set("final_leak_count", finalLeakCount);

            try
            {
                CallModuleFunction(roster, "handle_event", eventTable);
            }
            finally
            {
                eventTable.Dispose();
            }
        }

        private static LuaTable CreatePlayerEndpointEvent(
            LuaRuntime runtime,
            int playerId,
            int wave,
            int enemyInstanceId)
        {
            LuaTable eventTable = runtime.Environment.NewTable();
            eventTable.Set("type", "EnemyReachedEndpoint");
            eventTable.Set("target_player_id", playerId);
            eventTable.Set("wave", wave);
            eventTable.Set("enemy_instance_id", enemyInstanceId);
            return eventTable;
        }

        private static LuaTable RequireTable(LuaRuntime runtime, string moduleName)
        {
            runtime.Environment.Global.Set("__protect_tree_test_module", moduleName);

            try
            {
                object[] results = runtime.Environment.DoString(
                    "return require(__protect_tree_test_module)",
                    "LuaRuntimeTests.RequireTable");

                return results[0] as LuaTable
                    ?? throw new InvalidOperationException(
                        $"Lua module must return a table: {moduleName}");
            }
            finally
            {
                runtime.Environment.Global.Set<string, object>(
                    "__protect_tree_test_module",
                    null);
            }
        }

        private static void CallModuleFunction(
            LuaTable module,
            string functionName,
            params object[] args)
        {
            LuaFunction function = module.Get<LuaFunction>(functionName);

            try
            {
                function.Call(args);
            }
            finally
            {
                function.Dispose();
            }
        }

        private static int CallModuleIntFunction(
            LuaTable module,
            string functionName,
            params object[] args)
        {
            LuaFunction function = module.Get<LuaFunction>(functionName);

            try
            {
                object[] results = function.Call(args);
                if (results.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Lua function must return one value: {functionName}");
                }

                return Convert.ToInt32(results[0]);
            }
            finally
            {
                function.Dispose();
            }
        }

        private static string CallModuleStringFunction(
            LuaTable module,
            string functionName,
            params object[] args)
        {
            LuaFunction function = module.Get<LuaFunction>(functionName);

            try
            {
                object[] results = function.Call(args);
                if (results.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Lua function must return one value: {functionName}");
                }

                return Convert.ToString(results[0]);
            }
            finally
            {
                function.Dispose();
            }
        }

        private static LuaTable CallModuleTableFunction(
            LuaTable module,
            string functionName)
        {
            LuaFunction function = module.Get<LuaFunction>(functionName);

            try
            {
                object[] results = function.Call();
                return results[0] as LuaTable
                    ?? throw new InvalidOperationException(
                        $"Lua function must return a table: {functionName}");
            }
            finally
            {
                function.Dispose();
            }
        }

        private static void WriteScript(string path, string contents)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
        }
    }
}
