@echo off
setlocal enabledelayedexpansion

echo ===================================================
echo   REVIT 2027 MCP INTEGRATION SYSTEM INSTALLER
echo ===================================================
echo.

set "ROOT_DIR=%~dp0"
set "REVIT_ADDIN_DIR=%APPDATA%\Autodesk\Revit\Addins\2027"

echo [1/4] Building C# Revit 2027 Add-in (.NET 10)...
dotnet build "%ROOT_DIR%src\revit-bridge\RevitBridge.csproj" -c Release
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] C# build failed. Ensure .NET 10 SDK and Revit 2027 API are installed.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [2/4] Deploying Add-in to Revit 2027 directory...
if not exist "%REVIT_ADDIN_DIR%" (
    echo Creating directory: %REVIT_ADDIN_DIR%
    mkdir "%REVIT_ADDIN_DIR%"
)

copy /Y "%ROOT_DIR%src\revit-bridge\bin\Release\net10.0-windows\RevitBridge.dll" "%REVIT_ADDIN_DIR%\"
copy /Y "%ROOT_DIR%src\revit-bridge\RevitBridge.addin" "%REVIT_ADDIN_DIR%\"

if exist "%ROOT_DIR%src\revit-bridge\bin\Release\net10.0-windows\RevitBridge.deps.json" (
    copy /Y "%ROOT_DIR%src\revit-bridge\bin\Release\net10.0-windows\RevitBridge.deps.json" "%REVIT_ADDIN_DIR%\"
)

echo.
echo [3/4] Setting up Python Virtual Environment for MCP Server...
set "VENV_DIR=%ROOT_DIR%src\mcp-server\.venv"

if not exist "%VENV_DIR%\pyvenv.cfg" (
    echo Creating virtual environment at %VENV_DIR%...
    if exist "%VENV_DIR%" rmdir /S /Q "%VENV_DIR%" 2>nul
    python -m venv "%VENV_DIR%"
    if %ERRORLEVEL% NEQ 0 (
        echo [WARNING] Command 'python' failed, trying with 'py -3'...
        py -3 -m venv "%VENV_DIR%"
    )
)

echo Installing dependencies from requirements.txt...
"%VENV_DIR%\Scripts\python.exe" -m pip install --upgrade pip --quiet
"%VENV_DIR%\Scripts\python.exe" -m pip install -r "%ROOT_DIR%src\mcp-server\requirements.txt"

echo.
echo ===================================================
echo [4/4] INSTALLATION SUCCESSFUL!
echo ===================================================
echo Add-in deployed to:
echo   %REVIT_ADDIN_DIR%
echo.
set "PYTHON_EXE=%VENV_DIR%\Scripts\python.exe"
set "SERVER_PY=%ROOT_DIR%src\mcp-server\server.py"

set "PYTHON_EXE_ESC=%PYTHON_EXE:\=\\%"
set "SERVER_PY_ESC=%SERVER_PY:\=\\%"

echo Copy the following JSON snippet to Antigravity IDE MCP settings:
echo ---------------------------------------------------
echo {
echo   "mcpServers": {
echo     "revit-2027": {
echo       "command": "%PYTHON_EXE_ESC%",
echo       "args": [
echo         "%SERVER_PY_ESC%"
echo       ]
echo     }
echo   }
echo }
echo ---------------------------------------------------
echo.
pause
