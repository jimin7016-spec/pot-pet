@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
title 화분 펫

where node >nul 2>nul
if errorlevel 1 (
  echo [!] Node.js 가 설치되어 있지 않아요.
  echo     https://nodejs.org 에서 LTS 버전을 설치한 뒤 다시 실행해 주세요.
  echo     ^(Node.js 없이 쓰려면 PotPet-portable.zip 을 받으세요^)
  echo.
  pause
  exit /b 1
)

if exist "node_modules\electron\dist\electron.exe" goto run

echo 처음 한 번만 필요한 설치를 하고 있어요. 1~2분 걸려요...
echo.
call npm install --no-audit --no-fund --ignore-scripts=false
if exist "node_modules\electron\dist\electron.exe" goto run

rem npm 은 끝났는데 Electron 본체(exe)가 없다 = 내려받기가 막힌 경우가 대부분이에요.
echo.
echo [!] Electron 본체를 내려받지 못했어요. 다른 주소(미러)로 다시 시도할게요...
set ELECTRON_MIRROR=https://npmmirror.com/mirrors/electron/
if exist "node_modules\electron\install.js" (
  node node_modules\electron\install.js
) else (
  call npm install --no-audit --no-fund --ignore-scripts=false
)
if exist "node_modules\electron\dist\electron.exe" goto run

echo.
echo [!] 그래도 Electron 을 찾지 못했어요. 회사 네트워크(프록시/보안 프로그램)가 막고 있을 수 있어요.
echo     - 인터넷이 되는 곳에서 다시 실행해 보거나
echo     - 폴더 안의 node_modules 를 지우고 다시 실행하거나
echo     - Electron 이 설치된 PC 에서 make-portable.bat 으로 만든 PotPet-portable.zip 을 받아 쓰세요.
echo.
pause
exit /b 1

:run
rem 콘솔 창은 닫고 화분만 띄워요
start "" "node_modules\electron\dist\electron.exe" .
exit /b 0
