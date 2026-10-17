# Derby match menus

The pause, single-player results and multiplayer results are built by
`DerbyMenuView` using resolution-independent UI meshes. The original serialized
panels are disabled at runtime; their scene references are not destroyed.

Anton is by Vernon Adams, distributed under the SIL Open Font License in
`OFL.txt`. Its condensed lettering is used for the reference-style menus.

Menu actions, settings, scores, input-release protection and gamepad focus remain
owned by the existing PauseMenu, ScoreBoard and ResultBoard components.

The backdrop is only a translucent input-blocking Image. It never captures,
replaces, blurs, flips, or re-renders the camera output. HUD changes stay live.
Result badges use compact layouts; the leaderboard height follows the actual
number of participants rather than padding two players to four rows.

`CarFollowCamera` yields cursor ownership and mouse-look input to pause and result
screens. Do not re-lock the cursor from gameplay scripts while those menus are up;
the Input System UI module ignores pointer hit tests when the cursor is locked.
