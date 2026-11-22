# B1314 Numeric and simple quoted CSV ranges

Native file input describes simple quoted fields and unquoted numeric
tokens directly in the admitted line buffer. It copies neither into
a compacted input prefix before creating the row. The general CSV
parser still handles doubled quotes, multiline fields and trailing
syntax from the first field that needs it. Earlier ranges stay in the
funded buffer. Number conversion retains field-local errors and runs
before acquiring another line.

## Quiet comparison

Compared `ac2728ce7` with `e75ba4118` on November 22, 2026 UTC on
the quiet Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3.
Each revision used two independently warmed processes. Each process
contributed seven rotated batches. Ranges cover all process-median
pairings. Warm execution includes mediated RAM-backed I/O and
disposal of the previous result. External CPU covers the wider
worker request. Negative changes favor the candidate.

| 4,096-row script | Warm elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Typed ASCII meter report | −9.38 to −6.94% | −10.41 to −6.54% | 1.54 / 1.14% |
| Typed Unicode meter report | −8.78 to −7.79% | −7.35% | 0.34 / 0.74% |
| Quoted method-policy invoice | −6.38 to −5.30% | −5.99 to −5.31% | 0.57 / 0.58% |
| Plain integer invoice control | +0.19 to +2.19% | 0.00 to +1.33% | 1.72 / 0.27% |

Retain the borrowed ranges for the typed and quote-heavy reports.
Keep the adverse plain-invoice change visible. The control still shares
the same parser; its mostly literal bare fields already used ranges.
Five CPython-verified local cases differ in managed allocation by
less than 0.02%, with identical accounted peaks and restored budgets.
The prior [CPython comparison](../2026-11-22-float-meter-cpython/README.md)
measures the reference implementation.

Windows .NET 11 and Ubuntu .NET 10 each pass 118 white-box and 403
public focused Release checks. Mixed-row file tests cover CRLF,
strict syntax, doubled quotes, multiline fields, Unicode numbers,
signed zero, retained rows, actual async input and cleanup. The initial
mixed-row fixture selected an async-only host for both APIs; it now
uses a synchronous host for the sync case. Initial local staging
read an outdated key in the donor manifest and stopped before VM
access. Both full images then passed four CPython goldens before
staging. All 112 batches verified. Workers joined, fixtures were
removed and the persistent evidence verified under the lease in 288
seconds.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Observations](observations.tgz) · [Source and checks](checks-and-procedures.tar.xz) ·
[Reference core](../2026-11-22-direct-float-text/candidate-core.dll.gz).
