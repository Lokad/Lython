# Managed native-block API feasibility

R447, 2026-11-16 UTC. Standard managed expression APIs support a borrowed span
and by-reference ref-struct state containing a span. Both execute checked loop
arithmetic correctly without changing input. This resolves API feasibility, not
Lython CPU performance. No runtime source/dependency or public API changes.

Pinned .NET 11 RC3, Windows 10.0.26200 local development machine; diagnostic,
not quiet-VM observations. Twelve new programs per shape/process; corrected fresh
processes reverse order. First construction/Compile costs 19.364 ms span-first /
8.722 ms state-first, including lazy framework/JIT startup. Second-shape first
costs 0.668 / 0.323 ms. Warm medians 0.188–0.211 ms; ranges 0.171–0.506 ms;
about 11.5–11.7 KB allocation per tiny compiled program. First invocation is
separate: warmed median 0.0003 ms, zero measured invocation allocation. These
probes are neither a complete instruction set nor a whole-script speedup forecast.

The original process recorded 16.235 ms first construction/Compile and warm
0.16–0.32 ms, without a separate invocation clock. It remains unchanged. The
correction separates clocks and controls order; it discards no adverse original.
Independent phase observations do not establish compile-to-result latency.

Close the API/cost probe with feasibility established and whole-script merit
unresolved. A complete native-prefix replacement must remove its current dispatch
loop, preserve general no-replay handoff and avoid a third engine. Compilation
scratch/cache ownership, framework cancellation gaps, exact overflow/identity/
fault behavior, host suspension and managed stack remain integration obligations.
No token-bearing cache, raw IL, Unsafe or heap-frame workaround is justified.
New-script compile cost must pay for itself without identical-script caching.
No production prototype is selected from API feasibility alone.

A separate managed allocation check identifies a more contained possibility:
the current iteration result and existing executable scalar slot both have a
16-byte array stride on the tested runtime. Using the slot's unassigned state
for exhaustion could avoid enlarging all results. Canonical observation and copy
rules remain to prove; this is no identity or portability claim. Prioritize that
existing-storage migration ahead of dynamic compilation. No unchanged full
qualification is repeated; publication/tagging remain held.

[Summary](summary.json), [span-first](corrected-span-first.json),
[state-first](corrected-state-first.json), [original](original-observations.json),
[exact source/image/conditions](source-and-conditions.tar.xz).
