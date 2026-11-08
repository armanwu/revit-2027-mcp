import json
from typing import List, Optional
import httpx

try:
    from fastmcp import FastMCP
except ImportError:
    try:
        from mcp.server.fastmcp import FastMCP
    except ImportError:
        try:
            from mcp.server import FastMCP
        except ImportError:
            from mcp.server.mcpserver import MCPServer as FastMCP

# Initialize FastMCP Server
mcp = FastMCP("Revit 2027 MCP Server")

REVIT_BRIDGE_URL = "http://localhost:8000"


@mcp.tool()
async def check_revit_status() -> str:
    """Check connection status with Autodesk Revit 2027 REST HTTP Bridge and active document info."""
    async with httpx.AsyncClient(timeout=5.0) as client:
        try:
            response = await client.get(f"{REVIT_BRIDGE_URL}/health")
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "offline",
                "message": f"Failed to connect to Revit Bridge at {REVIT_BRIDGE_URL}: {str(e)}"
            })


@mcp.tool()
async def get_active_selection() -> str:
    """Get ElementId, Category, and Name of elements currently selected in the active Revit model."""
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            response = await client.get(f"{REVIT_BRIDGE_URL}/selection")
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to retrieve selection from Revit: {str(e)}"
            })


@mcp.tool()
async def select_elements_in_revit(element_ids: List[int]) -> str:
    """Select/highlight specific elements in Autodesk Revit UI by a list of Element IDs.

    Args:
        element_ids: List of numeric Element IDs to select in Revit UI.
    """
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            payload = {"element_ids": element_ids}
            response = await client.post(f"{REVIT_BRIDGE_URL}/selection/set", json=payload)
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to select elements in Revit: {str(e)}"
            })


@mcp.tool()
async def get_element_parameters(element_id: int) -> str:
    """Get ALL parameters (Instance & Type Parameters) for a Revit element by Element ID.

    Args:
        element_id: Numeric ID of the Revit element.
    """
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            payload = {"element_id": element_id}
            response = await client.post(f"{REVIT_BRIDGE_URL}/element/parameters", json=payload)
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to retrieve parameters for element {element_id}: {str(e)}"
            })


@mcp.tool()
async def get_element_info(element_id: int) -> str:
    """Get detailed geometry, level, category, type name, location, and bounding box for a Revit element.

    Args:
        element_id: Numeric ID of the Revit element.
    """
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            payload = {"element_id": element_id}
            response = await client.post(f"{REVIT_BRIDGE_URL}/element/info", json=payload)
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to retrieve info for element {element_id}: {str(e)}"
            })


@mcp.tool()
async def get_elements_by_category(category_name: str, limit: int = 100) -> str:
    """Query elements in the Revit project by category name (e.g., 'Walls', 'Doors', 'Windows', 'Rooms', 'Furniture').

    Args:
        category_name: Revit category name (case-insensitive).
        limit: Maximum number of elements to return (default 100).
    """
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            payload = {"category_name": category_name, "limit": limit}
            response = await client.post(f"{REVIT_BRIDGE_URL}/elements/by-category", json=payload)
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to query elements for category '{category_name}': {str(e)}"
            })


@mcp.tool()
async def set_element_parameter(element_id: int, param_name: str, value: str) -> str:
    """Modify parameter value of a specific Revit element inside a Transaction.

    Args:
        element_id: Numeric ID of the Revit element.
        param_name: Parameter name to modify.
        value: New parameter value (string format).
    """
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            payload = {
                "element_id": element_id,
                "param_name": param_name,
                "value": str(value)
            }
            response = await client.post(f"{REVIT_BRIDGE_URL}/parameter/set", json=payload)
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to modify parameter: {str(e)}"
            })


@mcp.tool()
async def get_project_info() -> str:
    """Get project metadata (Project Title, Project Number, Client Name, Address, etc.)."""
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            response = await client.get(f"{REVIT_BRIDGE_URL}/project/info")
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to retrieve project information: {str(e)}"
            })


@mcp.tool()
async def get_levels() -> str:
    """Get list of all Levels in the Revit project with elevation values."""
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            response = await client.get(f"{REVIT_BRIDGE_URL}/levels")
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to retrieve levels: {str(e)}"
            })


