$ErrorActionPreference = "Stop"
$projectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputDir = Join-Path (Split-Path -Parent $projectDir) "build"
$compiler = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path -LiteralPath $compiler)) {
    throw "The built-in .NET Framework compiler was not found."
}

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$sources = Get-ChildItem -LiteralPath $projectDir -Filter *.cs | ForEach-Object FullName
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ `
    /win32manifest:"$projectDir\app.manifest" `
    /win32icon:"$projectDir\Assets\digital-ruble.ico" `
    /out:"$outputDir\RKD.Project.exe" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll `
    /reference:System.Core.dll /reference:System.dll $sources
if ($LASTEXITCODE -ne 0) { throw "Build failed." }
Write-Host "Build complete: $outputDir\RKD.Project.exe"
