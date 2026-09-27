param([switch]$Visible)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskExe=Join-Path $taskRoot 'Builds/M1/YibiJianghu.exe'
$taskEvidence=Join-Path $taskRoot 'Docs/Evidence'
if(-not(Test-Path -LiteralPath $taskExe)){throw 'Build M1 first.'}
foreach($taskSize in @(@(1280,720),@(1920,1080),@(1920,810))) {
    $taskLabel="$($taskSize[0])x$($taskSize[1])"
    $taskLog=Join-Path $taskEvidence "M1-player-$taskLabel.log"
    $taskArguments=@('--m1-smoke','--evidence-dir',('"'+$taskEvidence+'"'),'-screen-fullscreen','0',
        '-screen-width',[string]$taskSize[0],'-screen-height',[string]$taskSize[1],'-logFile',('"'+$taskLog+'"'))
    $taskWindowStyle=if($Visible){'Normal'}else{'Hidden'}
    $taskProcess=Start-Process -FilePath $taskExe -ArgumentList $taskArguments -WindowStyle $taskWindowStyle -PassThru
    if(-not $taskProcess.WaitForExit(45000)){throw "Player timeout at $taskLabel; inspect process $($taskProcess.Id) before retrying."}
    $taskText=Get-Content -LiteralPath $taskLog -Raw
    if($taskProcess.ExitCode -ne 0 -or $taskText -notmatch 'YIBI_M1_SMOKE PASS'){throw "Player validation failed at $taskLabel. See $taskLog"}
    if($taskText -notmatch "resolution=$taskLabel"){throw "Actual resolution differs from $taskLabel"}
    if($taskText -match 'Exception:|Error:'){throw "Player logged an error at $taskLabel"}
    Add-Type -AssemblyName System.Drawing
    $taskBitmap=[System.Drawing.Bitmap]::new((Join-Path $taskEvidence "M1-$taskLabel-dianxue.png"))
    try {
        $taskMin=255;$taskMax=0
        for($taskX=20;$taskX -lt $taskBitmap.Width;$taskX+=29){for($taskY=20;$taskY -lt $taskBitmap.Height;$taskY+=29){
            $taskPixel=$taskBitmap.GetPixel($taskX,$taskY)
            $taskMin=[Math]::Min($taskMin,$taskPixel.R);$taskMax=[Math]::Max($taskMax,$taskPixel.R)
        }}
        if($taskMax-$taskMin -lt 30){throw "Screenshot is blank at $taskLabel; logic pass is not visual validation."}
    } finally {$taskBitmap.Dispose()}
    Write-Output "$taskLabel exit=0 PASS"
    Select-String -LiteralPath $taskLog -Pattern 'YIBI_M1_'
}
