param([ValidateRange(1024,65535)][int]$Port=7777)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskDotnet=Join-Path $taskRoot '.tools/dotnet/dotnet.exe'
$taskServer=Join-Path $taskRoot 'Server/GameServer/GameServer.csproj'
if(-not (Test-Path -LiteralPath $taskDotnet)){$taskDotnet=(Get-Command dotnet -ErrorAction Stop).Source}
$env:DOTNET_CLI_HOME=Join-Path $taskRoot '.tools/dotnet-home'
Write-Host "一笔江湖本地测试服务，端口 $Port。保持此窗口打开；Ctrl+C 停止。"
& $taskDotnet run --project $taskServer --configuration Release -- $Port
exit $LASTEXITCODE
