# Changelog

All notable changes to this project will be documented in this file.

## [2.1.0] - 2024-12-12

### Added
- **Dynamic Schema Generation**: Tools now include full JSON Schema in `tools/list` response
  - LLMs can now see parameter names, types, descriptions, and valid enum values
  - Uses `[McpParam]` attribute for field documentation
  
- **Screenshot Tools** (Phase 2A)
  - `unity_take_screenshot` - Capture Game View or Scene View as base64 PNG

- **Search Tools** (Phase 2B)
  - `unity_search_project` - Search by name, content (grep), or asset references

- **Play Mode Controls** (Phase 3A)
  - `unity_enter_play_mode` - Enter play mode
  - `unity_exit_play_mode` - Exit play mode
  - `unity_pause_play_mode` - Pause/unpause with toggle support

- **Input Simulation** (Phase 3B)
  - `unity_simulate_key` - Simulate keyboard input
  - `unity_simulate_mouse` - Simulate mouse clicks
  - `unity_click_ui_element` - Click UI Buttons/Toggles by name

### Changed
- All tool definitions now include complete input schemas
- Added `[McpParam]` attribute for parameter documentation

---

## [2.0.0] - 2024-12-11

### Added
- **6 GameObject Write Tools**
  - `unity_create_gameobject` - Create new GameObjects with optional primitive types
  - `unity_delete_gameobject` - Delete GameObjects with undo support
  - `unity_set_transform` - Set position, rotation, scale (world or local)
  - `unity_add_component` - Add components to GameObjects
  - `unity_remove_component` - Remove components
  - `unity_set_component_property` - Set serialized properties on components

- **4 Prefab Tools**
  - `unity_instantiate_prefab` - Spawn prefabs in scene
  - `unity_get_prefab_info` - Get prefab structure
  - `unity_create_prefab` - Create prefab from scene GameObject
  - `unity_unpack_prefab` - Unpack prefab instances

- **6 Scene Tools**
  - `unity_get_scenes` - List all scenes in project
  - `unity_open_scene` - Open scenes (single or additive)
  - `unity_save_scene` - Save current or specific scene
  - `unity_new_scene` - Create new empty scene
  - `unity_close_scene` - Close scenes (multi-scene editing)
  - `unity_set_active_scene` - Set active scene

- **3 Compilation Tools**
  - `unity_get_compilation_status` - Get compile errors and warnings
  - `unity_recompile_scripts` - Force script recompilation
  - `unity_get_assemblies` - List project assemblies

- **MCP Resource Support**
  - `resources/list` - List scripts, scenes, prefabs, ScriptableObjects
  - `resources/read` - Read resource content by URI

- **Server Enhancements**
  - Auto-start on Unity load
  - SSE (Server-Sent Events) support
  - Bundled Node.js bridge (`Bridge/mcp-bridge.js`)
  - Improved editor window with config copy button

### Fixed
- JSON parsing for nested params (manual parsing for tools/call)
- Scale defaulting to (0,0,0) when not specified

### Changed
- Protocol version updated to `2024-11-05`
- Server version now `2.0.0`

---

## [1.0.0] - 2024-12-11

### Added
- Initial release
- HTTP server with JSON-RPC 2.0 protocol
- MCP capability negotiation
- Editor window for server control
- 11 MCP tools:
  - `unity_get_hierarchy` - Scene hierarchy
  - `unity_get_gameobject` - GameObject details
  - `unity_get_components` - Component listing
  - `unity_get_assets` - Asset browsing
  - `unity_get_project_settings` - Project config
  - `unity_get_console_logs` - Console access
  - `unity_clear_console` - Clear console
  - `unity_execute_menu` - Menu execution
  - `unity_select_object` - Object selection
  - `unity_get_selection` - Selection query
  - `unity_get_editor_state` - Editor state
