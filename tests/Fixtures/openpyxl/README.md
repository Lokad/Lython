# Lython openpyxl Fixtures

This directory contains public Excel-authored workbook fixtures for the builtin
`openpyxl` compatibility module.

Layout:

- `cases/<id>/case.json`: fixture manifest and expected observations.
- `cases/<id>/input.xlsx`: the Office-authored workbook.
- `families/<id>.json`: capability-family coverage manifest.

Fixtures must be small, synthetic, and public. Do not commit customer workbooks,
local private files, screenshots, or generated validation artifacts. Reduce
private findings into new public cases before committing them.

Fixture cases should form a capability ladder. Prefer focused workbooks tagged
`focused`, each covering one mundane `openpyxl` expectation, and keep larger
interaction workbooks tagged `composition` as secondary coverage. Fixture
families should have at least one focused case whenever they are not explicitly
empty.

Useful commands:

```powershell
powershell -ExecutionPolicy Bypass -File tools/ValidateOpenPyxlFixtures.ps1
powershell -ExecutionPolicy Bypass -File tools/SanitizeOpenPyxlFixtures.ps1
powershell -ExecutionPolicy Bypass -File tools/NewExcelOpenPyxlFixtures.ps1
```

Excel COM generation and sanitization are test-preparation tools only. They must
not become runtime dependencies of `src/Lokad.Lython`.
