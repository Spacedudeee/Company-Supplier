param(
    [string]$Config = 'Debug',
    # '' / 'stable' = Stable-Build (CompanySupplier). 'beta' = Beta-Variante (CompanySupplierBeta).
    [string]$ModVariant = ''
)
$ErrorActionPreference = 'Stop'

# Pfad relativ zum Skript -> portabel (kein hartkodierter Maschinenpfad).
$proj = Join-Path $PSScriptRoot 'src\CompanySupplier\CompanySupplier.csproj'

# COI_ROOT muss auf die Captain-of-Industry-Installation zeigen. Bevorzugt per Umgebungsvariable
# setzen; der Fallback unten ist nur ein Standard-Pfad und sollte ggf. angepasst werden.
if (-not $env:COI_ROOT) { $env:COI_ROOT = 'D:\Steam\steamapps\common\Captain of Industry' }

Write-Host "==> dotnet build ($Config)  ModVariant=$ModVariant  COI_ROOT=$env:COI_ROOT" -ForegroundColor Cyan
dotnet build $proj -c $Config -v minimal -nologo -p:ModVariant=$ModVariant
exit $LASTEXITCODE
