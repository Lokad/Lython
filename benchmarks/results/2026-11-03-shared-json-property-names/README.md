# B138: execution-owned JSON property names, intermediate milestone

2026-11-03. Retained with **open unique-key regression debt**. This removes repeated
immutable name allocations/copies/admission and hash calculation in record workloads.
It is not a general JSON speedup or completion of the 0.11.0 performance objective.
Full platform/stress/package qualification is pending at this commit; focused checks
passed. Publication remains postponed.

One lazy native UTF-8 content table belongs to ExecutionState and the existing
execution/result memory owner. Borrowed input is lookup-only; stored keys own decoded
bytes. Separate loads/decoder calls and reentrant hooks share names, never mutable
records. Existing string grammar, offsets, strictness, Unicode and hooks stay intact.
Escaped duplicates can refund only a fresh unpublished candidate. No global pool,
per-parse table, hidden limit, new owner, fallback or retained cancellation token.

The initial per-entry index charge produced excessive unique-name costs. A bounded
refinement moved admission to capacity growth using existing temporary reservations,
covering old/new table overlap before allocation. It retains only major live capacity
and releases only discarded private index backing; names and aliases remain funded.
Both source snapshots, all adverse data and the pre-code contracts are preserved.

| Complete warm job | Initial elapsed / unchanged control | Refined elapsed / unchanged control | Refined allocation / unchanged control |
| --- | ---: | ---: | ---: |
| Report, first | -7.91% / +1.93% | -10.81% / -1.68% | -13.809% / -0.00075% |
| Report, second | -8.23% / -0.28% | -12.11% / +1.10% | same allocation diagnostic |
| JSON lines, first | -2.15% / -1.09% | -1.74% / +1.02% | -21.469% / -0.00025% |
| JSON lines, second | -1.45% / -0.85% | -2.03% / +0.73% | same allocation diagnostic |
| Config | +0.30% / +0.72% | -0.02% / +0.59% | not measured |
| All-unique names | +36.68% / +3.17% | +31.50% / -2.28% | +27.987% / -0.00071% |
| Text control | +3.15% / +4.21% | -5.28% / -2.23% | +0.00087% / 0% |

Refined report warm weighted process CPU: -12.70/-12.78%, controls -0.93/-0.65%.
This supports lower CPU for that workload, not a universal gain. JSON warm CPU
-0.68/-2.91%, controls +2.04/+1.71%. Unique CPU **+69.67%**, control -1.79%.
Fresh report elapsed -8.17%, control +1.41%; JSON -1.96%, control -0.007%.
Fresh CPU controls shift -12.50/-7.18%; those CPU deltas are inconclusive. Text's
allocation is unchanged; do not infer a text implementation gain from layout/noise.

**Debt decision:** retain the useful repeat-schema milestone on the unreleased
branch while prioritizing the unique-input cost and duplicated key/index construction
as the next bounded design gate. The standalone content index adds storage and probes
for names seen only once. Do not hide that cost, add heuristic cache limits, select
only favorable fixtures or claim the refinement resolved it. A coherent follow-up
must eliminate real duplicated representation/work, or explicitly choose the
measured documented tradeoff or withdrawal. No fixed percentage automatically vetoes
this improvement; no indefinite prototype mode is introduced.

Baseline runtime fd7bc7e4 (HEAD 1e73f72f adds only evidence/tests), core SHA256
547825f9dfe52ddf22e24e76b8bc54ae8975428dae5b5e22052dd223624d2c42.
Initial candidate c8c8b49f8357f00e34fec05394279320d2f0293f474c779a34d932de2a905d17.
Refinement 380b78d49359b22ba73fe337ef400f4d1949485f6ef6e9945bfae49d86a9d108.
Frozen after all local checks joined; auxiliary worker binaries are unchanged.

Quiet Azure Standard_F4as_v7, Ubuntu 24.04.4, .NET 11 RC3. Existing worker/protocol
and canonical complete report/JSON-lines/config/text scripts; added unique-name
control reads 1,024 four-field records, sums all 4,096 values and checks full output.
Its independent isolated CPython 3.13.2 golden agrees. No external CPython speed
ratio is derived here. The separately maintained external table remains fd7bc7e4.

Warm excludes compilation/startup; fresh includes per-invocation compile/dispose.
Both include reset/staging, efficient mediated file I/O, stdout/file validation and
prior-result disposal. Final results, compilation owners, processes and fixtures
join outside the batch timer. Nine interleaved approximately 200ms batches follow
four seconds of warmup per image; baseline/candidate/unchanged-control identities,
all goldens and before/after quiet gates pass. This is diagnostic timing, not the
long sampling qualification campaign. Process CPU comes from Linux /proc TGID ticks,
including runtime/JIT/response, not guest-only CPU. Separate allocation uses existing
192-warmup, nine-by-32 complete-Run diagnostics with shared CoreLib/process hashing;
its elapsed values are not the timing verdict.

The initial wrapper ended with CRLF, so timing completed but allocation did not
launch (service exit2). The original failed condition/journal is retained; only the
missing allocation was run, exit0 in47.261s. Refined timing completed in171.216s;
the immediately following allocation quiet gate correctly rejected14% busy CPU
(exit3) before measuring. That failed gate is preserved. One later allocation-only
launch passed both gates and finished exit0 in46.612s. Timing was not repeated for
either measurement correction. All owned units are inactive and the lease is free.

Focused Release:181 internal/822 public per actual .NET11/10 plus four storage/funding
cases per target (two new admission-refusal cases beyond the181 selection). Coverage
includes genuinely suspended/reentrant hooks, retained aliases, concurrent shared
allowances, scanner cancellation, terminal first refusal, cleanup and sequential
budget reuse. Borrowed-input mutation cannot alter stored names or later lookups;
mutable records remain distinct. Original checks were not relaxed. Full delivery
qualification will be recorded separately without replaying cached fd7bc7e4 CI.
