@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"
title 설치 필요 없는 버전 만들기

rem 이미 설치된 Electron 을 그대로 복사해서, Node.js 없이 PotPet.exe 만 눌러도 실행되는 폴더(+zip)를 만들어요.
rem 동료에게는 이 zip 하나만 전달하면 돼요. (내려받기가 필요 없어요)

if not exist "node_modules\electron\dist\electron.exe" (
  echo [!] 먼저 START.bat 을 한 번 실행해서 화분이 뜨는지 확인해 주세요. ^(Electron 설치가 필요해요^)
  pause
  exit /b 1
)

set "OUT=PotPet-portable"
if exist "%OUT%" rmdir /s /q "%OUT%"

echo Electron 복사 중...
robocopy "node_modules\electron\dist" "%OUT%" /E /NFL /NDL /NJH /NJS /NP >nul
if %errorlevel% geq 8 goto fail

if exist "%OUT%\resources\default_app.asar" del /q "%OUT%\resources\default_app.asar"
ren "%OUT%\electron.exe" PotPet.exe

echo 앱 파일 복사 중...
mkdir "%OUT%\resources\app"
copy /y main.js "%OUT%\resources\app\" >nul
copy /y preload.js "%OUT%\resources\app\" >nul
copy /y preload-note.js "%OUT%\resources\app\" >nul
copy /y preload-toy.js "%OUT%\resources\app\" >nul
robocopy renderer "%OUT%\resources\app\renderer" /E /NFL /NDL /NJH /NJS /NP >nul
if %errorlevel% geq 8 goto fail
robocopy assets "%OUT%\resources\app\assets" /E /NFL /NDL /NJH /NJS /NP >nul
if %errorlevel% geq 8 goto fail
node tools\make-portable-pkg.js "%OUT%\resources\app\package.json"
if errorlevel 1 goto fail
copy /y tools\portable-readme.txt "%OUT%\읽어보세요.txt" >nul

echo zip 으로 묶는 중... ^(1분쯤 걸려요^)
if exist PotPet-portable.zip del /q PotPet-portable.zip
powershell -NoProfile -Command "Compress-Archive -Path '%OUT%' -DestinationPath 'PotPet-portable.zip' -Force"
if errorlevel 1 goto fail

echo.
echo 완료! PotPet-portable.zip 을 동료에게 전달하세요.
echo ^(받은 사람은 압축을 풀고 PotPet.exe 를 실행하면 돼요^)
pause
exit /b 0

:fail
echo.
echo [!] 만드는 중에 문제가 생겼어요. 위 메시지를 확인해 주세요.
pause
exit /b 1
