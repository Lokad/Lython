# Qualified declaration-runtime CPython checkpoint

2026-11-04. Committed source **f6e51d1c**, frozen precommit core **df206f47**
(assembly metadata878894a3); exact source snapshots/hashes are authoritative.
Reference previous external5c827e68/frozen21cddae7. CPython3.13.16, actual.NET11RC3,
quiet AzureF4as_v7/Ubuntu24.04.4, ordinary GC/tiering. Four independent workers/job:
current, identical-source current control, previous external reference and CPython.

Fourteen jobs complete in **385.53s**, below480s. Existing worker/protocol2,
5s warmups/seven rotated150ms batches. Every loaded identity, output/file golden,
quiet gate, process join and owned-fixture cleanup passes. Source is fully qualified
on Windows/Ubuntu actual11/10, original Stress and installed Release consumers in
41919086629. Timing and correctness qualification are separate evidence.
[Identities](identity.json), [metrics](summary.json), [cached delivery](delivery-ci-status.json),
[raw snapshots/workers/source](observations.tgz), SHA256 `672884ab08b0cb656450e416db17dc4279fa574a5d52789a75c2b1a057c2efe8`.

## Fresh compilation plus one complete execution

| Script | Input | Lython | CPython | Lython/CPython | Identical-source control / Lython IQR |
| --- | --- | ---: | ---: | ---: | ---: |
| CSV summary/helpers | 4,096 records / 57,361 B | 5.013 ms | 3.485 ms | 1.44× | 1.06% / 1.22% |
| JSON-lines/helpers | 4,096 records / 110,592 B | 5.256 ms | 4.116 ms | 1.28× | 2.40% / 1.40% |
| Text summary/helpers | 3,072 lines / 35,840 B | 3.123 ms | 1.286 ms | 2.43× | 0.79% / 0.85% |
| JSON-to-Markdown report | 3,072 records / 189,441 B | 4.348 ms | 2.551 ms | 1.70× | 3.29% / 2.91% |
| Config read/edit/write | 1,024 paths / 7,177 B | 0.273 ms | 0.255 ms | 1.07× | 0.13% / 1.51% |
| CSV long fields | 16 records / 524,717 B | 0.406 ms | 2.423 ms | 0.17× | 0.95% / 1.70% |

Ratio=Lython/CPython elapsed; above one means slower. Control=absolute difference
between current-role medians / candidate median; IQR=(Q3−Q1)/median. These are
diagnostic observations, not narrow confidence intervals. Report current/control
IQR2.91%/2.03% and opposite warm/fresh control directions limit small-change claims.
JSON fresh control2.40% also limits interpretation. Long-field CSV does not
represent ordinary CSV. Substantial text/report gaps remain.

## Warm execution and separate compilation

| Script | Lython warm | CPython warm | Ratio | Current control / Lython IQR |
| --- | ---: | ---: | ---: | ---: |
| CSV summary/helpers | 4.874 ms | 3.339 ms | 1.46× | 2.23% / 0.56% |
| JSON-lines/helpers | 5.013 ms | 4.051 ms | 1.24× | 0.78% / 0.77% |
| Text summary/helpers | 2.898 ms | 1.176 ms | 2.46× | 2.33% / 0.40% |
| JSON-to-Markdown report | 4.367 ms | 2.367 ms | 1.84× | 2.93% / 3.20% |
| Config read/edit/write | 0.191 ms | 0.186 ms | 1.03× | 0.15% / 0.83% |
| CSV long fields | 0.253 ms | 2.368 ms | 0.11× | 0.40% / 1.73% |

Separate compile-only: CSV **80.49µs/54.86µs,1.47×**, report **55.34µs/37.70µs,1.47×**.
Lython IQR2.42%/2.53%, controls2.27%/2.25%; execution/I/O excluded. Never estimate
compile cost by subtracting independently measured warm/fresh medians.

## Previous external source and uncertainty

Compare current/control against5c827e68 within this collection; no additive gains:

| Script | Warm change | Fresh change |
| --- | ---: | ---: |
| CSV summary/helpers | -1.7..+0.5% | -2.5..-1.5% |
| JSON-lines/helpers | -2.5..-1.8% | -0.0..+2.4% |
| Text summary/helpers | -0.9..+1.4% | +0.4..+1.2% |
| JSON-to-Markdown report | -2.9..-0.0% | -6.0..-2.9% |
| Config read/edit/write | -1.3..-1.2% | -1.0..-0.9% |
| CSV long fields | +0.5..+0.9% | -1.5..-0.6% |

Typical results are near unchanged/control-limited; the report fresh direction is
favorable but variable and its warm result straddles the reference. No general
CPU gain is established. Original source-pair regressions remain preserved, not
erased or offset by adding independent percentages.

## Boundary and collection correction

Both clocks include invocation setup, actual mediated RAM-file I/O, full stdout/
file checks and prior-result cleanup. Fresh Lython also compiles/disposes per run.
Fixture staging/reset, process startup, transport/hash and final cached-owner/
process teardown are excluded; all users are subsequently joined. Fresh globals,
persistent CPython module caches and isolated Lython execution caches are explicit.
Startup, real suspended I/O, RSS/physical heap and embedding latency are unmeasured.

The reused collector's shared lease path was corrected before launch from the
second parent to root.parent/comparison-vm.lock. No worker, protocol or timing
change; no original evidence overwritten. Application executables/deps/runtimeconfig
are physically copied, and actual loaded core identities verified. Persistent
evidence is separate from RAM fixtures. No failed measurement or retiming here.

[Previous external checkpoint](../2026-11-04-csv-runtime-cpython-checkpoint/README.md)
and [declaration source pair](../2026-11-04-prepared-declarations/README.md) retain
their own controls and debt. Publication/tagging remain postponed.
