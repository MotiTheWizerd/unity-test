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

## Definition of done for a change

1. Compiles with no console errors or warnings from our code.
2. EditMode tests pass, and new logic has tests.
3. Respects the dependency rule and conventions in `ARCHITECTURE.md`.
4. Any shortcut is flagged with `// SHORTCUT:` and listed in `ARCHITECTURE.md`.
