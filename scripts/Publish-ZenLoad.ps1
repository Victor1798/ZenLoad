$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$publishDirectory = Join-Path $projectRoot "publish"

Write-Host "Publicando ZenLoad como aplicación autónoma..."
dotnet publish (Join-Path $projectRoot "ZenLoad.csproj") `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $publishDirectory

$iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
if ($null -eq $iscc) {
    Write-Warning "Inno Setup no está instalado. La publicación quedó en: $publishDirectory"
    Write-Host "Instala Inno Setup y compila installer\ZenLoad.iss para crear el instalador."
    exit 0
}

Write-Host "Creando instalador..."
& $iscc.Source (Join-Path $projectRoot "installer\ZenLoad.iss")
Write-Host "Instalador generado en installer\ZenLoad-Setup.exe"
