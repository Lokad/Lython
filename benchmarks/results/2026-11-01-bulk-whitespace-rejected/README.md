# Bulk UTF-8 whitespace construction rejected (B33)

Baseline **ae22392d**, candidate that source plus [source.patch](source.patch),
2026-11-01. Dedicated Ubuntu 24.04.4 Azure F4as_v7 / four EPYC 9V45 vCPUs,
pinned .NET 11 RC3, ordinary worker GC/tiering. One fixed before/candidate/
identical-before comparison, **six processes / 47.122 seconds**. Both quiet
gates, complete stdout/file goldens and baseline/zero ownership checks pass.

**Rejected and removed.** The predeclared gate required at least 5% lower
complete-text process CPU **and** allocation against **both** baselines. CPU
improves 6.84% against the first baseline but 4.78% against the final control;
allocation falls only 2.375% against either. Both targets are missed. Runtime
files are restored byte-for-byte, with no retiming or follow-up tuning candidate.

## Fixed complete-job observations

| Workload | Before CPU ms/job | Candidate CPU ms/job | Control CPU ms/job | Change vs before / control |
| --- | ---: | ---: | ---: | ---: |
| Text helpers | 6.6620 | 6.2060 | 6.5177 | -6.84% / -4.78% |
| CSV helper control | 7.7641 | 7.8013 | 7.8800 | +0.48% / -1.00% |

Allocation B/job, before/candidate/control: text
**5,518,672.3 / 5,387,601.2 / 5,518,697.3**; CSV
**9,542,260.4 / 9,542,310.4 / 9,542,804.3**. CSV is essentially unchanged.
Wall ms/job: text **6.7654 / 6.7189 / 6.7095**; CSV
**8.4229 / 8.3228 / 8.4493**. Text's candidate wall point is higher than its
control despite lower process CPU. No elapsed or CPython speedup is qualified.

Text input: 3,072 lines / 35,840 bytes; CSV: 4,096 rows / 57,361 bytes. Existing
complete scripts and CPU/allocation driver, four warmups, seven rounds of 128
public synchronous jobs. Fixture staging/reset, real mediated I/O, full checks
and result disposal are included; compilation/startup are excluded. Process CPU
includes GC/JIT and all threads. Every sample exceeds 100 ms; baseline CPU drift
is 2.17% text / 1.49% CSV. Each process has a 30-second bound, aggregate process
budget 90 seconds. The owned service is inactive and VM lease available afterward.

## Hypothesis, implementation and tradeoffs

Saved B29 caller chains contain 88/1,059 split-inclusive samples, including 38
monitor and 27 split self samples. These include necessary work and may omit
inlined frames; they are not a promised removable fraction. Source establishes
that word storage creates a temporary reservation per word; Commit itself only
moves local balances, while Reserve reaches the shared allowance.

The prototype consolidates duplicate unlimited/maxsplit loops into one native
UTF-8 word-boundary scanner, shared by sizing and construction. Bounded managed
bulk scans handle ordinary printable ASCII and space/tab runs; existing Unicode
whitespace/rune logic handles other bytes. Cancellation is checked between bounded
chunks and in Unicode runs, including after actual suspended count protocols.
No unsafe code, unbounded atomic scan, new public option or alternate backend.

One operation reservation admits exact word payload/metadata estimates before
allocation. Existing prefunded string hooks commit each word's ordinary ownership;
the list retains its real capacity owner. No new allocator, scope, per-object
ledger or ownership layer. Tradeoffs include an extra sizing pass and earlier
payload admission/denial. Copies and word objects remain necessary. The observed
allocation reduction is smaller than a broad reading of sampled construction
costs would suggest; these distinct quantities must not be conflated.

## Semantics, ownership and retained regressions

Focused Release checks pass **36 native / 172 public per actual framework**,
including the full Python whitespace inventory, maxsplit/remainder behavior,
cooperative cancellation, refusal, retained aliases and shared-budget cleanup.
The existing scan cancellation fix is preserved when restoring the prototype.

Three independent public boundary cases remain: long words/whitespace, UTF-8
scalars across ordinary bulk boundaries, embedded NUL/DEL, supplementary text,
negative/zero/large counts, exact retained remainders, both APIs and zero funding
after cleanup. Their assertions independently pass CPython 3.13.2 `-I -X utf8`
and the restored runtime. They test Python behavior, not the discarded scanner
layout or admission/checkpoint counts.

[acceptance.json](acceptance.json) preserves exact values and source/archive
hashes. [raw-diagnostics.tar.gz](raw-diagnostics.tar.gz) contains every round,
worker/library identity, catalog, machine gate and process record. The source
patch and readable adaptations of the existing driver preserve reproduction.
No new framework or producer-readiness check accompanies this experiment.

Stop B33. Keep the semantic regressions and evidence; do not retain the scanner,
phase layer, dormant code, tuning switch or a smaller rescue optimization.
