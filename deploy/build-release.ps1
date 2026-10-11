# بناء حزم إصدار EWMS لخادم Linux (x64). الإصدار من <Version> في API\API.csproj.
# الاستعمال (PowerShell على جهاز التطوير):
#   powershell -ExecutionPolicy Bypass -File F:\EWMSSS\deploy\build-release.ps1            ← مجلدان (الافتراضي)
#   powershell -ExecutionPolicy Bypass -File F:\EWMSSS\deploy\build-release.ps1 -Single    ← حزمة واحدة مستقلة
#
# مجلدان (الطريقة المعتمدة على الخادم الحالي: الخادم في مجلد يُشغَّل بـ dotnet API.dll، والواجهة في مجلد يقدّمه Nginx):
#   release\EWMS-<v>-api-linux-x64.tar.gz  الخادم (يحتاج .NET 10 على الخادم) — بلا appsettings.json كي لا تُستبدل
#                                           إعدادات الخادم ومفاتيحه؛ appsettings.template.json مرجع للإعدادات فقط
#   release\EWMS-<v>-web.tar.gz            ملفات الواجهة فقط (تُفك داخل مجلد الواجهة)
# حزمة واحدة (-Single): خادم مستقل بذاته (لا يحتاج .NET) والواجهة في wwwroot وملفات systemd/Nginx — للتثبيت الجديد.
# لا تحتوي أي حزمة أسراراً.
param(
    [string]$UiPath = 'F:\-EWMS-UI-main-main',
    [string]$OutDir = (Join-Path $PSScriptRoot '..\release'),
    [switch]$Single
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
[xml]$csproj = Get-Content (Join-Path $root 'API\API.csproj')
$version = ($csproj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw 'Version not found in API\API.csproj' }
New-Item -ItemType Directory -Force $OutDir | Out-Null
$OutDir = (Resolve-Path $OutDir).Path
# tar ويندوز نفسه: tar الخاص بـ Git (إن سبقه في PATH) يقرأ «F:» كاسم خادم بعيد ويفشل
$tar = Join-Path $env:SystemRoot 'System32\tar.exe'

function New-Stage([string]$name) {
    $dir = Join-Path $OutDir $name
    if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    New-Item -ItemType Directory -Force $dir | Out-Null
    return $dir
}

# أرشيف tar.gz + بصمته. contentsOnly: محتوى المجلد مباشرة (يُفك فوق مجلد قائم) بدل المجلد نفسه
function New-Archive([string]$stage, [string]$archiveName, [bool]$contentsOnly) {
    $archive = Join-Path $OutDir $archiveName
    if (Test-Path $archive) { Remove-Item $archive -Force }
    if ($contentsOnly) { & $tar -czf $archive -C $stage . } else { & $tar -czf $archive -C $OutDir (Split-Path $stage -Leaf) }
    if ($LASTEXITCODE -ne 0) { throw 'tar failed' }
    $hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLower()
    Set-Content -Path "$archive.sha256" -Value "$hash  $archiveName" -Encoding ascii
    Write-Host "Done: $archive"
    Write-Host "SHA256: $hash"
}

Write-Host "== UI build ($UiPath)"
Push-Location $UiPath
try { npx ng build --configuration production; if ($LASTEXITCODE -ne 0) { throw 'ng build failed' } } finally { Pop-Location }
$uiDist = Join-Path $UiPath 'dist\EWMS.Client\browser'
if (-not (Test-Path (Join-Path $uiDist 'index.html'))) { throw 'index.html missing in the UI build' }

if (-not $Single) {
    Write-Host '== API publish (framework-dependent linux-x64)'
    $api = New-Stage "EWMS-$version-api-linux-x64"
    dotnet publish (Join-Path $root 'API\API.csproj') -c Release -r linux-x64 --self-contained false -o $api -nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
    # إعدادات الخادم ومفاتيحه تبقى كما هي على الخادم: لا appsettings في الحزمة، والقالب للمرجع فقط
    Remove-Item (Join-Path $api 'appsettings.json'), (Join-Path $api 'appsettings.Development.json') -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $PSScriptRoot 'appsettings.release.json') (Join-Path $api 'appsettings.template.json') -Force
    Set-Content -Path (Join-Path $api 'VERSION') -Value $version -Encoding ascii
    New-Archive $api "EWMS-$version-api-linux-x64.tar.gz" $true

    Write-Host '== UI package'
    $web = New-Stage "EWMS-$version-web"
    Copy-Item (Join-Path $uiDist '*') $web -Recurse -Force
    New-Archive $web "EWMS-$version-web.tar.gz" $true

    Copy-Item (Join-Path $root 'docs\DEPLOYMENT.md') (Join-Path $OutDir 'DEPLOYMENT.md') -Force
    return
}

Write-Host '== API publish (self-contained linux-x64)'
$name = "EWMS-$version-linux-x64"
$stage = New-Stage $name
dotnet publish (Join-Path $root 'API\API.csproj') -c Release -r linux-x64 --self-contained true -o (Join-Path $stage 'app') -nologo
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }

Write-Host '== UI -> wwwroot'
$wwwroot = Join-Path $stage 'app\wwwroot'
New-Item -ItemType Directory -Force $wwwroot | Out-Null
Copy-Item (Join-Path $uiDist '*') $wwwroot -Recurse -Force

Write-Host '== settings without secrets + deploy files'
# إعدادات التطوير وأسرارها لا تدخل الحزمة أبداً
Remove-Item (Join-Path $stage 'app\appsettings.Development.json') -ErrorAction SilentlyContinue
Copy-Item (Join-Path $PSScriptRoot 'appsettings.release.json') (Join-Path $stage 'app\appsettings.json') -Force
Copy-Item (Join-Path $PSScriptRoot 'linux') (Join-Path $stage 'deploy') -Recurse -Force
# ملفات Linux بنهايات LF مهما كانت إعدادات git على جهاز البناء
Get-ChildItem (Join-Path $stage 'deploy') -File | ForEach-Object {
    $text = [IO.File]::ReadAllText($_.FullName) -replace "`r`n", "`n"
    [IO.File]::WriteAllText($_.FullName, $text, (New-Object Text.UTF8Encoding $false))
}
Copy-Item (Join-Path $root 'docs\DEPLOYMENT.md') $stage -Force
Set-Content -Path (Join-Path $stage 'VERSION') -Value $version -Encoding ascii
New-Archive $stage "$name.tar.gz" $false
