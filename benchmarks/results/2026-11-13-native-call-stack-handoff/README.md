# Native call handoff on managed continuation stacks

R303's composed calls exposed a fatal stack regression after genuine suspension.
The qualified reference **6b468bf1/fc2531b8** returns a structured
`InterpreterStackLimit`; **2f34a48f/47e0a7be** overflows its CLR stack. Full delivery
[43373824703](https://github.com/Lokad/Lython/actions/runs/43373824703) passes Ubuntu
but aborts the Windows public test host after5,749passes. Its last active case is
interpreter-stack attribution after actual suspension. No crash dump was emitted;
the exact job log, complete run logs and unfinished-case sequence are preserved.

A deterministic, public-API subprocess reproduces this comparison. A completed
second command on a one-worker diagnostic profile proves that the first startup
prefix returned and registered its whole await chain. Completing the held host
operation on a thread requesting256KiB then proves that the guest resumed on that
same owned managed thread. The reference exits0 with its structured stack failure;
2f34a48f exits**0xC00000FD**, with25repeated native call chains in its fatal trace.
The first attempts resumed on the large startup worker and are inconclusive, not
evidence against this bug. Their original logs remain available.

Corrected core **2e74ef4f** adds one native-call eligibility guard using existing
logical depth. Deeper calls transfer before child entry to the existing linear
runner. The eight-level threshold governs this optimization, not guest recursion:
there is no new execution counter, quota, public option, worker policy or stack
telemetry. Existing managed stack admission,512interpreter bound, recursion options,
cooperative cancellation and terminal memory refusal remain authoritative. Deep
calls can acquire ordinary frame storage earlier; their admission remains coarse
and host-funded. Shallow composition is preserved.

The corrected probe returns the structured failure at depth10,000 and succeeds at
depth100 on the owned continuation. The maintained subprocess regression reuses the
existing stack fixture to cover direct/mutual recursion, recovery, finite depth100,
actual continuation identity and budget cleanup. **66focused Release public checks
pass per actual .NET11/.NET10**, including existing stack/protocol/cancellation,
recursion, attribution and composed-call ownership cases. No timeout or containment
assertion is relaxed. A fixture-build overload ambiguity was corrected before checks.

[Validation](validation.json) and
[exact corrected sources/binary, fatal witness, failed CI logs and checks](source-checks.tar.xz)
preserve the evidence. Full qualification of2e74ef4f remains pending. Older R303
timings retain their own source labels; this correction claims no speedup. The
next qualified-source external checkpoint will include a bounded invoice comparison
against the earlier Lython reference alongside CPython. Publication remains held.
