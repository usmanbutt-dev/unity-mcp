# Unity MCP Server

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![MCP](https://img.shields.io/badge/MCP-Compatible-purple.svg)](https://modelcontextprotocol.io)
[![Version](https://img.shields.io/badge/Version-2.2.0-orange.svg)](CHANGELOG.md)

A **Model Context Protocol (MCP)** server for Unity that enables AI agents to **query and control** the Unity Editor.

## What is MCP?

MCP is an open standard by Anthropic that allows AI systems to access external tools and data. This package turns Unity into an MCP server, letting AI assistants like **Antigravity**, **Claude**, and **Cursor** query your scenes, assets, and execute editor commands.

## Features

- 🎮 **Scene Hierarchy** - Query GameObjects, components, and structure
- ✏️ **Write Operations** - Create, delete, and modify GameObjects in real-time
- 🧩 **Component Control** - Add, remove, and configure components
- 🎬 **Scene Management** - Open, save, create, and manage scenes
- 🏷️ **Prefab Tools** - Instantiate, create, and inspect prefabs
- 📦 **Asset Browser** - List and search project assets
- 📁 **Resource Access** - Read scripts, prefabs, and ScriptableObjects
- 📋 **Console Access** - Read and clear Unity console logs
- ⚙️ **Editor Control** - Execute menu items, select objects
- 🔧 **Compilation Status** - Monitor build errors and warnings
- 📷 **Screenshots** - Capture Game View or Scene View (v2.1)
- 🔍 **Project Search** - Search by name, content, or references (v2.1)
- ▶️ **Play Mode Control** - Enter/exit/pause play mode (v2.1)
- 🎮 **Input Simulation** - Keyboard, mouse, and UI interactions (v2.1)
- 📋 **Schema Support** - Full JSON Schema for all tool parameters (v2.1)
- 🔒 **Secure** - Localhost only, no external access

## Installation

### Via Git URL (Recommended)

1. Open `Window > Package Manager`
2. Click `+` > `Add package from git URL...`
3. Enter:
   ```
   https://github.com/usmanbutt-dev/unity-mcp.git
   ```

## Quick Start

1. The server **auto-starts** when Unity loads
2. Open `Window > MCP Server` to view status
3. Click **"Copy Config to Clipboard"**
4. Paste into your MCP client's configuration file

## Available Tools (52 Total)

### GameObject Tools
| Tool | Description |
|------|-------------|
| `unity_create_gameobject` | Create new GameObjects (primitives supported) |
| `unity_delete_gameobject` | Delete GameObjects from scene |
| `unity_set_transform` | Set position, rotation, scale |
| `unity_add_component` | Add components to GameObjects |
| `unity_remove_component` | Remove components |
| `unity_set_component_property` | Set component property values |

### Hierarchy Tools
| Tool | Description |
|------|-------------|
| `unity_get_hierarchy` | Get scene GameObject hierarchy |
| `unity_get_gameobject` | Get details of a specific GameObject |
| `unity_get_components` | List components on a GameObject |

### Prefab Tools
| Tool | Description |
|------|-------------|
| `unity_instantiate_prefab` | Instantiate prefabs in scene |
| `unity_get_prefab_info` | Get prefab structure |
| `unity_create_prefab` | Create prefab from GameObject |
| `unity_unpack_prefab` | Unpack prefab instances |

### Scene Tools
| Tool | Description |
|------|-------------|
| `unity_get_scenes` | List all scenes in project |
| `unity_open_scene` | Open a scene |
| `unity_save_scene` | Save current scene |
| `unity_new_scene` | Create new scene |
| `unity_close_scene` | Close a scene |
| `unity_set_active_scene` | Set active scene |

### Asset & Editor Tools
| Tool | Description |
|------|-------------|
| `unity_get_assets` | List assets in a folder |
| `unity_get_project_settings` | Get project configuration |
| `unity_get_console_logs` | Get recent console logs |
| `unity_clear_console` | Clear the console |
| `unity_execute_menu` | Execute a menu item |
| `unity_select_object` | Select a GameObject |
| `unity_get_selection` | Get current selection |
| `unity_get_editor_state` | Get editor play/pause state |

### Compilation Tools
| Tool | Description |
|------|-------------|
| `unity_get_compilation_status` | Get compile errors/warnings |
| `unity_recompile_scripts` | Force recompilation |
| `unity_get_assemblies` | List project assemblies |

### Screenshot & Search Tools (v2.1)
| Tool | Description |
|------|-------------|
| `unity_take_screenshot` | Capture Game View or Scene View as base64 PNG |
| `unity_search_project` | Search by name, content, or asset references |

### Play Mode & Input Tools (v2.1)
| Tool | Description |
|------|-------------|
| `unity_enter_play_mode` | Enter play mode |
| `unity_exit_play_mode` | Exit play mode |
| `unity_pause_play_mode` | Pause/unpause play mode |
| `unity_simulate_key` | Simulate keyboard input |
| `unity_simulate_mouse` | Simulate mouse clicks |
| `unity_click_ui_element` | Click UI buttons/toggles by name |

### Animation Tools (v2.2)
| Tool | Description |
|------|-------------|
| `unity_set_animator_parameter` | Set bool/float/int/trigger parameters |
| `unity_get_animator_info` | Get animator state, parameters, layers |
| `unity_play_animation` | Play animation state by name |

### Material & Shader Tools (v2.2)
| Tool | Description |
|------|-------------|
| `unity_get_material_info` | Get material properties and shader |
| `unity_set_material_property` | Set color/float/int/vector properties |
| `unity_set_material` | Assign material to renderer |

### Physics Tools (v2.2)
| Tool | Description |
|------|-------------|
| `unity_raycast` | Cast ray and get hit info |
| `unity_overlap_sphere` | Find colliders in radius |
| `unity_add_force` | Apply force to Rigidbody |

### Asset Creation Tools (v2.2)
| Tool | Description |
|------|-------------|
| `unity_create_folder` | Create project folders |
| `unity_create_material` | Create material assets |
| `unity_create_script` | Create C# scripts with templates |
| `unity_move_asset` | Move/rename assets |
| `unity_duplicate_asset` | Duplicate assets |

### AI Context Tools (v2.2)
| Tool | Description |
|------|-------------|
| `unity_get_scene_summary` | Compact scene overview for AI |
| `unity_get_component_schema` | Get component properties schema |
| `unity_get_type_info` | Discover Unity component types |

## MCP Resources

The server also provides resource access via MCP resources protocol:
- **Scripts** - Read C# source files
- **Scenes** - Get scene metadata
- **Prefabs** - Read prefab structure
- **ScriptableObjects** - Read SO data as JSON

## MCP Client Configuration

Add to your MCP client config (e.g., `mcp_config.json`):

```json
{
  "mcpServers": {
    "unity": {
      "command": "node",
      "args": ["path/to/Packages/com.community.unity-mcp/Bridge/mcp-bridge.js"]
    }
  }
}
```

> **Note**: Use the "Copy Config to Clipboard" button in `Window > MCP Server` to get the correct path.

## Example Queries

Once connected, ask your AI assistant:
- "Create a red cube at position (0, 2, 0)"
- "Add a Rigidbody to the Player object"
- "What GameObjects are in my current scene?"
- "Show me the components on the Player object"
- "Open the MainMenu scene"
- "What compilation errors do I have?"

## Requirements

- Unity 2021.3 or later
- Node.js (for the MCP bridge)

## Related Packages

- [Antigravity IDE Support](https://github.com/usmanbutt-dev/antigravity-unity) - IDE integration for Unity

## License

MIT License - see [LICENSE](LICENSE)
