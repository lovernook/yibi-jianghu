param([int]$MatchCount=5,[switch]$Visible)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskExe=Join-Path $taskRoot 'Builds/Demo/YibiJianghu.exe'
$taskRun=Join-Path $taskRoot ('Docs/Evidence/PVP-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $taskRun | Out-Null
for($match=1;$match -le $MatchCount;$match++){
    $taskDir=Join-Path $taskRun ('match-'+$match)
    New-Item -ItemType Directory -Path $taskDir | Out-Null
    $taskProcesses=@()
    foreach($role in @('host','join')){
        $taskLog=Join-Path $taskDir ($role+'.log')
        $taskArguments=@(('--pvp-'+$role),'--session-dir',('"'+$taskDir+'"'),'-screen-fullscreen','0','-screen-width','960','-screen-height','540','-logFile',('"'+$taskLog+'"'))
        $style=if($Visible){'Normal'}else{'Hidden'}
        $taskProcesses+=Start-Process -FilePath $taskExe -ArgumentList $taskArguments -WindowStyle $style -PassThru
    }
    Write-Output ('Started match '+$match+' processes '+($taskProcesses.Id -join ','))
    $taskUntil=(Get-Date).AddSeconds(135)
    while(($taskProcesses | Where-Object {-not $_.HasExited}).Count -gt 0 -and (Get-Date) -lt $taskUntil){Start-Sleep -Milliseconds 500}
    foreach($p in $taskProcesses){if(-not $p.HasExited){throw ('Timed out; inspect process '+$p.Id)};if($p.ExitCode -ne 0){throw ('Client failed: '+$p.ExitCode+' '+$taskDir)}}
    foreach($role in @('host','join')){$log=Get-Content (Join-Path $taskDir ($role+'.log')) -Raw;if($log -notmatch 'YIBI_PVP_SMOKE PASS'){throw ('Missing PASS '+$role)};if($log -match 'Exception:|Error:'){throw ('Runtime error '+$role)}}
    $taskHost=Get-Content (Join-Path $taskDir 'host-state.json') -Raw
    $taskJoin=Get-Content (Join-Path $taskDir 'join-state.json') -Raw
    if($taskHost -ne $taskJoin){throw ('Authoritative snapshots differ: '+$taskDir)}
    $s=$taskHost|ConvertFrom-Json
    Write-Output ('MATCH '+$match+' PASS match='+$s.matchId+' revision='+$s.revision+' winner='+$s.winner+' processes='+($taskProcesses.Id -join ','))
}
Write-Output ('Evidence: '+$taskRun)
