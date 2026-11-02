# Coherent CPython checkpoint — B125

2026-11-02; source90785737, core137b563ac198d769a13f45b1894b8fb4309a764b6da62b7e94bd14d01a8a58f5.
CPython3.13.16 PGO/LTO/GIL/GC, .NET11RC3/workstation GC/ordinary tiering.
Quiet dedicated Azure F4as_v7 Ubuntu24.04.4, four EPYC9V45 vCPUs/16GiB/no swap.
These are external diagnostic points, not a source-pair improvement claim.

| Complete script | Fresh Lython / CPython ms | Ratio | Warm Lython / CPython ms | Ratio |
| --- | ---: | ---: | ---: | ---: |
| CSV/helpers |7.297 /3.647|2.00×|7.092 /3.468|2.05×|
| JSON-lines/helpers |7.483 /4.443|1.68×|7.426 /4.265|1.74×|
| Text/helpers |5.688 /1.513|3.76×|5.568 /1.365|4.08×|
| Search/early exit |1.997 /0.385|INCONCLUSIVE|4.520 /0.262|INCONCLUSIVE|
| JSON-to-Markdown report |8.893 /2.767|3.21×|8.792 /2.560|3.43×|

Fresh means compilation plus one complete invocation in an already-running host.
Warm reuses compiled code. CSV compile-only83.79/54.46µs=1.54×; report57.23/37.62µs
=1.52×. Startup unmeasured; independent phase medians cannot be subtracted.

Search is unusably unstable, despite passing before/after quiet gates: fresh
Lython IQR270.49%, unchanged control218.05% higher; warm IQR138.01%, control77.36%
higher. Raw arithmetic ratios5.18×/17.28× are retained in assessment.json, but they
are not reliable comparisons. No design is rejected or source regression claimed
from this uncertainty. No confirmation was run to overwrite adverse conditions.

Other fresh control drift: CSV−1.75%, JSON+3.64%, text−4.49%, report−2.56%.
Warm:+1.24%,−1.15%,−1.68%,−2.22%. Text fresh remains variable despite candidate
IQR0.54%; report fresh/warm IQR5.56%/4.14%. CSV/JSON candidate IQR below3%/2%.
Both current Lython controls load identical bits; they are not before/after
revisions. Historical observations remain valid only for their named sources.

Twelve existing checked-worker jobs: four-second conditioning/image, nine
interleaved ~200ms batches, two identical Lython processes plus CPython, ordinary
GC/tiering and no source instrumentation. All full stdout/file goldens, library
hashes, reference executable hash, quiet gates and joined worker/fixture cleanup
pass. Owned service229.568s, exit0/inactive, shared lease free. No new framework.

Included: fixture reset/staging, actual efficient mediated synchronous I/O,
complete output/file checks and previous result disposal in each batch. Fresh
also disposes each per-run compilation. Last result is funded through response;
its disposal and cached-program/process/fixture teardown are outside batches and
joined. Transport/hashing/startup excluded. Genuine async semantics are qualified
separately. Existing ownership and cancellation contracts remain active.

[Current guide](../../../docs/BENCHMARKS.md), exact identities, complete catalog,
compressed observations, collectors, assessments and service receipt preserve the
boundaries and adverse results. Decompress raw observations into the analyzer's
named retrieved directory. This checkpoint does not complete the performance
objective or qualify a particular cause of the remaining gaps.
