# SludgeTower

A Waste Management card game / tower defense hybrid built in Unity.

Enemies spawn and traverse waypoint paths toward a goal. Players use cards to place defenses and manage resources across turns and waves.

## Structure

```
Scripts/
├── Core/           — turn/wave flow, game coordination
├── Object Scripts/ — runtime entities and paths
├── UI/             — interface controllers
└── Tests/          — test assembly
```

## Key Entry Points

- `TurnController.BeginWaveSequence()` — starts wave logic
- `IssueObject.OnReachedEnd` — fires when an enemy reaches the goal
- `GameMaster.Instance` — access to major subsystems

## Notes

- Unity project; build and run via the Unity Editor
- Scripts use the `_project.Scripts.<FolderName>` namespace convention
- See `CHANGELOG.md` for version history
