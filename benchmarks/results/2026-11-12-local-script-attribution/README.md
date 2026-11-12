# Local whole-script sampled-time diagnostic — R286

2026-11-12; frozen qualified9bca1337/e4cc65b2 production, unchanged at source
commitda61f00f. Windows, actual.NET11RC3, ordinary workstation GC/tiering. Existing
seven-script catalog/fixture source; three complete synchronous captured-text,
CSV-validation and UTF8-report scripts selected. Reuses the existing comparison
file host and private complete-job driver, without production or benchmark API
changes. Each has five-second warmup followed by eight-second sampled collection.

**These are sampled .NET thread wall-time diagnostics, not CPU samples or a
comparison.** Keep all worker/runtime thread stacks. Derived attribution selects
intervals with the actual executable prefix, preserving other threads separately.
Disk-host guest intervals total6468/4850/3061ms; unmanaged-marker leaves total
4978/2828/2159ms. File acquisition is prominent in their nearest managed parents.
This local disk/Windows boundary does not explain the published quiet-VM RAM-I/O
gap or justify prioritizing host-open changes.

One bounded correction uses the existing in-memory BenchmarkHost for the same
captured-text script, inputs, stdout/file digests and balance checks. It completes
2768 warmups and6388 measured-owner iterations; no timing comparison is inferred.
Selected executable intervals total7700ms, of which7395ms have MapCaseRune as the
nearest Lython parent. CPU_TIME/UNMANAGED_CODE_TIME are sampler markers. This
surprising concentration and possible sampling/rundown/safepoint bias are unresolved;
do not promote that parent distribution to CPU dominance, forecast a speedup or
transfer it to Linux. The in-memory host also has different write/storage costs.
Original disk captures remain visible; the correction does not replace them.

All four captures complete goldens, command/compiled-owner zero balances, profiler
exit and joined worker cleanup; no owned filesystem fixture remains. Initial build
fails on an unused host helper, then the installed tracer rejects cpu-sampling for
Windows collect before sampling. Both failures survive; the first worker still
joins and closes cleanly. The installed tool explicitly offers
dotnet-sampled-thread-time for this boundary. Its conversion and raw nettrace
and Speedscope outputs are retained, with the exact private driver/parser/scripts.

No VM collection, new CPython ratio, CPU improvement or repeated platform/package
qualification. Earlier R267/R272 quiet-VM CPU profiles remain source-labelled
evidence. No further unchanged local sampling is selected automatically. A future
CPU attribution must use an appropriate boundary/tool and preserve these adverse
conditions. Current source inspection identifies a separate testable allocation
target: ordinary two-item tuple return storage in the CSV helper; tuple unpacking
already borrows immutable backing and is not duplicate copying to remove again.
R287 is an independent source prototype, not a speedup implied by these samples.

[All raw traces, original failures and scripts](diagnostics.tar.xz),
[disk attribution](attribution.json), [memory correction](memory-attribution.json).
Publication remains postponed.
