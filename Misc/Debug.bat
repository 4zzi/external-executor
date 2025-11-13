@echo off
cls

:: check admin
net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

cd /d "I:\Download\visual studio stuff\cs\Labubu\"
dotnet run --configuration Debug
pause