param([string]$BuildDirectory='Builds/FrameworkStageA')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskBuild=[IO.Path]::GetFullPath((Join-Path $taskRoot $BuildDirectory))
if(-not $taskBuild.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Build must stay within the workspace.'}
$taskExe=Join-Path $taskBuild 'YibiJianghu.exe'
if(-not(Test-Path -LiteralPath $taskExe -PathType Leaf)){throw 'Build the framework player first.'}
$taskDir=Join-Path $taskRoot ('Docs/Evidence/FrameworkPlayer-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $taskDir | Out-Null
$taskReport=Join-Path $taskDir 'app-flow.json'
$taskArgs=@('--app-smoke-report',('"'+$taskReport+'"'),'-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',('"'+(Join-Path $taskDir 'player.log')+'"'))
$taskProcess=Start-Process -FilePath $taskExe -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
@{utc=(Get-Date).ToUniversalTime().ToString('o');executable=$taskExe;sha256=(Get-FileHash -LiteralPath $taskExe -Algorithm SHA256).Hash;pid=$taskProcess.Id} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskDir 'launch.json') -Encoding utf8
Write-Output ('Evidence '+$taskDir+' process '+$taskProcess.Id)
$taskDeadline=(Get-Date).AddSeconds(265)
while(-not $taskProcess.HasExited -and (Get-Date) -lt $taskDeadline){Start-Sleep -Milliseconds 400}
if(-not $taskProcess.HasExited){throw ('Timed out. Inspect test-owned process '+$taskProcess.Id)}
if(-not(Test-Path -LiteralPath $taskReport)){throw ('Player exited without a report: '+$taskProcess.ExitCode)}
$taskResult=Get-Content -LiteralPath $taskReport -Raw | ConvertFrom-Json
if($taskProcess.ExitCode -ne 0 -or -not $taskResult.finished -or -not $taskResult.passed -or $taskResult.errorLogCount -ne 0){throw ('App flow failed: '+$taskResult.failure+'; exit='+$taskProcess.ExitCode)}
$taskResult | Select-Object passed,finished,acceptedNavigations,requestedEvents,completedEvents,errorLogCount,elapsedSeconds | ConvertTo-Json
Write-Output 'FRAMEWORK NATIVE PLAYER PASS'
