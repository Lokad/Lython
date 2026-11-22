# Scalar activation for calls with omitted defaults

B1408 tests decoding supplied arguments and exact prepared defaults
into machine integers for closed return bodies. The successful probe
bypasses general binding and interpreter dispatch. Default tuples
and dictionaries keep their existing identities and funding. A probe
declines on missing arguments, dynamic defaults and unsupported
arithmetic before the ordinary binder runs. Earlier B1404 applied a
similar kernel after general binding. [B1404](../2026-11-22-closed-return-bound-slots/README.md).

## B1408 Initial probe inside the native entry

Frozen `55424591f` and private `82f89a50f` ran on November 22, 2026
UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
Only the core DLL differs across the 32-file images.

| Complete workload and phase | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| Postponed factories, warm | −4.61 to −0.60% | −4.47 to +0.88% |
| Postponed factories, compile to result | −3.02 to +0.74% | −3.38 to −0.16% |
| Fully supplied helper, warm | −2.50 to +0.54% | −2.41 to +1.37% |
| Eager factories, warm | −6.23 to +0.44% | −5.35 to −0.51% |
| Default metadata observer, warm | +3.40 to +14.65% | +2.71 to +14.46% |
| Quote-heavy CSV invoice, warm | −0.60 to +0.81% | +0.00 to +0.76% |

Negative values mean lower time. Each revision uses two independent
processes with eight seconds of full-request warmup and seven batches
per process. Ranges span all pairings of process medians. Warm elapsed
includes applicable mediated RAM-backed I/O, verification and
prior-result disposal. Compile to result adds compilation and
compiled-owner disposal. Wider CPU covers the worker request. Startup
and final teardown have separate boundaries.

Keep this candidate private. The main warm elapsed range is favorable
with 3.76% candidate spread and mixed wider CPU. The observer is
consistently adverse with 10.48% candidate spread. It reads the
keyword-default dictionary before calling, so the scalar probe
declines. Local allocation and admitted peaks are effectively flat.
Windows x64 JIT diagnostics show that the native entry still clears
the reference slot frame before it reaches the probe. Move the probe
ahead of that entry and check observed defaults before decoding any
operands. These changes address concrete remaining work and justify a
bounded comparison.

Both platforms passed 478 focused Release checks, including missing
unused arguments, identity of an observed mutable dictionary and wide
integer fallback. The new Python case agrees with CPython 3.13.2.
Both images pass five complete script goldens. All 168 timed batches
and both quiet gates passed. Workers joined, the guest mount was
removed and the persistent archive verified under the lease.

[Timings](initial/summary.json) · [Allocation](initial/local-allocation.json) ·
[Source and image hashes](initial/source.json) · [Raw observations](initial/observations.tgz) ·
[Checks and JIT diagnostics](initial/procedures.tar.xz).
