# Revit 2027 MCP

A Model Context Protocol (MCP) integration system for Autodesk Revit 2027, allowing any MCP-compatible AI Assistant (such as Antigravity IDE, Claude Desktop, Cursor, or VS Code) to query, inspect, create, and modify Revit BIM models.

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
   │ (ExternalEvent / UI Main Thread + Transaction)
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

> **Units Note**: All geometry creation and coordinate inputs use **millimeters (`mm`)** and are automatically converted to Revit's internal unit (`feet`) inside the C# bridge.

---

## Available MCP Tools (18 Tools)

### 1. Read & Inspection Tools (10 Tools)

| MCP Tool | Description |
| :--- | :--- |
| `check_revit_status()` | Check HTTP bridge connection and active document status. |
| `get_project_info()` | Read project metadata (Title, Number, Client, Address, etc.). |
| `get_levels()` | Get all building levels and elevations (in `mm` and `ft`). |
| `get_views()` | Get list of project views (Floor Plans, 3D Views, Sections, Sheets). |
| `get_active_selection()` | Get currently selected elements (`element_id`, `category`, `name`). |
| `get_elements_by_category(category_name, limit)` | Query elements by category name (e.g., `"Walls"`, `"Doors"`, `"Windows"`, `"Rooms"`). |
| `get_available_types(category_name, limit)` | List loaded `ElementType` / `FamilySymbol` types in the document (e.g., `"Walls"`, `"Floors"`, `"Doors"`, `"Windows"`, `"Furniture"`). |
| `get_element_info(element_id)` | Get element geometry details, level, category, type, location (`mm`), and bounding box (`mm`). |
| `get_element_parameters(element_id)` | Read all instance and type parameters of an element. |
| `select_elements_in_revit(element_ids)` | Highlight/select element IDs in the active Revit window. |

### 2. Write, Create & Modify Tools (8 Tools)

| MCP Tool | Description |
| :--- | :--- |
| `create_level(elevation_mm, name)` | Create a new building `Level` at the specified elevation in `mm`. |
| `create_grid(start_x_mm, start_y_mm, end_x_mm, end_y_mm, name)` | Create a datum `Grid` line between two XY coordinates in `mm`. |
| `create_wall(start_x_mm, start_y_mm, end_x_mm, end_y_mm, height_mm, level_name, wall_type_name, offset_mm, structural)` | Create a `Wall` between two XY points in `mm` with customizable height, level, and wall type. |
| `create_floor(points_mm, level_name, floor_type_name)` | Create a `Floor` slab from a closed polygon of `[x_mm, y_mm]` boundary coordinates. |
| `place_family_instance(x_mm, y_mm, category_or_family_name, type_name, z_mm, level_name, host_element_id, rotation_deg)` | Place a `FamilyInstance` component (Doors, Windows on a host Wall via `host_element_id`, or standalone Furniture/Columns with rotation). |
| `create_room(x_mm, y_mm, room_name, room_number, level_name)` | Create an architectural `Room` at `(x_mm, y_mm)` with room name and number. |
| `set_element_parameter(element_id, param_name, value)` | Modify an existing element's parameter value inside a Revit `Transaction`. |
| `delete_elements(element_ids)` | Delete one or multiple elements from the Revit model by IDs. |

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
