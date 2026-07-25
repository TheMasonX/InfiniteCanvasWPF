[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [string[]]$Path
)

$ErrorActionPreference = 'Stop'
$validStatuses = @('Proposed', 'To Do', 'In Progress', 'In Review', 'Done', 'Archived', 'Blocked', 'Reverted')
$validTypes = @('Task', 'Story', 'Bug', 'Spike', 'Improvement', 'Docs', 'Epic')
$validPriorities = @('P0', 'P1', 'P2', 'P3')
$requiredFields = @('id', 'key', 'title', 'status', 'type', 'priority', 'tags')

function Get-TaskFiles {
  param([string[]]$Entries)

  $files = New-Object System.Collections.Generic.List[string]
  $skipNames = @('README.md', 'TASK_SCHEMA.md', 'active-tasks.md', 'JIRA.md', 'agent-to-human-requests.md', 'human-requests.md')

  foreach ($entry in $Entries) {
    if (-not (Test-Path -LiteralPath $entry)) {
      throw "Path not found: $entry"
    }

    if (Test-Path -LiteralPath $entry -PathType Container) {
      foreach ($file in Get-ChildItem -LiteralPath $entry -Filter '*.md' -Recurse) {
        $relativePath = [System.IO.Path]::GetRelativePath((Resolve-Path -LiteralPath $entry).Path, $file.FullName)
        if ($skipNames -contains $file.Name) {
          continue
        }

        if ($relativePath -notmatch '(^|[\\/])(tickets|templates)[\\/]') {
          if ($file.DirectoryName -notmatch '[\\/]docs[\\/]tasks[\\/]templates$') {
            continue
          }
        }

        $files.Add($file.FullName)
      }
    }
    else {
      $files.Add((Resolve-Path -LiteralPath $entry).Path)
    }
  }

  return $files | Sort-Object -Unique
}

function Get-FrontMatterMap {
  param([string]$Content)

  $lines = $Content -split "`r?`n"
  if ($lines.Count -lt 3 -or $lines[0].Trim() -ne '---') {
    return $null
  }

  $map = @{}
  $inFrontMatter = $true
  for ($i = 1; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    if ($line.Trim() -eq '---') {
      break
    }

    if ([string]::IsNullOrWhiteSpace($line)) {
      continue
    }

    if ($line -match '^(?<key>[A-Za-z0-9_-]+):\s*(?<value>.*)$') {
      $key = $Matches.key
      $value = $Matches.value.Trim()
      if ($value -eq '') {
        $map[$key] = @()
      }
      else {
        $map[$key] = $value
      }
    }
  }

  return $map
}

$taskFiles = Get-TaskFiles -Entries $Path
$issues = New-Object System.Collections.Generic.List[string]
$validatedCount = 0
$skippedCount = 0

foreach ($file in $taskFiles) {
  $content = Get-Content -LiteralPath $file -Raw
  if ($content -notmatch '^(?:---\r?\n)') {
    $skippedCount++
    continue
  }

  $frontMatter = Get-FrontMatterMap -Content $content

  if ($null -eq $frontMatter) {
    $skippedCount++
    continue
  }

  if (-not $frontMatter.ContainsKey('id')) {
    $skippedCount++
    continue
  }

  $validatedCount++

  foreach ($field in $requiredFields) {
    if (-not $frontMatter.ContainsKey($field)) {
      $issues.Add($file + ': missing required field ''' + $field + '''')
    }
  }

  if ($frontMatter.ContainsKey('status') -and $frontMatter['status'] -notin $validStatuses) {
    $issues.Add($file + ': invalid status ''' + $frontMatter['status'] + '''')
  }

  if ($frontMatter.ContainsKey('type') -and $frontMatter['type'] -notin $validTypes) {
    $issues.Add($file + ': invalid type ''' + $frontMatter['type'] + '''')
  }

  if ($frontMatter.ContainsKey('priority') -and $frontMatter['priority'] -notin $validPriorities) {
    $issues.Add($file + ': invalid priority ''' + $frontMatter['priority'] + '''')
  }
}

if ($issues.Count -gt 0) {
  Write-Host "Task validation failed."
  foreach ($issue in $issues) {
    Write-Host " - $issue"
  }
  exit 1
}

Write-Host "Validated $validatedCount task file(s); skipped $skippedCount legacy markdown file(s)."
