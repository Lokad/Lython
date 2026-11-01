# Bulk native CSV records: rejected (B74)

2026-11-01, source baseline2b8e1cfa; core source9003d045 and ordinary adapter
d703767c/PythonRe0.4.0. **The whole prototype is removed.** The fixed comparison
passes its validity controls but misses two acceptance gates. No production gain,
retiming or updated CPython ratio is claimed.

## Design and semantic boundary

B73 records CsvDictReader under83/194 positional CSV chains, including32 parser
descendants. These overlapping counts are not removable CPU fractions. Source
shows native equal-width rows still entering generic header cursors, repeated
field-name/length helpers, per-key ambient scopes and incremental table growth.

The28-line prototype checks for an exact PyList of PyString keys with the same
width as the native row, then uses existing funded PyDictStorage.Create(width),
one ambient scope and direct traversal. All eligibility work is nonallocating
and observes cancellation/refusal. Every other case retains existing semantics;
there is no retry or general fallback after denied construction. Duplicate names
preserve insertion order/last value but preallocated unused capacity stays funded.
No new cache, owner subsystem, cap, dialect, call engine or public API is added.

## One frozen comparison

Quiet dedicated Azure VM, actual.NET11RC3, normal GC/tiering. Original/candidate/
identical-original Lython images share CoreLib and key hashes; all auxiliary bits
match.256 interleaved warmups/image and2s settling/case, seven rotating32-job
batches. Whole unchanged scripts include fixture reset, mediated I/O, all stdout/
file goldens, result disposal and balance checks; startup/compile are excluded.

Before code, acceptance required at least7.5% elapsed gain for BOTH CSV scripts
against BOTH originals, text regression/original drift at most3%, IQR at most10%
and every batch longer than100ms. The service limit is60s; actual runtime43.646s.

| Complete job | Change vs original | vs identical original | Fixed gate |
| --- | ---: | ---: | --- |
| CSV plain | -8.32% | -8.33% | Pass |
| CSV helpers | -6.11% | -7.71% | Fails7.5% target against first original |
| Text helper control | +4.16% | +1.28% | Fails3% regression bound against first original |

Positive changes mean longer elapsed time. All controls are valid: original drift
0.005–2.839%, IQR0.62–2.37%, minimum batch202.56ms. Both quiet gates, complete
goldens, per-job baselines, final budgets0 and owned fixture cleanup pass. Service
exits0, MainPID0, empty group, lease free. Coexisting images share process GC/JIT;
these observations do not attribute the text difference to a specific mechanism.
No favorable control selection, gate change, rescue adjustment or retiming follows.

## Validation and removal

Fresh nonincremental Release candidate solution build has zero warnings/errors.
337 white-box and1,352 public focused cases pass on each actual.NET11RC3/.NET10.0.8.
They cover CSV values, custom protocols, real suspension, cancellation, terminal
refusal, ownership and shared-budget reuse. A new independent CPython3.13.2 golden
changes a shared duplicate string header to a custom hash key, then back to string
names while retaining earlier rows. Values, key identity/order and line_num stay
correct. The case is explicitly present in both final public TRX files.

Initial focused337/1351 checks predate that new case. Its edit overlaps the first
multi-target build; subsequent incremental canonical checks show39 cases on.NET11
and38 on.NET10. TRX inspection proves the new case is absent from.NET10. That proof
is rejected before benchmarking. Fresh nonincremental solution rebuild and focused
checks supersede those results; original observations/correction are preserved.

The prototype is removed completely, leaving only the independent public golden.
All production files match HEAD. Another fresh nonincremental solution build has
zero warnings/errors and the same337/1352 checks pass on BOTH actual runtimes,
including the new named transition case. No layout-only tests or obsolete helpers
remain. Unchanged full production qualification atd703767c/41343479748 remains
cached; it is not requeried or repeated for this rejected experiment.

[Summary/check hashes](summary.json), [exact candidate](prototype.patch.gz), source
variants, frozen plans and original driver are preserved. [Raw results](raw-results.tar.gz),
SHA256`1405f8a26f73b0b414697b8c5bce35fd685b16f8a43e2e3ee0536b2b7e89eb58`.
This historical experiment does not describe future HEAD performance.
