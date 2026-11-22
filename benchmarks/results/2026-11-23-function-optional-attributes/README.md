# B1425 Remove optional storage from ordinary functions

One optional field replaces the custom dictionary, type-parameter
tuple and override holder references on every function. It stores a
lone dictionary or tuple directly. Standard overrides and mixed
metadata use the existing lazily admitted holder. The default
`__type_params__` is empty. Assignment preserves tuple and tuple-subclass
identity. Invalid assignment and deletion raise CPython's `TypeError`.
A dictionary installed during admission remains authoritative, and any
unpublished replacement refunds.

Compared `9946d48aa` and `1a8aefaff` on the quiet Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3 on November 22, 2026 UTC. Only the
core library differs in the two 32-file images. `771f8f8b2` retains
the exact measured production and test patch.

| Complete script | Boundary | Elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: |
| Postponed nested factories | Warm | −0.14 to +5.98% | +0.16 to +5.17% |
| Postponed nested factories | Compile to result | +0.65 to +2.77% | +1.30 to +4.45% |
| Eager nested factories | Warm | −4.95 to −0.70% | −4.30 to −0.18% |
| Metadata wrapper factories | Warm | −7.86 to −5.18% | −7.89 to −5.86% |
| Dictionary-only factories | Warm | −0.71 to +0.84% | 0.00 to +1.80% |
| Generic factories | Warm | −1.98 to −0.41% | −1.54 to +0.47% |
| Fully supplied helper | Warm | −0.95 to +0.29% | −0.95 to +0.29% |

Ranges span every pairing of two process medians per source after
eight seconds of warmup per worker and seven rotated batches. Warm
elapsed includes output verification and previous-result disposal.
Compile to result adds compilation and compiled-owner disposal. Wider
CPU includes resets and protocol. Startup and final teardown sit
outside elapsed. The invoice goldens include mediated file I/O.
No external timing ratio was collected.

| Allocation diagnostic | Initial candidate | Refined candidate | Refined admitted peak change |
| --- | ---: | ---: | ---: |
| Postponed nested factories | −6.917% | −6.917% | 0 B |
| Eager nested factories | −6.918% | −6.918% | 0 B |
| Metadata wrapper factories | +0.0004% | −5.442% | +32,768 B |
| Dictionary-only factories | +4.659% | −1.555% | 0 B |
| Generic factories | +1.868% | −0.624% | 0 B |
| Fully supplied helper | Approximately flat | −0.011% | 0 B |
| Quote-heavy invoice | Approximately flat | −0.001% | 0 B |

The initial `8347c9770` design also raised the dictionary-only and
generic peaks by 163,840 B each. The refined design restores those
peaks. Both factory goldens allocate 65,552 fewer managed bytes per
invocation. All allowance baselines return after cleanup.

Retain the smaller ordinary function, the eager and wrapper CPU gains,
and the correctness repairs. Carry the higher mixed-wrapper peak and
the slower postponed compile-to-result lane. Postponed warm elapsed
spans zero and CPU is adverse in every pairing. These results leave
the larger postponed factory performance gap unresolved.

Windows .NET 11 and Ubuntu .NET 10 each pass 85 white-box and 590
public focused Release checks. The reentrant dictionary assertions
and public type-parameter test fail on the exact reference core.
The latter script also passes on local CPython 3.13. Both frozen
images match all seven tool-verified CPython goldens.

All 196 batches and both quiet gates pass. The comparison takes
383 seconds. Workers join, the owned RAM fixtures are removed and
the archive verifies under the lease. Staging retires only the prior
CSV candidate working DLL, whose exact bytes are already archived.

[Sources and images](source.json) · [Timings](summary.json) ·
[Local allocations and peaks](local-allocation.json) · [Observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Reference core](../2026-11-22-csv-literal-field-scan/reference-core.dll.gz).
