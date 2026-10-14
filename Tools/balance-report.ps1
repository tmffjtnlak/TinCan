# 밸런스 보고서: PlayLogs/*.jsonl + tree.json → PlayLogs/report.html (브라우저로 연다)
# 쓰는 법: powershell -File Tools\balance-report.ps1
$root = Split-Path $PSScriptRoot -Parent
$logs = Join-Path $root 'PlayLogs'
$utf8 = New-Object Text.UTF8Encoding $false
if (-not (Test-Path $logs)) { Write-Host '기록이 없습니다 (PlayLogs 폴더 없음). 게임을 한 번 플레이하세요.'; exit 1 }

$sessions = @()
foreach ($f in Get-ChildItem $logs -Filter 'play_*.jsonl' | Sort-Object Name) {
    $lines = [IO.File]::ReadAllLines($f.FullName, $utf8) | Where-Object { $_.Trim() -ne '' }
    if ($lines.Count -lt 2) { continue }   # 시작만 찍고 끝난 기록은 뺀다
    $sessions += '{"name":"' + $f.BaseName + '","lines":[' + ($lines -join ',') + ']}'
}
$treePath = Join-Path $logs 'tree.json'
$tree = if (Test-Path $treePath) { [IO.File]::ReadAllText($treePath, $utf8) } else { '[]' }
$data = '{"made":"' + (Get-Date -Format 'yyyy-MM-dd HH:mm') + '","sessions":[' + ($sessions -join ',') + '],"tree":' + $tree + '}'

$html = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'balance-report.html'), $utf8).Replace('/*DATA*/null', $data)
$out = Join-Path $logs 'report.html'
[IO.File]::WriteAllText($out, $html, $utf8)
Write-Host "만들었습니다: $out  (기록 $($sessions.Count)개)"
