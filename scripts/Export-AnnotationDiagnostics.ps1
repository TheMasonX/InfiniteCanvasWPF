[CmdletBinding()]
param(
    [string[]]$LogPath,
    [string]$OutputDirectory = "docs/benchmarks/runs/annotation-ab"
)

$ErrorActionPreference = 'Stop'

$culture = [System.Globalization.CultureInfo]::InvariantCulture
$logDirectory = Join-Path $env:LOCALAPPDATA 'InfiniteCanvas\logs'

function Convert-ToInteger([string]$text) {
    return [int64]::Parse(($text -replace ',', ''), $culture)
}

function Convert-ToNumber([string]$text) {
    return [double]::Parse($text, $culture)
}

function Get-AverageValue($items, [string]$propertyName) {
    if ($null -eq $items -or @($items).Count -eq 0) {
        return 0
    }

    return [math]::Round((@($items) | Measure-Object -Property $propertyName -Average).Average, 2)
}

function Get-MaximumValue($items, [string]$propertyName) {
    if ($null -eq $items -or @($items).Count -eq 0) {
        return 0
    }

    return [math]::Round((@($items) | Measure-Object -Property $propertyName -Maximum).Maximum, 2)
}

function Get-SumValue($items, [string]$propertyName) {
    if ($null -eq $items -or @($items).Count -eq 0) {
        return 0
    }

    return (@($items) | Measure-Object -Property $propertyName -Sum).Sum
}

if ($null -eq $LogPath -or $LogPath.Count -eq 0) {
    $todayPath = Join-Path $logDirectory ("infinitecanvas-{0}.log" -f (Get-Date -Format 'yyyyMMdd'))
    if (Test-Path -LiteralPath $todayPath) {
        $LogPath = @($todayPath)
    }
    else {
        $latestLog = Get-ChildItem -Path $logDirectory -Filter 'infinitecanvas-*.log' -File |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($null -eq $latestLog) {
            throw "No InfiniteCanvas log file was found in $logDirectory."
        }

        $LogPath = @($latestLog.FullName)
    }
}

$inputFiles = @(
    foreach ($path in $LogPath) {
        Get-ChildItem -Path $path -File
    }
)

if ($inputFiles.Count -eq 0) {
    throw 'No log files matched the supplied -LogPath values.'
}

$timestampPattern = [regex]::new('^(?<Timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2})')
$modePattern = [regex]::new('Annotation overlay mode:\s+"(?<Mode>[^"]+)"')
$annotationPattern = [regex]::new(
    'AnnotationDiag:\s+mode\s+"(?<Mode>[^"]+)"\s+\|\s+(?<Updates>\d+)u\s+\|\s+avg\s+(?<AverageMs>[\d.]+)ms max\s+(?<MaximumMs>[\d.]+)ms\s+\|\s+fast\s+(?<FastPath>\d+)\s+\|\s+created\s+(?<Created>\d+)\s+pool-hit\s+(?<PoolHit>\d+)\s+return\s+(?<PoolReturn>\d+)\s+drop\s+(?<PoolDrop>\d+)\s+rebuild\s+(?<Rebuild>\d+)\s+recreated\s+(?<Recreated>\d+)\s+\|\s+pool-size\s+(?<PoolSize>\d+)/(?<PoolCapacity>\d+)\s+\|\s+element\s+\+\s*(?<ElementAdds>\d+)/-\s*(?<ElementRemoves>\d+)\s+label\s+\+\s*(?<LabelAdds>\d+)/-\s*(?<LabelRemoves>\d+)')
$framePattern = [regex]::new(
    'FrameDiag:\s+(?<Frames>\d+)f\s+\|\s+avg\s+(?<AverageMs>[\d.]+)ms\s+\|\s+coord\s+(?<CoordActive>[\d,]+)a/\s*(?<CoordQueued>[\d,]+)q/\s*(?<CoordCompleted>[\d,]+)c/\s*(?<CoordCanceled>[\d,]+)x/\s*(?<CoordFailed>[\d,]+)f\s+\|\s+tiles\s+(?<FetchedTiles>[\d,]+)/(?<TotalTiles>[\d,]+) fetched\s+\|\s+avgGen\s+(?<AvgGenMs>[\d.]+)ms\s+\|\s+budget\s+(?<BudgetBytes>[\d,]+)b')

$annotationRows = [System.Collections.Generic.List[object]]::new()
$frameRows = [System.Collections.Generic.List[object]]::new()
$activeModes = @{}
$unparsedDiagnostics = 0

