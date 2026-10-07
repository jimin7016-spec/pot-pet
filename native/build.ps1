# 화분 펫 (C# 버전) 빌드: Windows 에 기본으로 들어 있는 .NET Framework 4 컴파일러만 써요. 설치할 것 없음.
#   powershell -ExecutionPolicy Bypass -File build.ps1          → dist\PotPet.exe
#   powershell -ExecutionPolicy Bypass -File build.ps1 -Test    → 빌드 후 규칙 검사까지
param([switch]$Test)
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (!(Test-Path -LiteralPath $compiler)) { throw 'Windows .NET Framework 4.x 컴파일러(csc.exe)를 찾지 못했어요.' }

$dist = Join-Path $here 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$exe = Join-Path $dist 'PotPet.exe'
$assets = Join-Path $here 'assets'

# 실행 파일 아이콘: icon.png 를 그대로 담은 .ico 를 만들어요 (Vista 이후 PNG 아이콘 지원)
$png = Join-Path (Split-Path $here) 'assets\icon.png'
$ico = Join-Path $dist 'potpet.ico'
Add-Type -AssemblyName System.Drawing
$bytes = [IO.File]::ReadAllBytes($png)
$img = [System.Drawing.Image]::FromFile($png)
$w = [Math]::Min(255, $img.Width); $h = [Math]::Min(255, $img.Height); $img.Dispose()
$ms = New-Object IO.MemoryStream; $bw = New-Object IO.BinaryWriter($ms)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]1)
$bw.Write([Byte]($w % 256)); $bw.Write([Byte]($h % 256)); $bw.Write([Byte]0); $bw.Write([Byte]0)
$bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]$bytes.Length); $bw.Write([UInt32]22)
$bw.Write($bytes); $bw.Flush(); [IO.File]::WriteAllBytes($ico, $ms.ToArray()); $bw.Dispose()

$sources = Get-ChildItem -LiteralPath $here -Filter '*.cs' | ForEach-Object { $_.FullName }
$cscArgs = @(
  '/nologo', '/target:winexe', '/optimize+', '/platform:anycpu', '/codepage:65001', '/utf8output',
  "/win32icon:$ico", "/out:$exe",
  '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Web.Extensions.dll',
  ("/resource:" + (Join-Path $assets 'neodgm.ttf') + ',PotPet.Font'),
  ("/resource:" + (Join-Path (Split-Path $here) 'assets\icon.png') + ',PotPet.Icon'),
  ("/resource:" + (Join-Path (Split-Path $here) 'assets\tray.png') + ',PotPet.Tray')
) + $sources
& $compiler @cscArgs
if ($LASTEXITCODE -ne 0) { throw '빌드 실패' }
Copy-Item -LiteralPath (Join-Path $assets 'FONT-LICENSE.txt') -Destination $dist -Force
Remove-Item -LiteralPath $ico -Force

if ($Test) {
  $p = Start-Process -FilePath $exe -ArgumentList '--self-test' -NoNewWindow -PassThru -Wait -RedirectStandardOutput (Join-Path $dist 'self-test.txt')
  Get-Content -LiteralPath (Join-Path $dist 'self-test.txt') -Encoding UTF8
  if ($p.ExitCode -ne 0) { throw '규칙 검사 실패' }
}
Write-Host "빌드 완료: $exe"
