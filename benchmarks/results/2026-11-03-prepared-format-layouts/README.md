# Compiler-prepared constant format layouts — B160

2026-11-03. Retained in **873ae625**. Constant f-string specifications reuse an
immutable parsed layout stored in the existing executable operation. Native
per-field grammar scans, width/precision substring creation and descriptor
allocation disappear. There is one parser and one formatter. Dynamic specs and
other formatting APIs keep their actual runtime parse; custom/date formatters
receive the raw specification. Invalid native grammar does not become an early
compiler error when a custom formatter can accept it. Value-dependent validation,
conversion order, asynchronous protocols and error cleanup remain at execution.

The existing compilation owner funds the metadata. There is no lazy cache,
execution token retention, new allowance, backend or context/frame merger.
Broader activation work remains open; this does not complete the performance goal.

## Complete-script comparison

Quiet Azure F4as_v7, Ubuntu24.04.4, .NET11 RC3, CPython3.13.16, ordinary GC/tiering.
Actual file APIs use shared tmpfs. Same protocol2 as B158: fixture reset outside
execution clocks; invocation, guest I/O, complete checks and prior-result cleanup
inside. Compile-run also includes compilation/disposal. Compile-only runs no guest
code or I/O. Startup, transport and final owner/process/fixture teardown excluded;
all users joined. These are RAM-backed results, not an ordinary-filesystem gain.

Six jobs, six-second warmups, nine rotated approximately250ms batches, one-second
settling and reversed setup order for later controls. Identical original binary
controls and same-run CPython report points. Timing takes191.719s; allocation
checks bring the owned service to233.813s. All gates/goldens/joins pass, exit0.

| Job | Earlier ms | New ms | Elapsed Δ / unchanged control | Process CPU Δ / control |
| --- | ---: | ---: | ---: | ---: |
| Report warm |5.918459|5.540508|−6.39 /−0.53%|−6.77 /−2.22%|
| Report compile-run |6.056956|5.757979|−4.94 /+1.07%|−6.15 /+0.14%|
| Report compile-only |0.059338|0.059137|−0.34 /−0.09%|−0.79 /−1.55%|
| Text warm |3.436986|3.356292|−2.35 /+3.20%|−0.92 /+5.11%|
| CSV summary warm |5.136785|5.253020|+2.26 /−0.85%|+3.36 /−1.16%|
| JSON-lines warm |5.527290|5.498153|−0.53 /+0.67%|−0.49 /0.00%|

Report new IQR2.81/3.20%; control3.00/3.32%. Compile-only new/control0.70/1.07%:
no material compilation regression established. Text control drift prevents an
attributed improvement. Initial CSV is adverse with low0.43% new/0.30% control
IQR. It is not discarded or explained away by the report gain. Process CPU
includes reset/runtime/JIT/response, not guest-only CPU.

| Report phase, 3,072 records /189,441B | New Lython ms | CPython ms | Lython / CPython |
| --- | ---: | ---: | ---: |
| Warm |5.540508|2.356168|2.351×|
| Compile-run |5.757979|2.506498|2.297×|
| Compile-only |0.059137|0.037695|1.569×|

Compile-only processes454B of source; it does not read the189,441B fixture.
The external gap remains substantial. These current points complement the older
nine-script ordinary-filesystem table; independent revision medians do not
establish a source speedup.

## Allocation and bounded confirmation

Unchanged complete-job diagnostic:512 interleaved warmups per image,32-job batches,
nine rotated rounds, normal GC/tiering, three isolated images sharing CoreLib,
matching hash witness and auxiliary bits. Complete reset/Run/I/O/checks/result
cleanup included; compilation/startup excluded. Its elapsed stays diagnostic and
does not replace the protocol2 worker timing above. The tool retains its original
B154 purpose label; actual identities/catalogues define these runs.

| Complete job | Allocation Δ | Unchanged control |
| --- | ---: | ---: |
| Report |−10.473%|−0.0003%|
| Large config |−0.005%|−0.005%|
| Small config |0.000%|−0.263%|
| CSV rewrite |+0.020%|0.000%|

