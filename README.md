# Unity MCP Server

[![Unity 2021.3+](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)](https://unity.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![MCP](https://img.shields.io/badge/MCP-Compatible-purple.svg)](https://modelcontextprotocol.io)

A **Model Context Protocol (MCP)** server for Unity that enables AI agents to interact with the Unity Editor.

## What is MCP?

MCP is an open standard by Anthropic that allows AI systems to access external tools and data. This package turns Unity into an MCP server, letting AI assistants like **Antigravity**, **Claude**, and **Cursor** query your scenes, assets, and execute editor commands.

## Features

- 🎮 **Scene Hierarchy** - Query GameObjects, components, and structure
- 📦 **Asset Browser** - List and search project assets
- 📋 **Console Access** - Read Unity console logs
- ⚙️ **Editor Control** - Execute menu items, select objects
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

1. Open `Window > MCP Server`
2. Click **Start Server**
3. Configure your MCP client with the displayed URL

## Available Tools

| Tool | Description |
|------|-------------|
| `unity_get_hierarchy` | Get scene GameObject hierarchy |
| `unity_get_gameobject` | Get details of a specific GameObject |
| `unity_get_components` | List components on a GameObject |
| `unity_get_assets` | List assets in a folder |
| `unity_get_project_settings` | Get project configuration |
| `unity_get_console_logs` | Get recent console logs |
| `unity_clear_console` | Clear the console |
| `unity_execute_menu` | Execute a menu item |
| `unity_select_object` | Select a GameObject |
| `unity_get_selection` | Get current selection |
| `unity_get_editor_state` | Get editor play/pause state |

## MCP Client Configuration

Add to your MCP client config (e.g., Antigravity, Claude Desktop):

```json
{
  "mcpServers": {
    "unity": {
      "url": "http://localhost:3000/"
    }
  }
}
```

## Example Queries

Once connected, ask your AI assistant:
- "What GameObjects are in my current scene?"
- "Show me the components on the Player object"
- "List all prefabs in the Assets folder"
- "What errors are in the console?"

## Related Packages

- [Antigravity IDE Support](https://github.com/usmanbutt-dev/antigravity-unity) - IDE integration for Unity

## License

MIT License - see [LICENSE](LICENSE)