@mcp.tool()
async def get_views() -> str:
    """Get list of project views (Floor Plan, 3D View, Section, Schedule, Sheet)."""
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            response = await client.get(f"{REVIT_BRIDGE_URL}/views")
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to retrieve views: {str(e)}"
            })


@mcp.tool()
async def delete_elements(element_ids: List[int]) -> str:
    """Delete specified elements from the Revit model by Element IDs inside a Transaction.

    Args:
        element_ids: List of numeric Element IDs to delete.
    """
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            payload = {"element_ids": element_ids}
            response = await client.post(f"{REVIT_BRIDGE_URL}/elements/delete", json=payload)
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to delete elements: {str(e)}"
            })


@mcp.tool()
async def get_available_types(category_name: str = "", limit: int = 200) -> str:
    """Get available Element Types / Family Symbols loaded in the Revit project (e.g., 'Walls', 'Floors', 'Doors', 'Windows', 'Furniture', 'Columns').

    Args:
        category_name: Optional Revit category filter (case-insensitive, e.g. 'Walls', 'Doors', 'Floors'). Leave empty for all categories.
        limit: Maximum number of element types to return (default 200).
    """
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            payload = {"category_name": category_name, "limit": limit}
            response = await client.post(f"{REVIT_BRIDGE_URL}/elements/types", json=payload)
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to retrieve available element types: {str(e)}"
            })


@mcp.tool()
async def create_level(elevation_mm: float, name: Optional[str] = None) -> str:
    """Create a new building Level in the Revit project at the specified elevation (in millimeters).

    Args:
        elevation_mm: Elevation height in millimeters (mm), e.g., 4000 for +4.00m.
        name: Optional name for the new Level (e.g., 'Level 2' or 'Lantai 2').
    """
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            payload = {"elevation_mm": elevation_mm, "name": name}
            response = await client.post(f"{REVIT_BRIDGE_URL}/levels/create", json=payload)
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to create level: {str(e)}"
            })


@mcp.tool()
async def create_grid(
    start_x_mm: float,
    start_y_mm: float,
    end_x_mm: float,
    end_y_mm: float,
    name: Optional[str] = None
) -> str:
    """Create a datum Grid line in the Revit project between two XY points (in millimeters).

    Args:
        start_x_mm: Start X coordinate in millimeters (mm).
        start_y_mm: Start Y coordinate in millimeters (mm).
        end_x_mm: End X coordinate in millimeters (mm).
        end_y_mm: End Y coordinate in millimeters (mm).
        name: Optional Grid label/name (e.g., 'A', 'B', '1', '2').
    """
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            payload = {
                "start_x_mm": start_x_mm,
                "start_y_mm": start_y_mm,
                "end_x_mm": end_x_mm,
                "end_y_mm": end_y_mm,
                "name": name
            }
            response = await client.post(f"{REVIT_BRIDGE_URL}/grids/create", json=payload)
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to create grid: {str(e)}"
            })


@mcp.tool()
async def create_wall(
    start_x_mm: float,
    start_y_mm: float,
    end_x_mm: float,
    end_y_mm: float,
    height_mm: float = 3000.0,
    level_name: Optional[str] = None,
    wall_type_name: Optional[str] = None,
    offset_mm: float = 0.0,
    structural: bool = False
) -> str:
    """Create a new Wall in the Revit model from start (X, Y) to end (X, Y) in millimeters.

    Args:
        start_x_mm: Start point X coordinate in millimeters (mm).
        start_y_mm: Start point Y coordinate in millimeters (mm).
        end_x_mm: End point X coordinate in millimeters (mm).
        end_y_mm: End point Y coordinate in millimeters (mm).
        height_mm: Unconnected wall height in millimeters (default 3000 mm).
        level_name: Optional target Level name (defaults to the lowest Level if omitted).
        wall_type_name: Optional WallType name (use get_available_types('Walls') to list options; uses default WallType if omitted).
        offset_mm: Base offset from the Level in millimeters (default 0 mm).
        structural: Whether the wall is structural (default False).
    """
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            payload = {
                "start_x_mm": start_x_mm,
                "start_y_mm": start_y_mm,
                "end_x_mm": end_x_mm,
                "end_y_mm": end_y_mm,
                "height_mm": height_mm,
                "level_name": level_name,
                "wall_type_name": wall_type_name,
                "offset_mm": offset_mm,
                "structural": structural
            }
            response = await client.post(f"{REVIT_BRIDGE_URL}/walls/create", json=payload)
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to create wall: {str(e)}"
            })