Report allocation8,215,065→7,354,668B per invocation. All accounted execution/root
peaks are identical across images: report4,556,536/4,638,968B; large config254,327/
327,575B; small config14,681/87,929B; CSV rewrite5,358,537/5,426,761B. These coarse
admission peaks are not physical RSS. Minor descriptors were covered by existing
headroom; moving them to compilation need not change the charged peak.

One additional66.494s run uses the same images and existing diagnostic on CSV
summary/text/JSON/report, sharing CLR hashing. `PyString.GetHashCode` uses the
process's `HashCode` seed; ordinary worker processes do not share it. Source
inspection finds only a few formatted CSV output rows, so the changed helper
parameter is not established as the cause of its initial regression. No code
change was made on that hypothesis. The confirmation tests a confound, not a
causal explanation.

| Shared-hash diagnostic | Elapsed Δ / control | CPU Δ / control | Allocation Δ / control |
| --- | ---: | ---: | ---: |
| CSV summary |−4.77 /−5.21%|−5.02 /−5.43%|−0.021 /−0.021%|
| Text |−0.50 /−1.67%|−0.48 /−1.36%|0.000 /0.000%|
| JSON-lines |+0.88 /+0.36%|+0.89 /+0.36%|0.000 /0.000%|
| Report |−6.39 /+0.96%|−5.79 /+1.27%|−10.475 /0.000%|

CSV new is about0.46% adverse to the later unchanged image, while originals differ
5.21%. Thus no consistent2–3% CSV regression is established. The initial adverse
condition remains **unresolved**, not disproved or attributed to hashing. Retain
the report/storage simplification with this uncertainty; make no zero-impact
claim. Carry CSV controls into the next coherent runtime comparison, without
another unchanged retiming now. Report improvement repeats across both boundaries.
All confirmations preserve exact goldens, zero balances, image isolation and cleanup.

## Source, semantics and delivery

Baseline **1634d73971284ed6c18871f0571f389e5cabe0b1**, fully qualified B158 runtime,
nonincremental build frozen before mutation. Core SHA256
`eb9c807478e0bb9e84b5f72eab169d6b7444ffcbf97e27069270d4b3a9f410cf`.
Candidate core SHA256
`a890ccf44ea9ee7ccbae436cfc64b39bb6367191f1a9aaf42c8ef5388ac2da79`.
Four production source hashes/patch match retained873ae625; rebuilt commit metadata
can change binary hashes. Unchanged worker, unused Windows apphost and optional
adapter bits are normalized; only core DLL/PDB differ. No discarded variant exists.

Focused Release:826 public/59 internal per actual .NET11/.NET10, zero build
warnings. Independent CPython3.13.2 goldens cover reused specs across strings,
integers, floats, complex/date/custom values; raw malformed/oversized custom specs;
native type errors; single/appended fields and conversions. One compilation runs
repeated sync/delayed async calls with allowance returning to baseline, then zero
on disposal. Existing cancellation, terminal refusal, identity and cleanup controls
pass. Full delivery [41632382342](https://github.com/Lokad/Lython/actions/runs/41632382342)
is COMPLETE/GREEN for exact873ae625: Windows/Ubuntu .NET11/.NET10, original Stress
and installed consumers. All required steps verified in the
[cached receipt](delivery-ci-status.json). Publication remains postponed.
B158's independent full delivery is also GREEN.

Both owned VM units exit0 and are inactive/dead; the shared lease is free. No
background work is abandoned. [Timing/initial allocation observations](initial-observations.tar.gz),
[confirmation](hash-confirmation.tar.gz), [timing assessment](assessment.json),
[allocation assessment](allocation-assessment.json),
[confirmation assessment](hash-confirmation-assessment.json),
[peaks](accounted-peaks.json), [identity](identity.json),
[source patch](source.patch.gz), [focused logs](focused-logs.tar.gz),
[goldens](cpython-goldens.json), [original source gate](source-gate.md).
