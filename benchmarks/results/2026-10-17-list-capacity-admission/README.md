# List admission and native CSV excess-row conversion — 2026-10-17 UTC

| Native conversion, 128 pre-read rows | Earlier Lython allocation | Candidate allocation |
| --- | ---: | ---: |
| Nine excess fields per row | 74,752 bytes | 61,440 bytes |
| No excess fields, control | 30,720 bytes | 30,720 bytes |

This short diagnostic establishes an allocation reduction of **13,312 bytes
per 128 excess-row conversions**. It measures two frozen runtime files on
97273b98, with exact hashes and the [source patch](runtime.patch) in
[diagnostic.json](diagnostic.json). It is not a public execution, whole-script
or CPython comparison. The maintained CSV/CPython result remains
[the separate complete-job checkpoint](../2026-10-17-csv-checkpoint/README.md).

The admission regression is independent of these allocation totals. Earlier
list construction/growth allocated approximately 66 KB before a shared allowance
refused an 8,192-slot backing array. The local-limit controls refused earlier.
Four new tests fail on the original source: those two shared cases and two
replacement-overlap cases. They pass with the candidate, preserving old values,
first refusal origin, live aliases, committed funding and cleanup to zero.
[Baseline](baseline-overlap/tests.trx) and [candidate](candidate-list/tests.trx)
retain the exact process receipts and assertions. This bounds the defect to the
tested allocation boundary; it is not a general accounting audit.

The implementation reserves full replacement capacity before assigning the
backing capacity. The old backing stays charged through the copy. Existing
temporary-reservation cleanup returns overlap or failed funding; the final
capacity charge remains unchanged. It removes the check-only preflight,
post-allocation reservation and their helpers. Conservative earlier denial is
intentional. No budget API, tracing, GC, cache or ownership layer was added.

`csv.DictReader` now creates a known-capacity governed list for excess cells
and fills it directly from the parsed row. Its temporary `object[]` and the
subsequent array-to-list copy disappear. Cell order, key-validation order and
existing owners remain. Quoted fields, duplicate headers, rest-key collisions,
Unicode, retained list mutation and cleanup have independent
[CPython controls](cpython-quoted-controls.json); both Lython execution APIs
exercise those public regression cases.

The native recipe replays one already-read row through the existing dictionary
reader, with one declared field and either zero or nine excess fields. Each
sample converts 128 rows under a finite shared scope, checks the retained cells,
and returns the allowance to zero after cleanup. It uses four discarded warmups
and seven samples per case. Context/input setup, parsing, compilation, public
worker startup/projection and cleanup are outside the timer/allocation counter.
Normal GC/tiering remain enabled; no collection is forced. The
[temporary probe source](CsvOverflowAllocationProbe.cs.txt) is archived and
removed from the test project before delivery.

Raw captures: [earlier](baseline-diagnostic/tests.trx),
[candidate](candidate-diagnostic/tests.trx),
[extracted samples](diagnostic-samples.json). Native medians were
0.1523 → 0.1311 ms per 128 excess conversions, with 7.45%/7.74% IQR/median;
the control measured 0.0290 → 0.0272 ms. These microsecond local timings are
unqualified and establish no elapsed benefit, confidence interval, embedding
latency or RSS result. No second capture, long lane or VM campaign was run.

Machine: active Windows 11 Business workstation, build 26200, x64,
Intel Core i7-14700KF, 20 cores / 28 logical processors. Release SDK
10.0.300-preview.0.26177.108, .NET runtime 10.0.13. CPython 3.13.2 supplies
semantic controls only and is not timed. Owned diagnostic/test processes use
the existing 28-second limit plus one-second cleanup, under 30 seconds total;
all linked processes terminated within their bound.

A separate pre-existing parser bug surfaced during preparation:
`QUOTE_NONNUMERIC` readers leave unquoted numbers as strings. Five new public
goldens fail against the original Lython source and match CPython. The
[failing receipt](baseline-csv/tests.trx),
[original test source](initial-CsvCompatibilityTests.cs.txt) and
[CPython results](unquoted-numeric-cpython-baseline.json) preserve that evidence.
Its numeric-conversion correction is scheduled separately; this delivery's
public cases use ordinary input and correctly quoted `QUOTE_NONNUMERIC` fields.
