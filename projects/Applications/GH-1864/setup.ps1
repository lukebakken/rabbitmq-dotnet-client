$DebugPreference = "Continue"
$ErrorActionPreference = 'Stop'
# Set-PSDebug -Strict -Trace 1
Set-PSDebug -Off
Set-StrictMode -Version 'Latest' -ErrorAction 'Stop' -Verbose

New-Variable -Name curdir  -Option Constant -Value $PSScriptRoot
Write-Host "[INFO] curdir: $curdir"

(Get-Content -Raw -LiteralPath rabbitmq.conf.in).Replace('@@PWD@@', $curdir).Replace('\', '/') | Set-Content -LiteralPath rabbitmq.conf
