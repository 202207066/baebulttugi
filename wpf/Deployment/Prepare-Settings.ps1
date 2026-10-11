param([Parameter(Mandatory=$true)][string]$SourceDirectory, [Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
# Desktop OAuth identifies the app; account tokens and service-account keys are never embedded.
try {
    $settings = Get-Content -LiteralPath (Join-Path $SourceDirectory 'appsettings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $oauth = Get-Content -LiteralPath (Join-Path $SourceDirectory 'credentials.json') -Raw -Encoding UTF8 | ConvertFrom-Json
} catch { throw 'Missing or invalid deployment JSON configuration. Contents are not logged.' }
if (-not $oauth.installed -or $oauth.private_key -or $oauth.type -eq 'service_account' -or $oauth.web) {
    throw 'Only an installed desktop OAuth client may be packaged. Private keys are forbidden.'
}
if (-not $oauth.installed.client_id -or -not $oauth.installed.client_secret) { throw 'Desktop OAuth configuration is incomplete.' }
if (-not $settings.SpreadsheetId) { throw 'The template spreadsheet configuration is missing.' }
$publicSettings = [ordered]@{ SpreadsheetId = $settings.SpreadsheetId; TemplateSpreadsheetId = $settings.TemplateSpreadsheetId; CredentialsFileName = 'credentials.json' }
$sheetNames = [ordered]@{}
foreach ($name in @('Menu','WeeklyMenu','Recipe','TrackA','TrackB','Diet','Event','Patient','Dashboard','Facility','Cost','MealCount','AllergyPatient','DietSave','Ingredient')) {
    if ($settings.Sheets.$name) { $sheetNames[$name] = [string]$settings.Sheets.$name }
}
$publicSettings['Sheets'] = $sheetNames
$installed = [ordered]@{}
foreach ($name in @('client_id','project_id','auth_uri','token_uri','auth_provider_x509_cert_url','client_secret','redirect_uris')) {
    if ($oauth.installed.$name) { $installed[$name] = $oauth.installed.$name }
}
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$publicSettings | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'appsettings.json') -Encoding UTF8
@{installed=$installed} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'credentials.json') -Encoding UTF8
Write-Output 'Deployment settings prepared without account tokens or API keys.'
