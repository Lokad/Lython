# Report CPython checkpoint — B121

2026-11-02, measured c3fa26cc, CPython3.13.16, dedicated quiet F4as_v7
Ubuntu24.04/.NET11RC3. Three complete checked jobs using the existing workers:
four-second timed warmup per engine, nine interleaved approximately200ms batches,
identical Lython control. This refreshes the materially affected report only;
unaffected CSV/text and historical workload rows retain their named revisions.

| Boundary, same3,072-record /189,441B input | Lython | CPython | Lython / CPython | Lython / CPython IQR | Identical Lython control |
| --- | ---: | ---: | ---: | ---: | ---: |
| Fresh compilation + complete execution |9.819ms|2.989ms|3.28x|4.74% /1.44%|−0.73%|
| Warm complete execution |9.508ms|2.869ms|3.31x|5.98% /1.92%|+3.60%|
| Compilation only |57.52us|37.70us|1.53x|0.92% /0.30%|−0.12%|

Ratio above one means Lython is slower. IQR is interquartile range/median. These
are diagnostic, machine-specific observations; warm variability and its3.6%
control drift remain visible. No retiming is necessary to label the uncertainty.
Independent medians and earlier CPython clocks do not establish a source speedup.
Use [B120's controlled source pair](../2026-11-02-shared-formatted-output/README.md)
for the separate allocation/CPU/elapsed improvement decision. The remaining
roughly3.3x complete-report gap is substantial; the performance objective is open.

Both engines run identical source/fixtures and independent full stdout/file
goldens. Timing includes staging/reset, actual mediated I/O, full output/file
checks and previous-result cleanup. Fresh includes per-invocation compilation and
disposal; warm excludes compilation. Last result/final cached script/process/
fixture cleanup is outside batches and joined. Compilation-only performs no guest
execution; separate verification executes full semantics first. Its output/file
hashes are correctly null under the existing protocol. No startup measurement or
addition/subtraction of independent phase medians.

Actual SDK/runtime11.0.100-rc.3.26522.148 /11.0.0-rc.3.26522.148, workstation GC
and normal tiering. Azure Standard_F4as_v7,4 AMD EPYC9V45 vCPUs/16GiB/no swap,
Ubuntu24.04.4 x86-64. CPython3.13.16 GCC13.3 PGO/LTO, ordinary GIL/GC, no debug/JIT,
isolated -I -S -X utf8. Complete provenance/quiet gates and exact loaded hashes
are in the raw evidence. Source changes stayed out of the core benchmark framework.

All three jobs, before/after quiet gates, identities, full goldens and joined
cleanup pass. Service57.260s, exit0/inactive, shared VM lease free. No failed or
adverse observations removed. The prior B120 supervisor's wrong lane is preserved
in that separate source-pair evidence; this collector uses actual compile-run,
warm and compile names and compile-only null hashes from the outset.

Authoritative table: [docs/BENCHMARKS.md](../../../docs/BENCHMARKS.md).
[Assessment](assessment.json), [identities](identity.json), compressed catalog,
collector/analyzer/preparation sources and [all raw observations](evidence.tar.gz)
preserve exact conditions. Decompress the catalog and raw confirmation folder to
rerun the existing analyzer. Focused semantic/cancellation/ownership checks cover
the measured implementation; full delivery41501504578 is separate and active at
this receipt. The earlier casing proof41497475403 is cached green for93ee4513.
Publication remains postponed.
