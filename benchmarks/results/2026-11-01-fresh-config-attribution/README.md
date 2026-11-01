# Fresh configuration attribution: unqualified combined assessment (B68)

2026-11-01, source9003d045. The phase diagnostic completes all output/accounting
checks. The subsequent fixed4,096-job CPU batch completes, but its11.992s capture
exceeds the predeclared6s limit. **The combined assessment is unqualified.** No
retry, stable latency, CPython ratio or improvement is claimed.

## Diagnostic phases

One existing private success-only phase-driver adaptation, maintained
`config-edit.small`,128 warmups per lane and16 observations per lane. Actual
mediated file I/O and cleanup remain inside their clocks; fixture reset and
verification are outside. Compilation is nested inside each fresh observation.
Reflection/decorators/counters affect these clocks; medians are not additive.

| Diagnostic boundary | Median us | Limitation |
| --- | ---: | --- |
| Reflected warm total before verification | 1,436.10 | Includes normal worker handoff and reflected phases |
| Context initialization | 38.40 | Includes builtin/state preparation; not a removable fraction |
| Execution including host calls | 1,359.75 | Nested host work is included |
| Host elapsed in reflected run | 1,112.87 | Actual opened-file work; independent median cannot be subtracted |
| Execution less its paired host elapsed | 240.61 | Subtracted within each observation before taking the median |
| Public warm total | 1,449.75 | Unmodified public Run |
| Public fresh total | 1,493.65 | Fresh compilation, Run and compiled-owner disposal |
| Compilation nested in fresh observations | 872.75 | Not fresh minus warm; thread/JIT/GC/I/O variance remains |

All48 recorded jobs match complete stdout/file goldens and return to the resident
compiled-owner baseline. Final compiled disposal returns allowance to zero. The
driver exits0 before profiling. Warm/fresh host medians differ materially; these
observations do not establish stable phase shares or an optimization speedup.
They do not support prioritizing a few more shared builtin wrappers: context setup
is small relative to these full jobs.

## Saved CPU evidence and failed gates

Initial quiet gate and isolated pinned CPython3.13.16 small-configuration golden
pass. The unchanged framed worker completes4,096 public compile-run jobs with
full stdout/file verification. Capture wall11.992s exceeds6s. The controller
then stops/joins perf and kills/joins the waiting worker (exit-9); there is no
post-capture verification, final worker budget witness or final quiet gate.
The service ends in19s with exit1, MainPID0, empty control group and lease free.
Normal profiler-worker ownership cleanup is unqualified. No work remains running.

The original perf map is recovered from the same stopped process. Offline
derivation uses the saved events, adding no workload execution. Actual guest TID
2948394 has98 samples/0.53s CPU. Actual compiler TID2948395 is identified by109
Compile caller chains, with236 total samples/1.21s CPU; the thread also performs
fixture/verification work. Compiler groups overlap: parser36, abstract analysis32,
syntax/scope18, executable construction12, annotations9 and lexing1. Unknown
compiler leaves24; one malformed trailing map record is preserved. Missing/inlined
frames and the failed capture limit prohibit causal or removable CPU fractions.
Individual small counts do not justify another local parser/annotation tweak.

The retained source decision is to stop this small-script measurement. Existing
B58/B65 complete-helper evidence and B34 allocation origins remain more relevant
to a coherent call-storage redesign. Such a design must eliminate actual per-call
interpreter/operand/binding storage, preserving observable frame identity and
suspension, rather than relocating unchanged objects as the rejected B61 did.
No runtime prototype or new performance collection is selected by these samples.

[All observations/limits](summary.json), the full phase records, saved compiler/
guest origins and [raw evidence](raw-results.tar.gz), SHA256
`701767b8665ffe6b060869c84eb44a45a3b798d9f84a1acde63adaa476b6ca79`,
retain the exact source/catalog/binaries' hashes, original failed controller,
offline recovery, terminal state and every output/accounting observation.
This historical artifact does not describe future HEAD or replace the maintained
external comparison guide.
