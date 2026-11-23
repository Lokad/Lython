# B1475 Inline ownership of small float tuples

Private `d4616bc11` replaces the element array of a two- or three-item
tuple when at least two values are fresh floats. Those floats stay in
the tuple's slots until observed. A borrowed element publishes its
canonical box, so the new tuple keeps the element without retaining the
earlier tuple. The existing admission covers the embedded fields and
eventual boxes. The older B1319 design added a wider slot array.

The exact image comparison uses production `123b2c894` as reference.
Windows .NET 11 and Ubuntu .NET 10 each pass 447 white-box and 1,843
public Release checks. The retained-value test covers identity through
`max`, tuple methods, signed zero, NaN, hash and equality, pending
host I/O, cancellation and budget reuse. Both 32-file images match
all five CPython fixture goldens. CPython also matches the identity test.

| Workload | Local managed allocation change | Admitted peak |
| --- | ---: | ---: |
| Typed ASCII CSV | −163,696 bytes (−10.19%) | 3,552,905 B on both |
| Explicit-float CSV | −130,928 bytes (−4.64%) | 6,285,897 B on both |
| Explicit-float CSV with every tuple element observed | +65,592 bytes (+2.33%) | 6,286,305 B on both |
| Configuration manifest and defaults observer | 0 bytes | Unchanged |

## Quiet comparison

Azure F4as_v7, Ubuntu 24.04.4, .NET 11 RC3, November 23, 2026 UTC.
Two independent processes per image each warm for eight seconds and
contribute seven rotated batches. Ranges cover all pairings of their
medians. Warm elapsed includes mediated RAM-backed I/O, result
verification and prior-result disposal. Time to result adds compilation
and compiled-owner disposal. Wider CPU includes reset and protocol.
Startup and final teardown are outside the elapsed clocks.

| Workload | Boundary | Elapsed change | Wider CPU change |
| --- | --- | ---: | ---: |
| Typed ASCII CSV | Warm | +4.50% to +7.31% | +5.11% to +8.49% |
| Explicit-float CSV | Warm | +2.08% to +11.03% | +2.04% to +9.10% |
| Explicit-float CSV | Time to result | +0.67% to +4.97% | 0.00% to +3.64% |
| Explicit-float CSV with every tuple element observed | Warm | +5.87% to +6.61% | +5.71% to +6.90% |
| Configuration manifest | Warm | −1.97% to +0.27% | −2.14% to +0.43% |
| Defaults observer | Warm | −3.68% to +3.94% | −3.20% to −0.06% |

Keep the layout private. The array and box reduction lowers allocation
without increasing admitted peaks. Both target warm runs and the tuple
observer take more elapsed and CPU. The explicit CSV workers vary more
than the stable observer. A refinement that removed the temporary
reservation object on the private inline build passed the same focused
suites. It lowered managed allocation by another 131,224 bytes on typed
CSV and about 262,000 bytes on each explicit variant at unchanged peaks.
That local comparison does not measure CPU. Test the fixed reservation
directly against the retained tuple construction to isolate its effect.

All 168 timed batches pass and the joined cleanup and archive gates pass.
The initial slot-array source was retained privately. Its focused runs
exposed missing CSV fixtures in the sparse worktree. The final inline
runs include those fixtures. Initial VM staging failed the free-space
precheck. The corrected stage reuses space occupied by verified working
duplicates of the older core and profile data. Their exact copies remain
in the retained archives.

[Source and images](identity.json) · [Allocation](allocation-summary-six.json) ·
[Timing](summary.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Checks and setup](procedures.tar.xz) ·
[Direct admission local diagnostic](direct-admission-local.tar.xz).
