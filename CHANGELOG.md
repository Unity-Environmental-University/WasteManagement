# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Fixed
- `FoilCard.ProcessEffect` now delegates to the inner card instead of being a no-op
- `ValidateUpgrades` limit corrected to match 6-slot upgrade array
- Card effect multipliers corrected for `ChemicalSolvent`, `UpgradedMeshNet`, and `SuperiorMaintenance`

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