@echo off
rem Removes the "downloaded from the Internet" mark from a file, which is what
rem makes Windows SmartScreen show "Windows protected your PC" on first run.
rem
rem Usage: drag a file onto this .bat, or:
rem        unblock.bat path\to\site-diag.exe
setlocal
if "%~1"=="" (
    echo.
    echo   Drag the file onto this .bat window, or run:
    echo     unblock.bat path\to\site-diag.exe
    echo.
    pause
    exit /b 1
)
if not exist "%~1" (
    echo [ERROR] File not found: %~1
    pause
    exit /b 1
)
powershell -NoProfile -Command "Unblock-File -LiteralPath '%~1'"
if errorlevel 1 (
    echo [ERROR] Could not unblock the file.
    pause
    exit /b 1
)
echo.
echo   Unblocked: %~1
echo   You can run it now; SmartScreen will not appear again for this file.
echo.
pause
exit /b 0