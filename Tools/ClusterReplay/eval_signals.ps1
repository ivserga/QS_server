$path = "D:\VsProjects\QScalpWPF_Api_Feed\Datasets\clusters.NVDA..20260603.020017.json"
$doc = Get-Content $path -Raw | ConvertFrom-Json
$bars = @()
foreach ($c in $doc.clusters) {
  $bars += [pscustomobject]@{
    i = $c.index
    t = [datetime]::Parse($c.start_time)
    o = $c.open_price_int
    h = $c.max_price_int
    l = $c.min_price_int
    cl = $c.close_price_int
  }
}

function Fwd($idx, $n) {
  $j = $idx + $n
  if ($j -ge $bars.Count) { return $null }
  return $bars[$j].cl - $bars[$idx].cl
}

$byTime = @{}
for ($i = 0; $i -lt $bars.Count; $i++) {
  $byTime[$bars[$i].t.ToString("yyyy-MM-dd HH:mm:ss")] = $i
}

$signals = @(
  @("BreakoutDown","2026-06-02 17:15:30","down"),
  @("BreakoutDown","2026-06-02 17:17:30","down"),
  @("BreakoutDown","2026-06-02 17:20:00","down"),
  @("BreakoutDown","2026-06-02 17:23:00","down"),
  @("BreakoutDown","2026-06-02 17:25:00","down"),
  @("BreakoutDown","2026-06-02 17:32:00","down"),
  @("BreakoutDown","2026-06-02 17:33:30","down"),
  @("BreakoutDown","2026-06-02 17:35:00","down"),
  @("BreakoutDown","2026-06-02 17:38:00","down"),
  @("BreakoutDown","2026-06-02 17:51:30","down"),
  @("BreakoutUp","2026-06-02 17:56:00","up"),
  @("BreakoutUp","2026-06-02 17:59:30","up"),
  @("Distribution","2026-06-02 17:20:30","down"),
  @("Distribution","2026-06-02 17:28:00","down"),
  @("Distribution","2026-06-02 17:43:30","down"),
  @("Distribution","2026-06-02 17:46:00","down"),
  @("Accumulation","2026-06-02 17:55:30","up"),
  @("Accumulation","2026-06-02 17:59:30","up"),
  @("BalanceAfterClimax","2026-06-02 17:09:00","neutral"),
  @("BalanceAfterClimax","2026-06-02 17:25:30","neutral"),
  @("VReversalDown","2026-06-02 17:33:30","down"),
  @("VReversalUp","2026-06-02 17:55:30","up"),
  @("DoubleBottom","2026-06-02 17:56:00","up"),
  @("HvnRejectUp","2026-06-02 17:26:30","up"),
  @("Legacy Resist","2026-06-02 17:13:00","down"),
  @("Legacy Resist","2026-06-02 17:30:00","down"),
  @("Legacy Resist","2026-06-02 17:40:30","down"),
  @("Legacy Resist","2026-06-02 17:42:00","down"),
  @("Legacy Resist","2026-06-02 17:54:00","down"),
  @("Legacy Resist","2026-06-02 18:00:00","down"),
  @("Legacy Support","2026-06-02 17:58:30","up"),
  @("Legacy AbsBuy","2026-06-02 17:35:30","up"),
  @("Legacy AbsBuy","2026-06-02 17:36:30","up"),
  @("Legacy AbsBuy","2026-06-02 17:39:00","up"),
  @("Legacy AbsBuy","2026-06-02 17:52:30","up"),
  @("Legacy AbsSell","2026-06-02 17:57:30","down")
)

$TH = 15
Write-Host "Session: $($bars[0].o/100) -> $($bars[-1].cl/100) ($($bars[-1].cl - $bars[0].o) ticks)"
Write-Host ""

$falseHits = @()
foreach ($s in $signals) {
  $name, $ts, $exp = $s
  $i = $byTime[$ts]
  $ref = $bars[$i].cl
  $f2 = Fwd $i 2; $f5 = Fwd $i 5; $f10 = Fwd $i 10
  $maxUp = 0; $maxDn = 0; $end = 0
  for ($j = $i + 1; $j -lt $bars.Count; $j++) {
    $up = $bars[$j].h - $ref
    $dn = $bars[$j].l - $ref
    if ($up -gt $maxUp) { $maxUp = $up }
    if ($dn -lt $maxDn) { $maxDn = $dn }
    $end = $bars[$j].cl - $ref
  }

  $v = "weak"
  if ($exp -eq "down") {
    if ($maxUp -ge $TH -and (($null -ne $f5 -and $f5 -gt 8) -or $end -gt 15)) { $v = "FALSE" }
    elseif ($maxDn -le -12) { $v = "OK" }
  }
  elseif ($exp -eq "up") {
    if ($maxDn -le -$TH -and (($null -ne $f5 -and $f5 -lt -8))) { $v = "FALSE" }
    elseif ($end -ge 8 -or $maxUp -ge 12) { $v = "OK" }
  }
  else {
    if ([math]::Abs($end) -gt 25 -or ([math]::Abs($f10) -gt 25 -and $null -ne $f10)) { $v = "FALSE" }
    elseif ([math]::Abs($end) -lt 12) { $v = "OK" }
  }

  $line = "$v  $name  $ts  close=$([math]::Round($ref/100,2)) f5=$f5 end=$end maxUp=$maxUp maxDn=$maxDn"
  Write-Host $line
  if ($v -eq "FALSE") { $falseHits += $line }
}

Write-Host "`n=== FALSE ($($falseHits.Count)) ==="
$falseHits | ForEach-Object { Write-Host $_ }
