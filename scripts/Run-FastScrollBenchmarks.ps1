[CmdletBinding()]
param(
  [string]$OutputRoot = "docs/benchmarks/runs",
  [string]$Filter = "*TileWorkCoordinatorBenchmarks*"
)

$ErrorActionPreference = 'Stop'
$runId = Get-Date -Format 'yyyyMMdd-HHmmss'
$outputDirectory = Join-Path $OutputRoot $runId
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$metadata = [ordered]@{
  TimestampUtc = (Get-Date).ToUniversalTime().ToString('o')
  GitRevision = (git rev-parse HEAD).Trim()
  OperatingSystem = [System.Environment]::OSVersion.VersionString
  Processor = (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name).Trim()
  DotNet = (dotnet --version).Trim()
  Configuration = 'Release'
  Framework = 'net10.0-windows'
  Filter = $Filter
  BenchmarkJob = 'Throughput, warmupCount=3, iterationCount=10'
  SmokeRun = $false
}

$metadata | ConvertTo-Json | Set-Content -Path (Join-Path $outputDirectory 'run-metadata.json')

$arguments = @(
  'run'
  '--configuration', 'Release'
  '--project', 'benchmarks/InfiniteCanvas.Benchmarks'
  '--framework', 'net10.0-windows'
  '--'
  '--filter', $Filter
  '--exporters', 'csv,json,html,github'
)

dotnet @arguments 2>&1 | Tee-Object -FilePath (Join-Path $outputDirectory 'benchmark-output.txt')
if ($LASTEXITCODE -ne 0) {
  throw "Fast-scroll benchmark command failed with exit code $LASTEXITCODE."
}

Write-Output "Benchmark evidence written to $outputDirectory"
