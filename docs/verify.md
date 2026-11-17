# Verify analytics and replay

Use a development build and test instances of Summit and Trailhead. On the
Game Master in the Main scene, configure `SummitAnalytics` with the Summit
origin and game app key. Configure `TrailheadRecorder` with the Trailhead URL
including `/trailhead` and the Trailhead recording upload key. Leave
`WasteBoardReplayRecorder.startOnLaunch` enabled for automatic capture.

1. Start a session and place a defense. Play long enough to capture a
   meaningful board change and let the recorder send a segment.
2. In Summit, open the game's session and find the placement on its timeline.
3. Follow the replay link. Check that the board shows the placement at the
   corresponding point on the replay timeline.
4. End the session and reopen the recording to check that the last activity
   appears.

If Trailhead shows the recording but Summit has no replay link, check that
the recording carries the Summit-issued session UUID. Check Trailhead's
server-side Summit sync settings if it has the UUID.

## What happens in a browser

The Waste Management recorder sends segments while the game is running. Its
default interval is 15 seconds. A tab becoming hidden asks for a checkpoint.
A page unload requests a final segment. Both browser callbacks are best
effort. The last segment can be lost when the tab closes, and earlier accepted
segments remain available to replay.

When testing that path, wait for a recording to appear in Trailhead before
hiding or closing the tab. Use a separate session to check normal
finalization.
