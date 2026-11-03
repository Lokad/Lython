# Terminal UTF-8 string storage — B174

2026-11-03. **Retain**, with explicit CSV timing and retained-capacity debt.
A terminal byte builder transfers its owned array to immutable `PyString`; a logical
byte length bounds all content operations. Delete the final partial-buffer trimming
copy and duplicate payload admission. Reserve new string metadata before publication;
failed admission leaves the builder intact and funded. Successful transfer detaches
before reuse. Snapshots from reusable builders remain independent copies; byte-array/
`PyBytes` outputs still require exact lengths. No new owner, pool, tracing, backend or
configuration policy. Strings retain the entire capacity and their ordinary caches.

## Observations and decision

Both APIs, complete RAM-file scripts:

| Work | Initial elapsed / unchanged control | Allocation | Accounted execution peak delta |
| --- | --- | --- | ---: |
| Report, sync | −6.62% / −1.72% | −5.68% | +150,059 B |
| Report, async | −4.13% / +1.17% | −5.68% | +150,059 B |
| Text, sync / async | −3.65% / +1.83%; −1.44% / +1.71% | flat | 0 B |
| CSV, sync / async | +0.08% / −3.55%; +2.15% / −1.99% | essentially flat | +10 B |
| JSON, sync / async | +2.62% / +0.52%; −2.30% / −1.68% | essentially flat | +50 B |
| Config, sync / async | +1.15% / −0.48%; +0.55% / +0.44% | flat | 0 B |

Report CPU −6.57%/−3.88%, controls −2.42%/+0.77%, supports a bounded complete-script
improvement. Text timing is diagnostic: its synchronous IQR approaches 4% and its
unchanged controls move. Short/long/Unicode controls reduce allocation 9.8%/22.1%/
19.1%; their elapsed controls vary, including +10.1% and −8.3%, so do not claim those
raw deltas as gains. Retained peaks increase +4,096/+1,548,154/+189,440 B respectively.
No universal CPU improvement or embedding RSS/latency claim.

CSV/JSON alone received one justified confirmation: identical frozen bits with
**SOURCE ROLES SWAPPED**, 512 warmups and nine 128-job samples per image, both APIs.
Here tool before/control are NEW B174 and tool candidate is OLD B171. Normalized
new versus old, CSV async elapsed +1.20/+1.30%, CPU +1.38/+1.41%, with close new
controls. Sync elapsed +3.43/+4.08%, CPU +3.66/+6.79%, with 3.5–4.8% elapsed IQR.
JSON sync new roles disagree (−0.85/+2.30% elapsed); async +0.29/+0.54% with 3.9–4.9%
IQR. JSON remains inconclusive. Preserve every adverse role/sample and original
condition; do not infer the CPU cause from the added logical-length field.

Accept the confirmed roughly 1–4% CSV elapsed cost and larger capacity retention for
the simpler terminal ownership transition and useful report gain. This is explicit
regression debt for the next coherent activation/transport milestone, not a fixed
percentage veto or a promise that every script improves. No third unchanged retime
selected. Prior B171 report debt remains its own historical source-pair observation;
do not add independent percentage deltas to claim it has been qualified away.

Two direct managed allocation-counter samples per isolated image measured **72 B**
for each `PyString` object in both sources on this VM/runtime. Immutable input bytes
were reused, factory delegates prepared/warmed before counters, and all new objects
allocated normally. This validates no object-size growth here, not a universal CLR
layout guarantee. Capacity and CLR allocation are different measurements.

## Conditions and validation

Before: fully qualified **b7a0f40a402ef6d067e2b41e63befe63077dc386**, core
**6d4b0c32961c01a1fe1f975585ceb01b3791ca24f1cd24b5750f60dac01161ed**.
Candidate core **bf2ddeb1d6adb8b4dccda00e0980225502b3a5a92e8aec019ab753ac3a6e69ed**,
frozen after focused checks; exact source/test hashes and snapshot supplied.
Quiet Azure Standard_F4as_v7, Ubuntu 24.04.4, .NET 11 RC3, ordinary GC/tiering.
Three isolated images share CoreLib, process GC and hashing: before, new candidate,
identical-before control. Auxiliary bits identical. Initial 256 warmups, nine rotated
64-job samples, two-second settle/case. Five existing complete CSV/text/JSON/report/
config scripts plus short/long/Unicode construction controls independently checked
under isolated CPython 3.13.2. No external performance ratios derived here.

