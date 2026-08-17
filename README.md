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

## Summit + Trailhead

The Main scene includes `WasteBoardReplayRecorder` on the Game Master. It records:

- the segmented path board as recording metadata;
- path placements/removals as deterministic cell events;
- towers, treatment equipment, the player camera, and moving issues as replay subjects;
- phase, level, placement, pipe-break, leak, and game-loss events to both Trailhead and Summit.

Configure the `SummitAnalytics` and `TrailheadRecorder` components beside it in the Main scene:

- set each service URL without a trailing slash;
- set the game-specific API key;
- leave `WasteBoardReplayRecorder.startOnLaunch` enabled for automatic sessions.

Unconfigured release builds skip capture and log one warning instead of attempting uploads. Editor play mode still captures locally while network uploads remain disabled by the client guards.

Completed Trailhead payloads are saved to the application's persistent data directory before upload. If an upload is interrupted, it is retried on the next launch. Exiting Unity Play Mode also completes the active session so it enters that retry queue.
