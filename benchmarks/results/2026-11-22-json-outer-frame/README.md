# B1375 Flat JSON encoder frames

Compared `e152d1ceb` with `927f0ca88` on November 22, 2026 UTC.
The measured patch is retained at `893cbd686`, on top of the
separately tested regex correction. The two frozen 32-file images
differ only in the core DLL and use the same optional 0.4.0 adapter.

The encoder uses its outer traversal frame as the first cycle marker.
Flat records skip the initial frame array and cycle set. Nested values
use one admitted spill array and identity set in the same traversal.
Eager encoding also removes a temporary reservation object and the
reentry guard used by exposed iterators. The larger encoder shell is
admitted before allocation. Exposed chunks keep their independent
storage, and traversal scratch remains charged through suspended
callbacks and until cleanup.

| Complete warm script | Elapsed change | Wider process CPU change | Local CLR allocation change |
| --- | ---: | ---: | ---: |
| JSON Lines export, expanded options | −5.07 to −4.39% | −5.18 to −3.32% | −17.90% |
| JSON Lines export, explicit options | −7.64 to −6.92% | −7.92 to −6.01% | −21.51% |
| Nested configuration manifest | −0.25 to −0.06% | −0.80 to +0.07% | +0.14% |
| Positional calls, unchanged control | −6.40 to +1.07% | −6.03 to −0.02% | 0.00% |

The flat scripts both export 3,072 records from a 189,441-byte
input. Warm elapsed includes mediated RAM-backed I/O, complete
stdout and file checks, and prior-result cleanup. Wider CPU also
includes reset and request handling. Compilation and startup are
outside the clocks. Negative changes favor the candidate. Ranges
span all pairings of two worker medians per image on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The four workers per
script rotate through seven timed batches after eight seconds of
warmup per worker. The unchanged control's reference workers differ
by 7.26%. The affected workers' widest same-image spread is 0.66%.

Locally the two flat exports allocate about 1.62 MB less per run.
The nested manifest allocates 144 bytes more, and its admitted peak
grows by 192 bytes. Flat admitted peaks decrease by 512 and 475
bytes. Every allowance returns to baseline. Both images match local
CPython 3.13.2 on all four scripts. The external timing ratios stay
with their own revisions.

Windows .NET 11 and Ubuntu .NET 10 each passed 217 white-box and 904
public focused Release checks. The regressions cover repeated shared
children, nested cycles, default-callback cycles, cancellation and
refusal during first spill or replacement, retained chunks and budget
reuse. The new cycle case also matches CPython. The quiet run verifies
all 112 batches, joins all workers and verifies the archive under the
lease in 222 seconds of collection. The six retired aliases preserve
their exact core and every raw result in existing archives.

An eight-second local EventPipe trace of the old expanded export
sampled little time in string escaping. Its sampled thread durations
overlap and include mediated I/O. It shows activity in keyword
expansion but cannot measure removable CPU. The first profiling
attempt stopped at fixture-directory validation. The corrected attempt
and the initial allocation run with an extra regex delta are preserved
with the exact-source comparison.

[Timing summary](summary.json) · [Allocation](allocation-summary.json) ·
[Attribution](profile-attribution.json) · [Source identity](source.json) ·
[Raw observations](observations.tgz) · [Procedures](procedures.tar.xz).
