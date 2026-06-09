<#
Intent: Generate the Office-authored openpyxl fixture workbooks through local Excel COM automation.
Usage: powershell -ExecutionPolicy Bypass -File tools/NewExcelOpenPyxlFixtures.ps1 [-OutputRoot path] [-Case case-id[,case-id...]] [-SkipSanitize]
#>

param(
    [string] $OutputRoot = "",
    [string[]] $Case = @(
        "excel-basic",
        "excel-dimensions",
        "excel-print-settings",
        "excel-sheet-view",
        "excel-styles",
        "excel-dates",
        "excel-merged-cells",
        "excel-hyperlinks",
        "excel-comments",
        "excel-tables",
        "excel-validations",
        "excel-charts",
        "excel-images",
        "excel-freeze-panes",
        "excel-rich-features"
    ),
    [switch] $SkipSanitize
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "tests/Fixtures/openpyxl"
}

$caseRoot = Join-Path $OutputRoot "cases"
$xlOpenXMLWorkbook = 51
$xlSrcRange = 1
$xlYes = 1
$xlValidateList = 3
$xlValidAlertStop = 1
$xlBetween = 1
$xlCellValue = 1
$xlGreater = 5
$msoFalse = 0
$msoTrue = -1

function Rgb([int] $r, [int] $g, [int] $b) {
    return $r + ($g * 256) + ($b * 65536)
}

