@echo off
echo OoiMRR - File Resource Manager
echo ==============================
echo.
echo Checking and starting application...
echo.

REM Check if instance is already running
tasklist /FI "IMAGENAME eq OoiMRR.exe" 2>NUL | find /I /N "OoiMRR.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo Application is already running, please close existing instance first.
    echo.
    pause
    exit /b 1
)

REM Try to run compiled program directly
if exist "bin\Debug\net8.0-windows\OoiMRR.exe" (
    echo Found compiled program, starting...
    "bin\Debug\net8.0-windows\OoiMRR.exe"
    echo Application started!
) else (
    echo Compiled program not found, compiling...
    dotnet build
    if %ERRORLEVEL% EQU 0 (
        echo Compilation successful, starting...
        "bin\Debug\net8.0-windows\OoiMRR.exe"
        echo Application started!
    ) else (
        echo Compilation failed, please check error messages.
    )
)

echo.
pause