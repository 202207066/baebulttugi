param([Parameter(Mandatory=$true)][string]$ScratchDirectory)
$ErrorActionPreference='Stop'
$projectDirectory=Split-Path $PSScriptRoot -Parent
$inputDirectory=Join-Path $ScratchDirectory 'input'
$outputDirectory=Join-Path $ScratchDirectory 'output'
New-Item -ItemType Directory -Force $inputDirectory,$outputDirectory | Out-Null
$names=@('Menu','WeeklyMenu','Recipe','TrackA','TrackB','Diet','Event','Patient','Dashboard','Facility','Cost','MealCount','AllergyPatient','DietSave','Ingredient')
$sheetSettings=@{}
foreach($name in $names){$sheetSettings[$name]='test-'+$name}
$sheetSettings['UnknownSecret']='must-not-package'
@{SpreadsheetId='test-template';TemplateSpreadsheetId='test-source';HolidayApiKey='must-not-package';AccessToken='must-not-package';Sheets=$sheetSettings} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $inputDirectory 'appsettings.json') -Encoding UTF8
@{installed=@{client_id='test-desktop';client_secret='test-app-id';auth_uri='https://accounts.google.com/o/oauth2/auth';token_uri='https://oauth2.googleapis.com/token';redirect_uris=@('http://localhost')};refresh_token='must-not-package'} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $inputDirectory 'credentials.json') -Encoding UTF8
& (Join-Path $projectDirectory 'Deployment\Prepare-Settings.ps1') -SourceDirectory $inputDirectory -OutputDirectory $outputDirectory
$settings=Get-Content (Join-Path $outputDirectory 'appsettings.json') -Raw | ConvertFrom-Json
$oauth=Get-Content (Join-Path $outputDirectory 'credentials.json') -Raw | ConvertFrom-Json
if($settings.HolidayApiKey -or $settings.AccessToken -or $settings.Sheets.UnknownSecret -or $oauth.refresh_token){throw 'Secret filtering failed'}
if($settings.SpreadsheetId -ne 'test-template' -or $settings.TemplateSpreadsheetId -ne 'test-source' -or $oauth.installed.client_id -ne 'test-desktop'){throw 'Required Google configuration lost'}
foreach($name in $names){if($settings.Sheets.$name -ne ('test-'+$name)){throw "Sheet setting lost: $name"}}
$appConfig=Get-Content (Join-Path $projectDirectory 'AppConfig.cs') -Raw
foreach($match in [regex]::Matches($appConfig,'GetString\("Sheets:([^"\r\n]+)"')){if($match.Groups[1].Value -notin $names){throw 'Runtime sheet key missing from whitelist'}}
$tokens=$null;$parseErrors=$null
[Management.Automation.Language.Parser]::ParseFile((Join-Path $projectDirectory 'Deployment\Set-HolidayApiKey.ps1'),[ref]$tokens,[ref]$parseErrors) | Out-Null
if($parseErrors.Count){throw 'Credential setup script syntax is invalid'}
@{type='service_account';private_key='not-a-real-key'} | ConvertTo-Json | Set-Content (Join-Path $inputDirectory 'credentials.json') -Encoding UTF8
$rejected=$false
try { & (Join-Path $projectDirectory 'Deployment\Prepare-Settings.ps1') -SourceDirectory $inputDirectory -OutputDirectory $outputDirectory } catch { $rejected=$true }
if(-not $rejected){throw 'Service account packaging must be rejected'}
Write-Output 'PASS Deployment preserves Google/OAuth and every runtime sheet key; excludes API keys/tokens; rejects service accounts; credential setup syntax is valid.'
