# Agent notes — unity-game-test (Forge)

Read `ARCHITECTURE.md` first. It is the source of truth for structure and conventions.

## Workflow with the Unity Editor (MCP for Unity)

- The Editor must be open with the MCP server running (HTTP, port 8765, see `.mcp.json`).
- After creating or editing scripts or asmdefs: `refresh_unity` (compile=request,
  wait_for_ready) → `read_console` for errors → `run_tests` (EditMode).
- Move, rename and delete assets through MCP `manage_asset`, never the raw file system,
  so GUIDs survive. `manage_asset` move/rename has reported "failed" while actually
  succeeding, so verify with `get_info` before retrying.
- The console line `[WebSocket] Unexpected receive error: WebSocket is not initialised`
  after a domain reload is harmless.
- Changes made via MCP (e.g. build settings) may only live in memory. Run
  `execute_menu_item("File/Save Project")` before committing.
- **asmdef pitfall:** if an assembly uses `overrideReferences: true`, it must also list
  every precompiled DLL (e.g. `Newtonsoft.Json.dll`) used by the assemblies it references.
  Otherwise Unity **silently drops** the reference and you get "namespace not found".
  Check `Library/Bee/artifacts/*/<Assembly>.rsp` to see the real references.
- Test count lower than expected = a stale test DLL. Read the console for compile errors.
- Commit with the Bash tool (heredoc). PowerShell 5.1 `-Encoding utf8` adds a BOM.
- **UI theme imports:** after adding an `@import` to `UI/ForgeRuntimeTheme.tss`, the running panel keeps
  the old flattened theme (the new sheet's rules silently don't apply, even across play restarts).
  Force-reimport the `.tss` (`AssetDatabase.ImportAsset(..., ForceUpdate)`) and request a script
  reload (`EditorUtility.RequestScriptReload()`), then play again. Sheets attached in UXML don't need this.
- A UXML that uses new `[UxmlElement]` classes may import before they compile ("missing a
  UxmlElementAttribute"); reimport the `.uxml` after the compile and the message is gone.
- Unity's default theme sizes slider parts per orientation (`.unity-base-slider--horizontal .…__dragger`), so
  overrides need that specificity. A USS type selector (`Button`) names the C# type: subclasses don't match.
- Unity's runtime theme colours focus rings and text selection through its own variables
  (`--unity-colors-input_field-border-focus`, `--unity-selection-color`, `--unity-cursor-color`,
  `--unity-colors-highlight-*`); override them in `ForgeTokens.uss`, selectors alone don't reach them.
  A text input ignores an inline `width`; set `min-width` and `max-width` instead (`FieldChrome.SetDigits`).
- SVG icons import as UI Toolkit `VectorImage` through the built-in vector graphics module; the
  `com.unity.vectorgraphics` package is not needed and 2.0.0-preview.24 does not compile here.
- **Play-mode script reload:** with "Recompile And Continue Playing", Unity reloads the
  domain mid-play and re-serializes *private* fields. Non-serializable runtime state
  becomes null and strings become `""`. Mark runtime-only fields `[NonSerialized]`.
  The editor pref `ScriptCompilationDuringPlay` is set to 1 (recompile after playing).
- `Transform.Find` treats `/` as a path separator, and record ids contain `/`, so look
  objects up via `RecordLink`, not by name.
- Regenerate the base plugin with the menu item `Forge/Generate Core Plugin`
  (`execute_menu_item`). It is deterministic.
- Stress world: `Forge/Generate Stress World` (git-ignored output, ~30 MB), then point
  `EditorAppBootstrap._profile` at `Settings/Worlds/StressWorld.asset`. Commit the scene with
  `DefaultWorld` only. Streaming can be measured headless by moving `_focus` and calling
  `WorldStreamer.Tick` via `execute_code`; real frame times need Moti flying with the window focused.
- While the Editor window is unfocused, play mode doesn't advance frames (`Time.frameCount`
  stays put), so `Object.Destroy` stays pending and child counts look too high. Check
  `WorldView.PresentedCount` instead of counting scene children.
- Play mode is fair game: stop it yourself (`manage_editor stop`) whenever you need to compile,
  test or verify. Moti doesn't save `forge.user` from it; he only forgets to stop it.
- Verify the running editor: `manage_editor play`, then `execute_code` (reflection on
  `EditorController._context`) and `manage_camera screenshot`. To look at something, set
  `Camera.main.transform` directly (frames are paused while Unity is unfocused, so a camera glide
  never finishes).
- Test the agent live: needs `claude` on PATH and logged in, and uses Moti's subscription. In one
  `execute_code` call, set `Application.runInBackground = true` (the editor is unfocused) and call
  `EditorController.Agent.Conversation.Send(...)`; then poll `Conversation.State` and `Entries`.
  The agent host turns background running back off when the request ends. Its Claude Code
  conversations live in `<persistentDataPath>/Agent`.
- RAM: Moti's machine has 16 GB. Unity's import workers take ~1 GB each, so the desired import
  worker count is 2 on this machine (`AssetDatabase.DesiredWorkerCount`, stored in the git-ignored
  `UserSettings/EditorUserSettings.asset`; it was 7 and the PC ran out of memory). Raise it once
  the RAM upgrade is in.

## Definition of done for a change

1. Compiles with no console errors or warnings from our code.
2. EditMode tests pass, and new logic has tests.
3. Respects the dependency rule and conventions in `ARCHITECTURE.md`.
4. Any shortcut is flagged with `// SHORTCUT:` and listed in `ARCHITECTURE.md`.
