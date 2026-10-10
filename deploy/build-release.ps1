# بناء حزمة إصدار EWMS لخادم Linux (x64): الخادم مستقل بذاته (لا يحتاج تثبيت .NET) + الواجهة في wwwroot.
# الاستعمال (PowerShell على جهاز التطوير):
#   powershell -ExecutionPolicy Bypass -File F:\EWMSSS\deploy\build-release.ps1
# الناتج: F:\EWMSSS\release\EWMS-<الإصدار>-linux-x64.tar.gz — بلا أي أسرار (الإعدادات من deploy\appsettings.release.json).
param(
    [string]$UiPath = 'F:\-EWMS-UI-main-main',
    [string]$OutDir = (Join-Path $PSScriptRoot '..\release')
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
[xml]$csproj = Get-Content (Join-Path $root 'API\API.csproj')
$version = ($csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw 'Version not found in API\API.csproj' }
$name = "EWMS-$version-linux-x64"
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
$stage = Join-Path $OutDir $name
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }

Write-Host "== 1/5 UI build ($UiPath)"
Push-Location $UiPath
try { npx ng build --configuration production; if ($LASTEXITCODE -ne 0) { throw 'ng build failed' } } finally { Pop-Location }

Write-Host '== 2/5 API publish (self-contained linux-x64)'
dotnet publish (Join-Path $root 'API\API.csproj') -c Release -r linux-x64 --self-contained true -o (Join-Path $stage 'app') -nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Write-Host '== 3/5 UI -> wwwroot'
$wwwroot = Join-Path $stage 'app\wwwroot'
New-Item -ItemType Directory -Force $wwwroot | Out-Null
Copy-Item (Join-Path $UiPath 'dist\EWMS.Client\browser\*') $wwwroot -Recurse -Force
if (-not (Test-Path (Join-Path $wwwroot 'index.html'))) { throw 'index.html missing in wwwroot' }

Write-Host '== 4/5 settings without secrets + deploy files'
# إعدادات التطوير وأسرارها لا تدخل الحزمة أبداً
Remove-Item (Join-Path $stage 'app\appsettings.Development.json') -ErrorAction SilentlyContinue
Copy-Item (Join-Path $PSScriptRoot 'appsettings.release.json') (Join-Path $stage 'app\appsettings.json') -Force
Copy-Item (Join-Path $PSScriptRoot 'linux') (Join-Path $stage 'deploy') -Recurse -Force
Copy-Item (Join-Path $root 'docs\DEPLOYMENT.md') $stage -Force
Set-Content -Path (Join-Path $stage 'VERSION') -Value $version -Encoding ascii

Write-Host '== 5/5 archive'
$archive = Join-Path $OutDir "$name.tar.gz"
if (Test-Path $archive) { Remove-Item $archive -Force }
tar -czf $archive -C $OutDir $name
if ($LASTEXITCODE -ne 0) { throw 'tar failed' }
$hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLower()
Set-Content -Path "$archive.sha256" -Value "$hash  $name.tar.gz" -Encoding ascii
Write-Host "Done: $archive"
Write-Host "SHA256: $hash"
