# Revit 2027 MCP

A Model Context Protocol (MCP) integration system for Autodesk Revit 2027, allowing any MCP-compatible AI Assistant (such as Antigravity IDE, Claude Desktop, Cursor, or VS Code) to query, inspect, and modify Revit BIM models.

> **Tested Environment**: This integration has been tested and verified using **Autodesk Revit 2027** and **Antigravity IDE**.

---

## Architecture

```
MCP-Compatible AI Client (Antigravity / Claude Desktop / Cursor / VS Code)
   │ (StdIO)
   ▼
Python FastMCP Server (src/mcp-server/)
   │ (HTTP REST: http://localhost:8000)
   ▼
Revit Bridge Add-in (C# .NET 10 / Revit 2027)
   │ (ExternalEvent / UI Main Thread)
   ▼
Autodesk Revit 2027 Model
```

---

## Quick Setup

1. Run the automated installer:
   ```cmd
   install.bat
   ```
2. Copy the generated MCP JSON configuration snippet into your AI Assistant settings (`mcp_config.json`).
3. Open Autodesk Revit 2027. The HTTP bridge automatically starts in the background.

---

## Available MCP Tools (11 Tools)

| MCP Tool | Description |
| :--- | :--- |
| `check_revit_status()` | Check HTTP bridge connection and active document status. |
| `get_active_selection()` | Get currently selected elements (`element_id`, `category`, `name`). |
| `select_elements_in_revit(element_ids)` | Highlight/select element IDs in active Revit window. |
| `get_element_parameters(element_id)` | Read all instance and type parameters of an element. |
| `get_element_info(element_id)` | Get element geometry details, level, category, type, and bounding box. |
| `get_elements_by_category(category_name, limit)` | Query elements by category name (e.g., "Walls", "Doors", "Windows", "Rooms"). |
| `set_element_parameter(element_id, param_name, value)` | Modify element parameter value inside a Revit Transaction. |
| `get_project_info()` | Read project metadata (Title, Number, Client, Address, etc.). |
| `get_levels()` | Get all building levels and elevations. |
| `get_views()` | Get list of project views (Floor Plans, 3D Views, Sections, Sheets). |
| `delete_elements(element_ids)` | Delete elements from Revit model by IDs. |

---

## Uninstallation

To cleanly remove the add-in and virtual environment:
```cmd
uninstall.bat
```

---

## License & Disclaimer

This project is licensed under the **MIT License**.

> **Disclaimer**: This software is provided "AS IS", without warranty of any kind, express or implied. Use at your own risk. The author and contributors are not responsible for any data loss, model corruption, or damages resulting from the use of this software.

---

Copyright (c) 2026 Arman Arisman. All rights reserved.
