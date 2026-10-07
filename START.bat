@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
title 화분 펫

rem 화분 펫 (C# 버전). Node.js 도 설치도 필요 없어요. Windows 에 기본으로 있는 .NET Framework 로 처음 한 번 빌드해요.
set "EXE=native\dist\PotPet.exe"
if not exist "%EXE%" goto build
rem 코드(.cs)가 exe 보다 새로우면 다시 빌드
powershell -NoProfile -Command "$e = (Get-Item 'native\dist\PotPet.exe').LastWriteTime; if (Get-ChildItem 'native\*.cs' | Where-Object LastWriteTime -gt $e) { exit 1 } else { exit 0 }"
if errorlevel 1 goto build
goto run

:build
echo 화분 펫을 준비하고 있어요. 몇 초 걸려요...
powershell -NoProfile -ExecutionPolicy Bypass -File native\build.ps1
if errorlevel 1 goto fail

:run
rem 콘솔 창은 닫고 화분만 띄워요
start "" "%EXE%"
exit /b 0

:fail
echo.
echo [!] 빌드하지 못했어요. 위 메시지를 확인해 주세요.
pause
exit /b 1