Clocks include public Run or awaited RunAsync, fixture reset, actual mediated RAM
file I/O, full stdout/file validation and result disposal. Startup/compilation and
final cached program/fixture teardown excluded; those users are joined afterward.
No frontend change, so no compilation retiming. Initial owned service **179.401 s**,
confirmation **138.439 s**, both exit zero. Every golden, image/hash identity, quiet
gate, ownership baseline, fixture cleanup and independently free shared lease passes.
Both raw archives were retrieved and SHA-256 verified locally. At later cleanup,
the original/confirmation RAM roots were already absent; the cause was unmeasured.
Residual profile staging was removed separately after verifying its local archive.
The retained candidate was restored from the verified local frozen image into a
permanent VM comparison directory; its core/auxiliary hashes match.

Focused Release: **143 internal /104 public per actual .NET 11 and .NET 10**, zero
build warnings. Prior broad .NET 11 selection passed 3,761 cases and exposed four
casing-refusal tests dependent on the removed copy peak. Those now explicitly hold
the shared allowance after ready output, leave setup headroom and refuse substantial
output before publication; original failure/terminal-output/cleanup assertions remain.
The storage test now expects full retained capacity. New checks cover invalid unused
tails, ASCII/multibyte/astral content, decoding/hash/order/search/replacement/slices/
rune boundaries, builder reuse, independent snapshots, retained funding, first
refusal before detach, empty cleanup and budget reuse. Existing gated callbacks,
JSON chunks, regex windows, retained results and cancellation controls pass.

Full platform/stress/installed-package qualification is a separate delivery receipt;
focused checks alone are not full qualification. Publication remains postponed.

[All initial tables](comparison-tables.md), [initial assessment](assessment.json),
[initial raw samples](observations.tgz), [confirmation](confirmation/assessment.json),
[confirmation raw samples](confirmation/observations.tgz), [identity](identity.json),
[source snapshot](source-snapshot.tgz), [allocation motivation](../2026-11-03-activation-storage-profile/README.md).

## Initial full qualification and fixture correction

Terminal **41690987281** at exact **7c183083** failed on BOTH platforms: 22 internal
and three public cases, 4,029 internal/15,812 public passing per platform. Later
framework/package/stress steps were skipped; this was not fully qualified.
[Exact receipt](delivery-ci-initial-failure.json), [Ubuntu log](ci-ubuntu.log),
[Windows log](ci-windows.log). Never re-query this terminal run.

The internal failures assumed exact final copies/duplicate overlap or triggered
cancellation at that removed allocation. Metadata refusal/cancellation now targets
actual metadata funding, retained-capacity assertions include unused bytes, and
read refusal still precedes cursor commit. Two cases solely enforcing final-copy
payload overlap are removed; real initial/replacement-overlap refusal and independent
metadata/cancellation/input/cleanup assertions remain. Repeated sticky-refusal probes
are shorter where failure is already latched; no timeout relaxed.

Public callback controls distinguish storage funded BEFORE the callback, which can
transfer afterward without another large allocation, from large output constructed
AFTER the callback, which must still refuse. Both retain the 128 KiB post-gate CLR
allocation bound, terminal origin and zero-baseline cleanup checks. This preserves
real preflight detection while allowing the copy to disappear.

Test-only correction **7e96843b**, focused **96 internal/38 public per actual .NET 11
and .NET 10 PASS**, zero build warnings. Production source hashes still match the
measured snapshot. Rebuilt binaries carry newer revision metadata; the frozen
measurement image and its original hash remain immutable. No runtime change or
source-pair/CPython replay is justified by test-only edits. Corrected full delivery
**41693945867** is FULLY GREEN at exact **7e96843b5875de444ae479d87ab5f1341ddd050e**:
Windows/Ubuntu, actual .NET 11/.NET 10 Release, installed consumers and original
stress scales all pass. [Terminal receipt](delivery-ci-status.json) cached once;
never re-query. Original failed run/conditions remain above. Publication postponed.
