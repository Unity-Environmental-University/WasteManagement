# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added
- Token-based shop system (`ShopManager`, `ShopObject`) with a level-gated catalog and `PlacementInventory` for queued purchases
- Grid-based path building (`PathBuildBoard`/`PathBuildCell`): place/break tools, live placement/break previews, path-facing rotation for oriented placement, modular pipe tile rendering, path splitter routing, and an alternate path preview
- Dynamic `WaypointPath` rebuilding via BFS with strict edge-adjacent endpoint validation
- Prefab-driven `PathToolBar` with short/long/break/clear tool switches, level-gated interactability, and a rotate-hint label
- Placement slots (`SpecialInteractController`) for towers, sifters, cesspits, and treatment tanks, scattered across the board by `SpecialTileSpawner`
- `Cesspit` utility with fullness-driven overflow, phase-paused runaway issue spawning, click-to-destroy runaways, cesspit cap sealing, and cesspit burial (demolish into a debuff tile)
- `TreatmentTank` utility with `EffluentObject` spawning for consumed issues
- `LimeSprinkler` placeable for pipeline-only stink reduction
- `PopulationManager` with population/level progression, wave-based growth weighted by infrastructure/pollution/stink, spawn-rate and wave-duration scaling, and level-1 onboarding mechanics
- Stink system: `IStinkSource` interface, `StinkSourceRegistry` aggregation, and a stink meter UI, with the lake, cesspit, treatment tank, and lime sprinkler registered as sources
- `LakeController` pollution-driven health degradation with per-turn recovery
- Buff/debuff tile system (`BuffDebuffTileController`, `BuffDebuffTileEffect` strategy assets, move-speed effect) and its spawner
- Post-round summary flow (`PostRoundSummaryPanel`/`PostRoundSummaryData`) showing growth, pollution, stink, cesspit stats, and unlocked shop items
- Game loss screen flow (`LossScreenPanel`) with retry/quit
- `CameraController` with camera switching between card/tower phases and camera shake when an issue blocks a pipe
- `CityscapeGrowthController` revealing background city buildings as the town level rises
- `IssueObject` merging and pipe-blockage mechanics (size-driven clogging, click-to-shrink, burst-telegraphing animation), travel-direction facing, and swapped size visuals using new custom poop assets
- `IssueType` (`Organic`/`Chemical`) processing and per-path tower assignment
- `WasteSifter` health depletion and collision-based damage
- World-space tower health bar and loss-condition wiring on `TowerController`
- `InfraValue` tracking on shop items, surfaced through `TurnController.infrastructureValue` and the info bar
- Move-count tracking (`TurnController.RegisterMove`)
- `IssuePreview` queue generation (not yet wired into any consuming system)
- `CreditsLib` ScriptableObject scaffold and `MenuManager` async scene loader
- Test coverage for `PopulationManager` growth/thresholds, `GameMaster` wiring, strict endpoint path validation, path-facing rotation, cesspit sealing/burial, lake recovery, issue merging, and full wave-lifecycle flows

### Changed
- Shop restocks consistently each session instead of accumulating or duplicating stock; placement inventory clears at round start
- Cesspit runaway spawning now stops alongside other spawners at wave end and while the round summary is shown
- `PathToolBar` converted from runtime-built to prefab-driven
- Population growth model replaced flat infrastructure-based jumps with wave-based growth weighted by pollution and stink
- Speed buff/debuff tile effects are now temporary rather than permanent
- `TurnController`/`InterfaceManager` refactored for clarity (shared `SetActive` helper, consolidated phase/UI handling); `CameraController` extracted from `TurnController`
- Buff/debuff tile and slot-scattering responsibilities consolidated from `BuffDebuffTileSpawner`/`SpecialInteractSpawner` into `SpecialTileSpawner`
- Deck initialization temporarily disabled pending the card system's return

### Fixed
- `FoilCard.ProcessEffect` now delegates to the inner card instead of being a no-op
- `ValidateUpgrades` limit corrected to match 6-slot upgrade array
- Card effect multipliers corrected for `ChemicalSolvent`, `UpgradedMeshNet`, and `SuperiorMaintenance`
- Null reference exceptions in `TurnController` camera/spawner operations and missing-quit-button handling
- UI click-through on path build cells
- Issues no longer soft-lock waves when reduced to zero move speed
- Broken sifters no longer register collisions after breaking
- Buff/debuff tiles no longer intercept pointer raycasts meant for placement slots; enforced one effect tile per board cell
- Entity spawners restart safely between waves
- Resized pipe jams now correctly enter treatment tanks
- Cesspits unregister from the stink source registry before destruction
- Info bar now updates correctly on phase transitions

### Removed
- `ScoreManager` (superseded by the level-access shop system)
- `BuffDebuffTileSpawner` and `SpecialInteractSpawner` (merged into `SpecialTileSpawner`)

---

## [0.1.0] - 2026-03-23

### Added
- Tower upgrade slot display in UI
- Hand UI spreads cards on draw; played cards are removed from the hand
- Wave sequencing with configurable delays
- Spawner start/stop control methods on `EntitySpawner`
- Maintenance system on `TowerController` (health, regen, per-type process power)
- `IssueType` enum (`Organic`, `Chemical`) with processing logic in `IssueObject`
- `DeckManager` for deck/hand/discard operations
- `GamePhase` enum; `TurnController` refactored to use it
- `UpgradeInterface` with hover and click handling
- `CardController` card selection logic and `AssignCard` method
- `InterfaceManager` next-button field and `PopulateHand`/`ClearHand` methods
- Null checks for `TowerManager`, `InterfaceManager`, `DeckManager` in `GameMaster`
- `TowerController` and `TowerManager` for tower entity management
- `IssueObject` enemy unit with waypoint traversal
- `WaypointPath` with multi-path support on `EntitySpawner`
- `CardClasses.cs` ported and simplified from Horticulture (`ICard`, `FoilCard`, `CardHand`)
- `GameMaster` singleton coordinator
- `TurnController` turn/wave state machine
- Initial project scripts and configuration

### Changed
- `TurnController.EnterCardSequence` now calls `DrawNewHand` and `PopulateHand` on each card phase entry
- `WaveTimer` increments turn counter and re-enters card phase after each wave ends
- `TowerController` now uses `ICard` interface for upgrades
- Upgrade array encapsulated behind `GetCurrentUpgrades` / `AddUpgrade`
- `selectedCard` capitalization standardized across `GameMaster`, `CardController`, `UpgradeInterface`, `TowerController`
- `Card Core/` folder renamed to `Core/`

### Fixed
- Start-of-game card draw
- `CardController` no longer hardcodes `TestCard` on Start