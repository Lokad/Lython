# B1447 Inline compiler instruction prefixes

Private `bb409a563` replaces per-block instruction lists with scratch
objects containing five inline instructions. Only the excess grows
through separate arrays. The entry block remains reusable across sibling
compilations, and the final executable tape keeps its original encoding.
The read-only validator has 179 five-instruction blocks. The sparse
validator emits 3,900 live instructions over 488 blocks whose existing
staging arrays hold 5,840 slots. These counts come from an instrumented
baseline, separately from the timing images.

Compared `ff2d2dcfc` and `bb409a563` on November 23, 2026 UTC on the
quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Two fresh workers per
revision warm for eight seconds and supply seven rotated batches each.
Ranges cover the four pairings of worker medians. Compile includes
compiled-owner disposal. Compile to result adds execution, applicable
mediated RAM-backed I/O, verification and prior-result disposal.
Wider process CPU includes fixture reset and request handling.
Startup and final teardown sit outside elapsed.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated read-only validator | Compile | −2.80 to +0.61% | −4.82 to +2.17% | 2.24 / 1.24% |
| Sparse generated validator | Compile | +3.93 to +6.16% | +3.58 to +5.43% | 1.92 / 0.22% |
| Postponed nested factories | Compile | −0.71 to +1.02% | −1.89 to +1.63% | 0.61 / 1.13% |
| Generated read-only validator | Compile to result | −4.24 to −1.09% | −3.83 to −0.80% | 1.03 / 2.24% |
| Sparse generated validator | Compile to result | +0.88 to +1.70% | +0.74 to +2.32% | 0.13 / 0.67% |
| Inherited dataclass | Compile | −1.31 to +0.93% | −2.01 to −0.15% | 1.03 / 1.22% |

Keep the implementation private. Local compilation allocates 6.22%
less for the read-only validator, approximately 5.4% less for sparse
validation, 2.51% less for postponed factories and 3.94% less for the
dead-heavy control. The dataclass allocates 1.64% more. Execution
allocation and admitted peaks match, and compilation keeps the existing
coarse owner and estimate. The read-only script reaches its result
sooner, while sparse compilation and time to result take longer across
both candidate workers. The data do not isolate access cost from growth
or copying cost. A further redesign needs to remove instruction access
or finalization work in addition to backing allocation.

The exact 32-file pair differs only in the core. Both images match eight
CPython goldens and identical executable metadata for 149 code objects
over 19 scripts. Windows .NET 11 and Ubuntu .NET 10 each pass 403 white-box
and 3,530 public Release checks. The archive preserves both initial
white-box fixture compilation failures and the Ubuntu probe setup
failures before the matching probe and CPython command were supplied.
All 168 timed batches verify. Workers join, the quiet-lease archive
verifies and guest fixtures are removed.

[Timings](summary.json) · [Compile allocation](compile-allocation.json) ·
[Block inventory](baseline-block-inventory.json) · [Images](source.json) ·
[Raw observations](observations.tgz) · [Source, checks and procedures](procedures.tar.xz).
