@echo off
setlocal

echo ===================================================
echo   REVIT 2027 MCP INTEGRATION SYSTEM UNINSTALLER
echo ===================================================
echo.

set "ROOT_DIR=%~dp0"
set "REVIT_ADDIN_DIR=%APPDATA%\Autodesk\Revit\Addins\2027"

echo [1/3] Removing Revit 2027 Add-in files...
if exist "%REVIT_ADDIN_DIR%\RevitBridge.addin" (
    del /F /Q "%REVIT_ADDIN_DIR%\RevitBridge.addin"
    echo [OK] Removed %REVIT_ADDIN_DIR%\RevitBridge.addin
)

if exist "%REVIT_ADDIN_DIR%\RevitBridge.dll" (
    del /F /Q "%REVIT_ADDIN_DIR%\RevitBridge.dll"
    echo [OK] Removed %REVIT_ADDIN_DIR%\RevitBridge.dll
)

if exist "%REVIT_ADDIN_DIR%\RevitBridge.deps.json" (
    del /F /Q "%REVIT_ADDIN_DIR%\RevitBridge.deps.json"
)

echo.
echo [2/3] Removing Python Virtual Environment...
if exist "%ROOT_DIR%src\mcp-server\.venv" (
    rmdir /S /Q "%ROOT_DIR%src\mcp-server\.venv"
    echo [OK] Removed virtual environment folder at src\mcp-server\.venv
)

echo.
echo [3/3] Cleaning up C# build directories...
if exist "%ROOT_DIR%src\revit-bridge\bin" (
    rmdir /S /Q "%ROOT_DIR%src\revit-bridge\bin"
    echo [OK] Cleaned src\revit-bridge\bin
)

if exist "%ROOT_DIR%src\revit-bridge\obj" (
    rmdir /S /Q "%ROOT_DIR%src\revit-bridge\obj"
    echo [OK] Cleaned src\revit-bridge\obj
)

echo.
echo ===================================================
echo CLEAN UNINSTALLATION SUCCESSFUL.
echo ===================================================
echo.
pause
