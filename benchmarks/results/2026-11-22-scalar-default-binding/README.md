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

## B1409 Probe before the reference workspace

Private `51f6d1154` checks eligibility before both ordinary native call
entries. An observed keyword-default dictionary declines before any
scalar decoding. The successful read uses bounded machine slots and
enters the existing integer kernel without constructing the native
reference-slot frame. The original arguments, ownership and error path
stay available when the probe declines. The original workspace is
still needed for other calls. The Windows JIT listings confirm the
separate successful probe and show code-generation changes in the
ordinary entry as well.

Compared with the same frozen `55424591f` image on the same quiet VM.
Worker roles reverse from the initial comparison. All other sample
and phase boundaries are unchanged.

| Complete workload and phase | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| Postponed factories, warm | −5.20 to −2.65% | −5.40 to −2.71% |
| Postponed factories, compile to result | −8.31 to +2.24% | −8.84 to +0.93% |
| Fully supplied helper, warm | +0.79 to +1.85% | +0.68 to +1.87% |
| Eager factories, warm | −9.85 to +3.75% | −10.18 to +4.93% |
| Default metadata observer, warm | −0.11 to +2.30% | +0.76 to +2.12% |
| Quote-heavy CSV invoice, warm | +1.17 to +3.43% | +1.14 to +3.82% |

The refined probe confirms a warm benefit for postponed factories,
and the initial observer slowdown shrinks. The observer's CPU remains
adverse. The fully supplied helper and quoted invoice consistently
use more elapsed time and CPU. First-use and eager results remain
inconclusive with 9.26% and 9.90% reference-worker spread. The candidate
stays private because the common call paths carry a measurable
regression and allocation and admitted peaks are effectively flat.
These observations do not separately attribute the controls'
changes to JIT layout or to dispatch. A further design should leave the
ordinary entries isolated and eliminate complete work on those paths
or keep their performance stable. Callable and default allocation
remains a separate cost.

The exact refined source passed 667 focused Release checks per
platform, including native unpack/return, recursive protection,
live defaults and cooperative cancellation. Both frozen images
match five CPython goldens. All 168 batches, both quiet gates,
worker joins and the archive hash verified.

[Timings](refined/summary.json) · [Allocation](refined/local-allocation.json) ·
[Source and image hashes](refined/source.json) · [Raw observations](refined/observations.tgz) ·
[Checks and JIT diagnostics](refined/procedures.tar.xz).
