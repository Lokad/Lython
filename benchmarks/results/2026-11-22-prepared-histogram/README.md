# B1401 Prepared-word histogram loop

The frozen `248266577` and private `430f0b1fa` cores ran on November
22, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and
.NET 11 RC3. The other 31 measured files, including the comparison
worker and optional adapter, match exactly.

The candidate removes the copied short-word packet and lets a
closed histogram segment read the cursor's funded UTF-8 bytes. A
miss publishes the original Python binding before the ordinary
body can invoke a protocol or request storage. Long words retain
cancellable construction. Ordinary iterators use the existing
per-body fused dictionary update after removal of the whole-loop
shortcut. The four scripts have identical inspected instruction
counts and histogram heads. The quote-heavy invoice has no such
head.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Worker spread old / new |
| --- | --- | ---: | ---: | ---: |
| Nonlocal word report | Warm | −10.36 to −4.07% | −10.84 to −4.83% | 2.79 / 4.12% |
| Ordinary word histogram | Warm | +37.30 to +39.69% | +34.22 to +36.67% | 1.24 / 0.49% |
| Dense 4,095-word Unicode line | Warm | −22.17 to −18.28% | −19.24 to −16.06% | 0.32 / 4.66% |
| Quote-heavy CSV policy invoice | Warm | −1.84 to +2.76% | −2.25 to +4.25% | 2.83 / 1.81% |
| Nonlocal word report | Compile to result | −4.46 to −1.40% | −3.18 to −2.03% | 1.88 / 1.30% |

Ranges cover comparisons between the medians of two independent
processes per source. Each process completes seven rotated batches
after at least eight seconds of full-request warmup. Warm elapsed
includes mediated RAM-backed I/O, output checks and prior-result
cleanup. Compile to result adds compilation and compiled-owner
cleanup. Wider CPU also includes resets and worker protocol. Startup
and final teardown have separate boundaries. The four CPython goldens
validate semantics. This run yields no new external timing ratio.

Windows allocation is flat for the nonlocal report, dense line and
invoice. Median managed allocation for the ordinary histogram falls
65,846 bytes. All admitted peaks match and each owner's budget returns
to baseline. The ordinary histogram still uses 34–37% more wider CPU
on the quiet VM. Short desktop timings had high spread, including
adverse dense-line, quote and compile-to-result readings. Preserve
those observations alongside the quiet comparisons.

Windows .NET 11 and Ubuntu .NET 10 each pass 347 focused Release
checks, including word identity, refusal, cancellation and
ownership. Both images verify all four CPython outputs. All 140 timed
batches and both quiet gates pass. Workers join, the guest mount is
removed and the persistent archive verifies under the lease.

Keep `430f0b1fa` private. Restore the existing whole-loop behavior
for ordinary iterators and retain the smaller prepared-word path
only if a revised complete-script comparison preserves the gains
and resolves the ordinary histogram regression. The quote-heavy
control remains inconclusive.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](procedures.tar.xz).
