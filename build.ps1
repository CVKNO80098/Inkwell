param(
    [switch]$Test,
    [switch]$App,
    [switch]$RunTests
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = 'D:\VS\MSBuild\Current\Bin\Roslyn\csc.exe'
$refDir = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
$outDir = Join-Path $root 'build'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$refNames = @(
    'mscorlib', 'System', 'System.Core', 'System.Xml', 'System.Xml.Linq', 'System.Data',
    'System.Drawing', 'System.Windows.Forms', 'System.Runtime.Serialization', 'System.Xaml',
    'WindowsBase', 'PresentationCore', 'PresentationFramework', 'System.Configuration',
    'System.Net.Http', 'System.IO.Compression', 'System.IO.Compression.FileSystem', 'System.Numerics'
)
$refs = @()
foreach ($n in $refNames) {
    $p = Join-Path $refDir ($n + '.dll')
    if (Test-Path $p) { $refs += ('/r:' + $p) }
}
if ($refs.Count -eq 0) { throw "no reference assemblies found in $refDir" }

function Invoke-Csc([string[]]$sources, [string]$target, [string]$out, [string[]]$extraRefs) {
    $all = @('/nologo', ("/target:" + $target), '/langversion:latest', '/platform:anycpu',
             '/optimize+', '/deterministic', '/nowarn:1591') + $refs
    if ($extraRefs) { $all += $extraRefs }
    $all += ("/out:" + $out)
    $all += $sources
    Write-Host ("csc: {0} files -> {1}" -f $sources.Count, $out)
    $log = Join-Path $outDir 'csc.log'
    if (Test-Path $log) { Remove-Item $log -Force }
    & $csc @all 2>&1 | Tee-Object -FilePath $log | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE (see $log)" }
}

$doTests = $Test -or $RunTests -or (-not $App)
$doApp = $App -or (-not $Test -and -not $RunTests)

if ($doTests) {
    $sources = @()
    $sources += Get-ChildItem (Join-Path $root 'src\Core') -Filter *.cs | ForEach-Object { $_.FullName }
    $sources += Get-ChildItem (Join-Path $root 'tests') -Filter *.cs | ForEach-Object { $_.FullName }
    Invoke-Csc $sources 'exe' (Join-Path $outDir 'Tests.exe') $null
}

if ($doApp) {
    $sources = @()
    $sources += Get-ChildItem (Join-Path $root 'src') -Filter *.cs -Recurse | ForEach-Object { $_.FullName }
    $appOut = Join-Path $outDir 'Inkwell.exe'
    $icon = Join-Path $root 'assets\Inkwell.ico'
    $appRefs = $null
    if (Test-Path $icon) { $appRefs = @('/win32icon:' + $icon) }
    Invoke-Csc $sources 'winexe' $appOut $appRefs
    Write-Host "app -> $appOut"
}

if ($RunTests -or ($doTests -and -not $App)) {
    & (Join-Path $outDir 'Tests.exe')
    exit $LASTEXITCODE
}