foreach ($inputFile in $inputFiles) {
    $activeMode = 'Unknown'
    foreach ($line in Get-Content -LiteralPath $inputFile.FullName) {
        $timestampMatch = $timestampPattern.Match($line)
        if (-not $timestampMatch.Success) {
            continue
        }

        $timestampText = $timestampMatch.Groups['Timestamp'].Value
        $timestamp = [DateTimeOffset]::Parse($timestampText, $culture)
        $modeMatch = $modePattern.Match($line)
        if ($modeMatch.Success) {
            $activeMode = $modeMatch.Groups['Mode'].Value
            continue
        }

        $annotationMatch = $annotationPattern.Match($line)
        if ($annotationMatch.Success) {
            $activeMode = $annotationMatch.Groups['Mode'].Value
            $annotationRows.Add([pscustomobject][ordered]@{
                Timestamp = $timestamp.ToString('o')
                LogFile = $inputFile.Name
                Mode = $activeMode
                Updates = Convert-ToInteger $annotationMatch.Groups['Updates'].Value
                AverageMs = Convert-ToNumber $annotationMatch.Groups['AverageMs'].Value
                MaximumMs = Convert-ToNumber $annotationMatch.Groups['MaximumMs'].Value
                FastPath = Convert-ToInteger $annotationMatch.Groups['FastPath'].Value
                Created = Convert-ToInteger $annotationMatch.Groups['Created'].Value
                PoolHit = Convert-ToInteger $annotationMatch.Groups['PoolHit'].Value
                PoolReturn = Convert-ToInteger $annotationMatch.Groups['PoolReturn'].Value
                PoolDrop = Convert-ToInteger $annotationMatch.Groups['PoolDrop'].Value
                Rebuild = Convert-ToInteger $annotationMatch.Groups['Rebuild'].Value
                Recreated = Convert-ToInteger $annotationMatch.Groups['Recreated'].Value
                PoolSize = Convert-ToInteger $annotationMatch.Groups['PoolSize'].Value
                PoolCapacity = Convert-ToInteger $annotationMatch.Groups['PoolCapacity'].Value
                ElementAdds = Convert-ToInteger $annotationMatch.Groups['ElementAdds'].Value
                ElementRemoves = Convert-ToInteger $annotationMatch.Groups['ElementRemoves'].Value
                LabelAdds = Convert-ToInteger $annotationMatch.Groups['LabelAdds'].Value
                LabelRemoves = Convert-ToInteger $annotationMatch.Groups['LabelRemoves'].Value
            })
            continue
        }

        $frameMatch = $framePattern.Match($line)
        if ($frameMatch.Success) {
            $frameRows.Add([pscustomobject][ordered]@{
                Timestamp = $timestamp.ToString('o')
                LogFile = $inputFile.Name
                Mode = $activeMode
                Frames = Convert-ToInteger $frameMatch.Groups['Frames'].Value
                AverageMs = Convert-ToNumber $frameMatch.Groups['AverageMs'].Value
                CoordActive = Convert-ToInteger $frameMatch.Groups['CoordActive'].Value
                CoordQueued = Convert-ToInteger $frameMatch.Groups['CoordQueued'].Value
                CoordCompleted = Convert-ToInteger $frameMatch.Groups['CoordCompleted'].Value
                CoordCanceled = Convert-ToInteger $frameMatch.Groups['CoordCanceled'].Value
                CoordFailed = Convert-ToInteger $frameMatch.Groups['CoordFailed'].Value
                FetchedTiles = Convert-ToInteger $frameMatch.Groups['FetchedTiles'].Value
                TotalTiles = Convert-ToInteger $frameMatch.Groups['TotalTiles'].Value
                AvgGenMs = Convert-ToNumber $frameMatch.Groups['AvgGenMs'].Value
                BudgetBytes = Convert-ToInteger $frameMatch.Groups['BudgetBytes'].Value
            })
            continue
        }

        if ($line.Contains('AnnotationDiag:') -or $line.Contains('FrameDiag:')) {
            $unparsedDiagnostics++
        }
    }
}

if ($annotationRows.Count -eq 0) {
    throw 'No mode-aware AnnotationDiag records were found in the supplied logs.'
}

$runDirectory = Join-Path $OutputDirectory (Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null

$annotationRows | Export-Csv -Path (Join-Path $runDirectory 'annotation-diagnostics.csv') -NoTypeInformation -Encoding utf8
$frameRows | Export-Csv -Path (Join-Path $runDirectory 'frame-diagnostics.csv') -NoTypeInformation -Encoding utf8

$summaryRows = foreach ($group in ($annotationRows | Group-Object Mode)) {
    $modeRows = @($group.Group)
    $modeFrameRows = @($frameRows | Where-Object { $_.Mode -eq $group.Name })
    [pscustomobject][ordered]@{
        Mode = $group.Name
        AnnotationSamples = $modeRows.Count
        AnnotationUpdates = Get-SumValue $modeRows 'Updates'
        MeanAnnotationMs = Get-AverageValue $modeRows 'AverageMs'
        MaximumAnnotationMs = Get-MaximumValue $modeRows 'MaximumMs'
        FastPath = Get-SumValue $modeRows 'FastPath'
        Created = Get-SumValue $modeRows 'Created'
        PoolHit = Get-SumValue $modeRows 'PoolHit'
        Rebuild = Get-SumValue $modeRows 'Rebuild'
        Recreated = Get-SumValue $modeRows 'Recreated'
        ElementAdds = Get-SumValue $modeRows 'ElementAdds'
        ElementRemoves = Get-SumValue $modeRows 'ElementRemoves'
        FrameSamples = $modeFrameRows.Count
        MeanFrameMs = Get-AverageValue $modeFrameRows 'AverageMs'
        MaximumFrameMs = Get-MaximumValue $modeFrameRows 'AverageMs'
    }
}

$summaryRows | Export-Csv -Path (Join-Path $runDirectory 'annotation-ab-summary.csv') -NoTypeInformation -Encoding utf8

[pscustomobject][ordered]@{
    ExportedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    InputLogs = ($inputFiles.FullName -join ';')
    AnnotationSamples = $annotationRows.Count
    FrameSamples = $frameRows.Count
    UnparsedDiagnostics = $unparsedDiagnostics
} | ConvertTo-Json | Set-Content -Path (Join-Path $runDirectory 'export-metadata.json')

Write-Output "Exported $($annotationRows.Count) AnnotationDiag rows and $($frameRows.Count) FrameDiag rows to $runDirectory"
Write-Output "Summary: $(Join-Path $runDirectory 'annotation-ab-summary.csv')"
