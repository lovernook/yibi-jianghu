param([switch]$AbortTest,[string]$BuildDirectory='Builds/Demo')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskBuild=[IO.Path]::GetFullPath((Join-Path $taskRoot $BuildDirectory))
if(-not $taskBuild.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Build directory must stay within the workspace.'}
$taskExecutable=Join-Path $taskBuild 'YibiJianghu.exe'
if(-not (Test-Path -LiteralPath $taskExecutable -PathType Leaf)){throw ('Client executable missing: '+$taskExecutable)}
$taskRun=Join-Path $taskRoot ('Docs/Evidence/NetworkJourney-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $taskRun | Out-Null
$taskProcesses=@()
foreach($role in @('host','join')){
    $taskArguments=@('--net-role',$role,'--net-dir',('"'+$taskRun+'"'),'-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-logFile',('"'+(Join-Path $taskRun ($role+'.log'))+'"'))
    if($AbortTest){$taskArguments+='--net-abort'}
    $taskProcesses+=Start-Process -FilePath $taskExecutable -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
}
Write-Output ('Evidence '+$taskRun+' processes '+($taskProcesses.Id -join ','))
if($AbortTest){Write-Output 'Wait for both *-active.txt, then stop only the test-owned server. Clients verify no local winner.'}
$taskUntil=(Get-Date).AddSeconds(490)
while(($taskProcesses | Where-Object {-not $_.HasExited}).Count -gt 0 -and (Get-Date) -lt $taskUntil){Start-Sleep -Milliseconds 500}
foreach($p in $taskProcesses){if(-not $p.HasExited){throw ('Timed out; inspect test-owned process '+$p.Id)};if($p.ExitCode -ne 0){throw ('Client failed '+$p.Id+' exit='+$p.ExitCode)}}
foreach($role in @('host','join')){
    $taskLog=Get-Content (Join-Path $taskRun ($role+'.log')) -Raw
    if($taskLog -notmatch 'YIBI_NETWORK_JOURNEY_PASS'){throw ('Missing PASS '+$role)}
    if($taskLog -match 'Exception:|Error:'){throw ('Runtime error '+$role)}
    Get-Content (Join-Path $taskRun ($role+'-pass.txt'))
}
Write-Output 'NETWORK JOURNEY ALL PASS'
