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

## Definition of done for a change

1. Compiles with no console errors or warnings from our code.
2. EditMode tests pass, and new logic has tests.
3. Respects the dependency rule and conventions in `ARCHITECTURE.md`.
4. Any shortcut is flagged with `// SHORTCUT:` and listed in `ARCHITECTURE.md`.
