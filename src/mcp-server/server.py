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


if __name__ == "__main__":
    mcp.run()
