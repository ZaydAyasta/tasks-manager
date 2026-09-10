[CmdletBinding()]
param(
    [ValidateRange(32, 4096)]
    [int]$Bytes = 48
)

$buffer = New-Object byte[] $Bytes
[System.Security.Cryptography.RandomNumberGenerator]::Fill($buffer)
$signingKey = [Convert]::ToBase64String($buffer)

Write-Output "JWT signing key ($Bytes random bytes; store it in your secret manager):"
Write-Output $signingKey
