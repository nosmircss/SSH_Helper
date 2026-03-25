param(
    [switch]$Install
)

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$flowCanvasDir = Resolve-Path (Join-Path $scriptDir '..')

Push-Location $flowCanvasDir
try {
    if ($Install) {
        npm install
        npm run test:e2e:install
    }

    npm run test:e2e:parity
}
finally {
    Pop-Location
}