function Release-ComObject($value) {
    if ($null -ne $value -and [System.Runtime.InteropServices.Marshal]::IsComObject($value)) {
        [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($value)
    }
}

function Complete-ComCleanup() {
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

function New-Workbook($excel) {
    $workbook = $excel.Workbooks.Add()
    while ($workbook.Worksheets.Count -gt 1) {
        $workbook.Worksheets.Item($workbook.Worksheets.Count).Delete()
    }
    return $workbook
}

function Save-CaseWorkbook($workbook, [string] $caseId) {
    $directory = Join-Path $caseRoot $caseId
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $path = Join-Path $directory "input.xlsx"
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }
    $workbook.SaveAs($path, $xlOpenXMLWorkbook)
}

function New-TinyPng([string] $path) {
    $payload = [Convert]::FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAFgwJ/lwGX4QAAAABJRU5ErkJggg==")
    [System.IO.File]::WriteAllBytes($path, $payload)
}

function New-ExcelBasic($excel) {
    $workbook = New-Workbook $excel
    try {
        $inventory = $workbook.Worksheets.Item(1)
        $inventory.Name = "Inventory"
        $second = $workbook.Worksheets.Add([System.Type]::Missing, $inventory)
        $second.Name = "Second"

        $inventory.Range("A1").Value2 = "sku"
        $inventory.Range("B1").Value2 = "name"
        $inventory.Range("C1").Value2 = "qty"
        $inventory.Range("D1").Value2 = "flag"
        $inventory.Range("E1").Value2 = "double_qty"
        $inventory.Range("A2").Value2 = "A001"
        $inventory.Range("B2").Value2 = " Widget "
        $inventory.Range("C2").Value2 = 5
        $inventory.Range("D2").Value2 = $true
        $inventory.Range("A3").Value2 = "A002"
        $inventory.Range("B3").Value2 = "Zero"
        $inventory.Range("C3").Value2 = 0
        $inventory.Range("D3").Value2 = $false
        $inventory.Range("E2").Formula = "=C2*2"
        $inventory.Range("E3").Formula = "=C3*2"
        $second.Range("B2").Value2 = "extra"
        $second.Activate() | Out-Null
        Save-CaseWorkbook $workbook "excel-basic"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelDimensions($excel) {
    $workbook = New-Workbook $excel
    try {
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "Dimensions"
        $sheet.Range("A2").Value2 = "A001"
        $sheet.Columns.Item("B").ColumnWidth = 18.5
        $sheet.Columns.Item("C").Hidden = $true
        $sheet.Rows.Item(2).RowHeight = 24.3
        $sheet.Rows.Item(3).Hidden = $true
        Save-CaseWorkbook $workbook "excel-dimensions"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelPrintSettings($excel) {
    $workbook = New-Workbook $excel
    try {
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "Print"
        $sheet.Range("A1").Value2 = "sku"
        $sheet.Range("C5").Value2 = "last"
        $sheet.PageSetup.PrintArea = "A1:C5"
        $sheet.PageSetup.PrintTitleRows = "1:2"
        $sheet.PageSetup.PrintTitleColumns = "A:B"
        $sheet.PageSetup.LeftMargin = $excel.InchesToPoints(0.25)
        $sheet.PageSetup.RightMargin = $excel.InchesToPoints(0.35)
        $sheet.PageSetup.TopMargin = $excel.InchesToPoints(0.45)
        $sheet.PageSetup.BottomMargin = $excel.InchesToPoints(0.55)
        $sheet.PageSetup.HeaderMargin = $excel.InchesToPoints(0.15)
        $sheet.PageSetup.FooterMargin = $excel.InchesToPoints(0.2)
        $sheet.PageSetup.Orientation = 2
        $sheet.PageSetup.PaperSize = 9
        Save-CaseWorkbook $workbook "excel-print-settings"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelSheetView($excel) {
    $workbook = New-Workbook $excel
    try {
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "View"
        $sheet.Range("C3").Value2 = "selected"
        $sheet.Activate() | Out-Null
        $sheet.Range("C3").Select() | Out-Null
        $excel.ActiveWindow.DisplayGridlines = $false
        Save-CaseWorkbook $workbook "excel-sheet-view"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelRichFeatures($excel) {
    $workbook = New-Workbook $excel
    try {
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "Rich"
        $sheet.Range("A1").Value2 = "sku"
        $sheet.Range("B1").Value2 = "qty"
        $sheet.Range("A2").Value2 = "A001"
        $sheet.Range("B2").Value2 = 5
        $sheet.Range("C2").Value2 = [datetime]"2024-01-02"
        $sheet.Range("C2").NumberFormat = "yyyy-mm-dd"
        $sheet.Range("D2").Formula = "=B2*2"
        $sheet.Range("A3").Value2 = "link"
        $sheet.Range("A1").Font.Bold = $true
        $sheet.Range("B1").Interior.Color = Rgb 255 255 0
        $sheet.Range("B1").HorizontalAlignment = -4108
        $sheet.Range("B1").Borders.Item(7).LineStyle = 1
        $sheet.Columns.Item("A").ColumnWidth = 18
        $sheet.Rows.Item(1).RowHeight = 24
        $sheet.Range("E1:F1").Merge() | Out-Null
        $sheet.Range("E1").Value2 = "merged"
        $sheet.Hyperlinks.Add($sheet.Range("A3"), "https://www.lokad.com/") | Out-Null
        $sheet.Range("A1").AddComment("reviewed") | Out-Null
        $table = $sheet.ListObjects.Add($xlSrcRange, $sheet.Range("A1:B3"), $null, $xlYes)
        $table.Name = "InventoryTable"
        $table.DisplayName = "InventoryTable"
        $table.TableStyle = "TableStyleMedium2"
        $shape = $sheet.Shapes.AddChart2(201, 51, 260, 40, 300, 190)
        $shape.Chart.SetSourceData($sheet.Range("A1:B3"))
        $sheet.Activate() | Out-Null
        $sheet.Range("B2").Select() | Out-Null
        $excel.ActiveWindow.FreezePanes = $true
        Save-CaseWorkbook $workbook "excel-rich-features"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelStyles($excel) {
    $workbook = New-Workbook $excel
    try {
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "Styles"
        $sheet.Range("A1").Value2 = "sku"
        $sheet.Range("B1").Value2 = "qty"
        $sheet.Range("A1").Font.Bold = $true
        $sheet.Range("B1").Interior.Color = Rgb 255 255 0
        $sheet.Range("B1").HorizontalAlignment = -4108
        $sheet.Range("B1").Borders.Item(7).LineStyle = 1
        Save-CaseWorkbook $workbook "excel-styles"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelDates($excel) {
    $workbook = New-Workbook $excel
    try {
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "Dates"
        $sheet.Range("A1").Value2 = "date"
        $sheet.Range("B1").Value2 = "datetime"
        $sheet.Range("A2").Value2 = [datetime]"2024-01-02"
        $sheet.Range("A2").NumberFormat = "yyyy-mm-dd"
        $sheet.Range("B2").Value2 = [datetime]"2024-01-02T13:45:00"
        $sheet.Range("B2").NumberFormat = "yyyy-mm-dd h:mm:ss"
        Save-CaseWorkbook $workbook "excel-dates"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelMergedCells($excel) {
    $workbook = New-Workbook $excel
    try {
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "Merged"
        $sheet.Range("E1:F1").Merge() | Out-Null
        $sheet.Range("E1").Value2 = "merged"
        Save-CaseWorkbook $workbook "excel-merged-cells"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelHyperlinks($excel) {
    $workbook = New-Workbook $excel
    try {
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "Links"
        $sheet.Range("A1").Value2 = "link"
        $sheet.Hyperlinks.Add($sheet.Range("A1"), "https://www.lokad.com/") | Out-Null
        Save-CaseWorkbook $workbook "excel-hyperlinks"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelComments($excel) {
    $workbook = New-Workbook $excel
    try {
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "Comments"
        $sheet.Range("A1").Value2 = "sku"
        $sheet.Range("A1").AddComment("reviewed") | Out-Null
        Save-CaseWorkbook $workbook "excel-comments"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelTables($excel) {
    $workbook = New-Workbook $excel
    try {
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "Table"
        $sheet.Range("A1").Value2 = "sku"
        $sheet.Range("B1").Value2 = "qty"
        $sheet.Range("A2").Value2 = "A001"
        $sheet.Range("B2").Value2 = 5
        $sheet.Range("A3").Value2 = "A002"
        $sheet.Range("B3").Value2 = 7
        $table = $sheet.ListObjects.Add($xlSrcRange, $sheet.Range("A1:B3"), $null, $xlYes)
        $table.Name = "InventoryTable"
        $table.DisplayName = "InventoryTable"
        $table.TableStyle = "TableStyleMedium2"
        Save-CaseWorkbook $workbook "excel-tables"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelValidations($excel) {
    $workbook = New-Workbook $excel
    try {
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "Validation"
        $sheet.Range("A1").Value2 = "choice"
        $sheet.Range("A2").Value2 = "A"
        $sheet.Range("B1").Value2 = "qty"
        $sheet.Range("B2").Value2 = 5
        $sheet.Range("A2:A4").Validation.Delete()
        $sheet.Range("A2:A4").Validation.Add($xlValidateList, $xlValidAlertStop, $xlBetween, "A,B,C") | Out-Null
        $condition = $sheet.Range("B2:B4").FormatConditions.Add($xlCellValue, $xlGreater, "0")
        $condition.Interior.Color = Rgb 198 239 206
        Save-CaseWorkbook $workbook "excel-validations"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelCharts($excel) {
    $workbook = New-Workbook $excel
    try {
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "Chart"
        $sheet.Range("A1").Value2 = "sku"
        $sheet.Range("B1").Value2 = "qty"
        $sheet.Range("A2").Value2 = "A001"
        $sheet.Range("B2").Value2 = 5
        $sheet.Range("A3").Value2 = "A002"
        $sheet.Range("B3").Value2 = 7
        $shape = $sheet.Shapes.AddChart2(201, 51, 260, 40, 300, 190)
        $shape.Chart.SetSourceData($sheet.Range("A1:B3"))
        Save-CaseWorkbook $workbook "excel-charts"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelImages($excel) {
    $workbook = New-Workbook $excel
    $imagePath = Join-Path ([System.IO.Path]::GetTempPath()) ("lython-openpyxl-fixture-" + [Guid]::NewGuid().ToString("N") + ".png")
    try {
        New-TinyPng $imagePath
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "Image"
        $sheet.Range("A1").Value2 = "image"
        $sheet.Shapes.AddPicture($imagePath, $msoFalse, $msoTrue, 120, 20, 40, 40) | Out-Null
        Save-CaseWorkbook $workbook "excel-images"
    }
    finally {
        if (Test-Path -LiteralPath $imagePath) {
            Remove-Item -LiteralPath $imagePath -Force
        }
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

function New-ExcelFreezePanes($excel) {
    $workbook = New-Workbook $excel
    try {
        $sheet = $workbook.Worksheets.Item(1)
        $sheet.Name = "Freeze"
        $sheet.Range("A1").Value2 = "sku"
        $sheet.Range("B2").Value2 = "selected"
        $sheet.Activate() | Out-Null
        $sheet.Range("B2").Select() | Out-Null
        $excel.ActiveWindow.FreezePanes = $true
        Save-CaseWorkbook $workbook "excel-freeze-panes"
    }
    finally {
        $workbook.Close($false)
        Release-ComObject $workbook
    }
}

$excel = $null
try {
    $excel = New-Object -ComObject Excel.Application
    $excel.DisplayAlerts = $false
    foreach ($caseId in $Case) {
        switch ($caseId) {
            "excel-basic" { New-ExcelBasic $excel; break }
            "excel-dimensions" { New-ExcelDimensions $excel; break }
            "excel-print-settings" { New-ExcelPrintSettings $excel; break }
            "excel-sheet-view" { New-ExcelSheetView $excel; break }
            "excel-styles" { New-ExcelStyles $excel; break }
            "excel-dates" { New-ExcelDates $excel; break }
            "excel-merged-cells" { New-ExcelMergedCells $excel; break }
            "excel-hyperlinks" { New-ExcelHyperlinks $excel; break }
            "excel-comments" { New-ExcelComments $excel; break }
            "excel-tables" { New-ExcelTables $excel; break }
            "excel-validations" { New-ExcelValidations $excel; break }
            "excel-charts" { New-ExcelCharts $excel; break }
            "excel-images" { New-ExcelImages $excel; break }
            "excel-freeze-panes" { New-ExcelFreezePanes $excel; break }
            "excel-rich-features" { New-ExcelRichFeatures $excel; break }
            default { throw "Unknown openpyxl Excel fixture case '$caseId'." }
        }
    }
}
finally {
    try {
        if ($excel -ne $null) { $excel.Quit() }
    }
    finally {
        Release-ComObject $excel
        Complete-ComCleanup
    }
}

if (-not $SkipSanitize) {
    & (Join-Path $PSScriptRoot "SanitizeOpenPyxlFixtures.ps1") -FixtureRoot $OutputRoot
}
