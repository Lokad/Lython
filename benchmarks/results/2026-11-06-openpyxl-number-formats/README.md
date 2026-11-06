# Excel numeric format correction — P01

Source base: committed text-operation provenance `1e9f53a9`; candidate is the
number-format classifier change recorded with this evidence, not a timing study.

Replace normalized-string construction, broad substring date tests and elapsed
prefix tests with a direct first-section scan. Ignore color/condition/locale
brackets and literals/escapes; recognize exact elapsed tokens. Reuse classification
for numeric conversion, and forward active cancellation into long scans. Keep
existing date conversion/epoch rules, XML bounds and ownership.

CPython 3.13/openpyxl 3.1.5 independently checks 60 numeric rows and 120 ordinary
or cached-formula cells, including all eight standard colors, negative/zero values,
negative-only color/date sections, metadata and escaped/quoted letters. All remain
numeric with their original type/value. Nine genuine date/time/elapsed formats
remain classified. The reference script and output are preserved; this is no claim
of full openpyxl or complete Excel formatting-engine compatibility.

Focused Release: 169 public / 45 internal checks pass on actual .NET 11 RC3 and
.NET 10.0.8; public checks include both public execution APIs, formula caches,
shared allowance cleanup and existing workbook/date-system coverage. Builds have
zero warnings/errors. Original failed build is preserved with its correction.
Full platform/original Stress/installed-package qualification is batched with R17
at this delivery checkpoint. No performance magnitude is inferred from this fix.