@mcp.tool()
async def create_floor(
    points_mm: List[List[float]],
    level_name: Optional[str] = None,
    floor_type_name: Optional[str] = None
) -> str:
    """Create a new Floor slab from a closed polygon of 2D boundary points (in millimeters).

    Args:
        points_mm: List of [x_mm, y_mm] coordinate pairs defining the floor boundary polygon (at least 3 points, e.g. [[0, 0], [5000, 0], [5000, 4000], [0, 4000]]).
        level_name: Optional target Level name (defaults to the lowest Level if omitted).
        floor_type_name: Optional FloorType name (use get_available_types('Floors') to list options; uses default FloorType if omitted).
    """
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            payload = {
                "points_mm": points_mm,
                "level_name": level_name,
                "floor_type_name": floor_type_name
            }
            response = await client.post(f"{REVIT_BRIDGE_URL}/floors/create", json=payload)
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to create floor: {str(e)}"
            })


@mcp.tool()
async def place_family_instance(
    x_mm: float,
    y_mm: float,
    category_or_family_name: str = "",
    type_name: str = "",
    z_mm: float = 0.0,
    level_name: Optional[str] = None,
    host_element_id: Optional[int] = None,
    rotation_deg: float = 0.0
) -> str:
    """Place a FamilyInstance component (e.g., Door, Window, Furniture, Column, Plumbing Fixture) in the Revit model.

    Args:
        x_mm: Placement X coordinate in millimeters (mm).
        y_mm: Placement Y coordinate in millimeters (mm).
        category_or_family_name: Category or Family name filter (e.g., 'Doors', 'Windows', 'Furniture', 'Columns', 'Single-Flush').
        type_name: Specific FamilySymbol / Type name (optional; if omitted, uses the first matching symbol in the category/family).
        z_mm: Vertical offset from the target Level in millimeters (default 0 mm; useful for Windows sill height).
        level_name: Optional target Level name (defaults to the lowest Level if omitted).
        host_element_id: Optional host Element ID (required for wall-hosted components like Doors and Windows — pass the Wall's element_id).
        rotation_deg: Rotation angle around Z-axis in degrees for non-hosted families (default 0.0).
    """
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            payload = {
                "x_mm": x_mm,
                "y_mm": y_mm,
                "z_mm": z_mm,
                "category_or_family_name": category_or_family_name,
                "type_name": type_name,
                "level_name": level_name,
                "host_element_id": host_element_id,
                "rotation_deg": rotation_deg
            }
            response = await client.post(f"{REVIT_BRIDGE_URL}/family-instance/place", json=payload)
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to place family instance: {str(e)}"
            })


@mcp.tool()
async def create_room(
    x_mm: float,
    y_mm: float,
    room_name: Optional[str] = None,
    room_number: Optional[str] = None,
    level_name: Optional[str] = None
) -> str:
    """Create a new architectural Room at the specified (X, Y) coordinate (in millimeters) inside an enclosed boundary.

    Args:
        x_mm: Room placement point X coordinate in millimeters (mm).
        y_mm: Room placement point Y coordinate in millimeters (mm).
        room_name: Optional name for the room (e.g., 'Living Room', 'Bedroom 1').
        room_number: Optional room number (e.g., '101', 'A-01').
        level_name: Optional target Level name (defaults to the lowest Level if omitted).
    """
    async with httpx.AsyncClient(timeout=15.0) as client:
        try:
            payload = {
                "x_mm": x_mm,
                "y_mm": y_mm,
                "room_name": room_name,
                "room_number": room_number,
                "level_name": level_name
            }
            response = await client.post(f"{REVIT_BRIDGE_URL}/rooms/create", json=payload)
            return response.text
        except Exception as e:
            return json.dumps({
                "status": "error",
                "message": f"Failed to create room: {str(e)}"
            })


if __name__ == "__main__":
    mcp.run()
