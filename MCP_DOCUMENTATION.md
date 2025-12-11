# Unity MCP Server - Technical Documentation

## Overview

The Unity MCP (Model Context Protocol) Server is a C# package that enables AI agents (like Antigravity, Claude, Cursor) to query and control the Unity Editor through a standardized JSON-RPC 2.0 interface.

**Version: 2.0.0**

---

## Architecture

```
┌─────────────────────┐     stdio      ┌─────────────────────┐    HTTP/SSE    ┌─────────────────────┐
│   Antigravity IDE   │ ◄───────────► │   mcp-bridge.js     │ ◄────────────► │   Unity McpServer   │
│   (MCP Client)      │               │   (Node.js Proxy)   │                │   (C# HttpListener) │
└─────────────────────┘               └─────────────────────┘                └─────────────────────┘
```

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| **McpServer.cs** | `Editor/Core/` | HTTP server with SSE support |
| **JsonRpcHandler.cs** | `Editor/Core/` | JSON-RPC 2.0 protocol handler |
| **ToolRegistry.cs** | `Editor/Core/` | Tool discovery and invocation |
| **ResourceHandler.cs** | `Editor/Core/` | MCP Resource operations |
| **mcp-bridge.js** | `Bridge/` | stdio-to-HTTP proxy (bundled) |

---

## Available Tools (27 Total)

### GameObject Tools (6)
| Tool | Description |
|------|-------------|
| `unity_create_gameobject` | Create a new GameObject (with optional primitive type) |
| `unity_delete_gameobject` | Delete a GameObject from the scene |
| `unity_set_transform` | Set position, rotation, scale of a GameObject |
| `unity_add_component` | Add a component to a GameObject |
| `unity_remove_component` | Remove a component from a GameObject |
| `unity_set_component_property` | Set a property value on a component |

### Hierarchy Tools (3)
| Tool | Description |
|------|-------------|
| `unity_get_hierarchy` | Get scene hierarchy (max depth 3) |
| `unity_get_gameobject` | Get detailed info for a specific GameObject |
| `unity_get_components` | List all components on a GameObject |

### Prefab Tools (4)
| Tool | Description |
|------|-------------|
| `unity_instantiate_prefab` | Instantiate a prefab in the scene |
| `unity_get_prefab_info` | Get detailed prefab structure |
| `unity_create_prefab` | Create a prefab from a scene GameObject |
| `unity_unpack_prefab` | Unpack a prefab instance |

### Scene Tools (6)
| Tool | Description |
|------|-------------|
| `unity_get_scenes` | List all scenes in the project |
| `unity_open_scene` | Open a scene (single or additive) |
| `unity_save_scene` | Save the current or specific scene |
| `unity_new_scene` | Create a new empty scene |
| `unity_close_scene` | Close a scene (multi-scene editing) |
| `unity_set_active_scene` | Set the active scene |

### Asset Tools (2)
| Tool | Description |
|------|-------------|
| `unity_get_assets` | List assets in a folder (max 100 results) |
| `unity_get_project_settings` | Get Unity project settings |

### Console Tools (2)
| Tool | Description |
|------|-------------|
| `unity_get_console_logs` | Get recent Unity console logs |
| `unity_clear_console` | Clear the Unity console |

### Editor Tools (4)
| Tool | Description |
|------|-------------|
| `unity_execute_menu` | Execute a Unity menu item |
| `unity_select_object` | Select a GameObject in Editor |
| `unity_get_selection` | Get currently selected objects |
| `unity_get_editor_state` | Get editor state (playing, paused, etc.) |

### Compilation Tools (3)
| Tool | Description |
|------|-------------|
| `unity_get_compilation_status` | Get compilation status and errors |
| `unity_recompile_scripts` | Force script recompilation |
| `unity_get_assemblies` | Get info about project assemblies |

---

## MCP Resources

The server supports MCP resource operations for reading project files:

### Supported Resource Types
| Type | URI Pattern | Description |
|------|-------------|-------------|
| Scripts | `unity://script/{path}` | C# source code files |
| Scenes | `unity://scene/{path}` | Scene metadata |
| Prefabs | `unity://prefab/{path}` | Prefab structure (JSON) |
| ScriptableObjects | `unity://scriptableobject/{path}` | SO data (JSON) |
| Files | `unity://file/{path}` | Any text file |

### Resource Methods
- `resources/list` - List all available resources
- `resources/read` - Read a specific resource by URI

---

## Quick Start

### 1. Open MCP Server Window
`Window > MCP Server`

### 2. Copy Configuration
Click "Copy Config to Clipboard" and paste into your MCP client's `mcp_config.json`:

```json
{
  "mcpServers": {
    "unity": {
      "command": "node",
      "args": ["path/to/mcp-bridge.js"]
    }
  }
}
```

### 3. Start Using
The server auto-starts when Unity loads. Your AI agent can now:
- Query scene hierarchy
- Create/modify GameObjects
- Manage scenes and prefabs
- Read project files
- And much more!

---

## Server Endpoints

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/` | POST | JSON-RPC requests (legacy) |
| `/message` | POST | JSON-RPC requests |
| `/sse` | GET | Server-Sent Events stream |

---

## Environment Variables

The bridge supports these environment variables:
- `UNITY_MCP_PORT` - Server port (default: 3000)
- `UNITY_MCP_HOST` - Server host (default: localhost)

---

## Changelog

### v2.0.0
- Added 6 GameObject write operations
- Added 4 Prefab tools
- Added 6 Scene tools
- Added 3 Compilation tools
- Added MCP Resource support (resources/list, resources/read)
- Bundled mcp-bridge.js into package
- Updated server window with better configuration UI
- Auto-start server on Unity load

### v1.1.0
- Added SSE support
- Fixed JSON-RPC nested object parsing
- Added resource capabilities

### v1.0.0
- Initial release
- Basic read-only tools
- HTTP POST server

---

## Status: ✓ FULLY OPERATIONAL

The Unity MCP Server v2.0.0 is fully functional with 27 tools and resource support.
