param(
  [Parameter(Mandatory=$true)][string]$SourceDoc,
  [string]$OutputPdf = (Join-Path $PSScriptRoot '..\app\assets\forms\noa-r3-template.pdf')
)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $SourceDoc)) { throw "Master DOC not found: $SourceDoc" }
$OutputPdf = [IO.Path]::GetFullPath($OutputPdf)
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $OutputPdf) | Out-Null

$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
$word.AutomationSecurity = 3  # Disable all macros in the legacy DOC.
try {
  $document = $word.Documents.Open($SourceDoc, $false, $true, $false)
  try {
    function Remove-CellSample($cell, [string]$needle) {
      $range = $cell.Range
      $index = $range.Text.IndexOf($needle, [StringComparison]::Ordinal)
      if ($index -lt 0) { throw "Unexpected master layout: missing sample '$needle'" }
      # Duplicate the cell's own story range: Document.Range would target the BODY
      # rather than the header when erasing the month in its header table.
      $target = $range.Duplicate
      $target.SetRange($range.Start + $index, $range.Start + $index + $needle.Length)
      $target.Text = ''
    }
    $header = $document.Sections.Item(1).Headers.Item(1).Range.Tables.Item(1)
    $monthCell = $null
    foreach ($cell in $header.Range.Cells) {
      if ($cell.Range.Text.Contains('October 2026')) { $monthCell = $cell; break }
    }
    if ($null -eq $monthCell) { throw 'Unexpected master layout: month cell missing' }
    Remove-CellSample $monthCell 'October 2026'

    $table = $document.Tables.Item(1)
    Remove-CellSample $table.Range.Cells.Item(28) 'New LAN and Access Point installation at mezzanine floor capacitor'
    Remove-CellSample $table.Range.Cells.Item(29) 'Excess wire, pipe, plactic, and dust.'
    # The Word master has the final period in a separate run.
    Remove-CellSample $table.Range.Cells.Item(29) '.'
    Remove-CellSample $table.Range.Cells.Item(30) 'PTIDS__26100005'
    Remove-CellSample $table.Range.Cells.Item(38) 'New LAN and Access Point installation at mezzanine floor capacitor'
    Remove-CellSample $table.Range.Cells.Item(39) 'Additional WiFi device to cover 2nd floor area'
    Remove-CellSample $table.Range.Cells.Item(40) 'Capacitor Building 2'
    $whenCell = $table.Range.Cells.Item(41)
    $text = $whenCell.Range.Text
    $start = $text.IndexOf('23 Okt 2026', [StringComparison]::Ordinal)
    if ($start -lt 0) { throw 'Unexpected master layout: When sample missing' }
    Remove-CellSample $whenCell $text.Substring($start, $text.Length - $start - 2)
    Remove-CellSample $table.Range.Cells.Item(42) 'PT DUTA COMPUTER'
    if ($document.ComputeStatistics(2) -ne 1) { throw 'Expected the original one-page NOA layout' }
    $document.ExportAsFixedFormat($OutputPdf, 17)
  } finally { $document.Close(0) }
} finally { $word.Quit() }
Write-Output "Prepared NOA PDF template: $OutputPdf"
