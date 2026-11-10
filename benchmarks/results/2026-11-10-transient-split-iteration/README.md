# R203 — private split-result iteration

2026-11-10. Qualified f6798fd7 before1ac042d3 versus isolated refined101ba9d3;
the [retained source patch](prototype-evidence.tar.xz) has identical normalized production source.
Sequential forced nonincremental .NET11RC3 builds, identical039 worker/auxiliaries,
optional adapter0.4 and existing complete scripts/fixtures/goldens. Original
candidate1d0bea78 is preserved separately. Neither comparison labels the candidate
fully platform-qualified; [focused retained checks](retained-validation.json) pass
212internal/617public cases on each actualNET11/10, with a zero-warning Release build.

Compiler-proven immediate consumption of an exact lower/upper whitespace split
uses one private cursor in the existing native iteration engine. It replaces the
PyList shell/object array across direct and closed-helper calls. One shared forward
scanner serves ordinary materialization, initial sizing and cursor movement;
the shared UTF-8 casing writer produces independently owned words. All future
word capacities and cursor metadata are admitted before loop-body effects.
The immutable source and unused conservative capacity stay funded through their
owners. Escaping/assigned lists, dynamic targets, protocols and genuine suspension
retain ordinary execution. No new public option, allowance, pool, tracing or engine.

Original desktop elapsed text +0.71..+4.14%sync/+0.01..+2.38%async, allocation
−5.18..−5.19%, with moving unchanged controls. One source-guided refinement removes
only two newly added duplicate cursor checks. Existing ForNext, scanner/writer
and publication checks remain; focused cancellation/refusal/cleanup still pass.
Refined desktop text −2.14..+0.46%sync/−1.22..−0.41%async, allocation unchanged;
CSV sync +4.43..+6.03% and other variable/adverse controls remain. These desktop
run-only clocks are diagnostic; wider envelope/CPU and all raw samples survive.
[Original](desktop-original/warm/summary.json), [refined](desktop-refined/warm/summary.json),
[source, original conditions, both identities, scripts and test logs](prototype-evidence.tar.xz).

B555: four jobs144.648s, F4as_v7 Ubuntu24.04.4/.NET11RC3, stock GC/tiering,
four independent source/control processes,5s warmup/role and7rotated400msrounds.
All identity/golden/ownership/quiet/join/RAM/archive/localSHA gates pass.

| Complete script | Candidate elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Report, sync | −3.82..+0.54% | −3.88..+0.02% |
| CSV validation, sync | +0.12..+0.42% | −1.30..−0.99% |
| Text helpers, sync | −3.21..−1.04% | −3.28..−0.84% |
| Text helpers, async API | −11.14..−2.32% | −9.58..+0.20% |

Ranges compare both candidates with both references, not confidence intervals.
Sync text has supporting CPU evidence; CSV elapsed is essentially flat. Report
reference spread3.99% and async-text reference spread9.14% leave their effect
uncertain. Async does not establish an11% gain. Allocation falls about5.2% in both
APIs in the separate desktop allocation measurement. CPU, allocation and storage
simplification remain distinct. [Raw cores/observations](quiet/observations.tgz),
[receipt](quiet/archive-receipt.json), [medians/controls](quiet/summary.json).

Quiet clocks include mediated RAM file I/O, capture/projection and prior-result
cleanup; exclude compilation, fixture reset, verification and protocol. Wider CPU
includes those surrounds and coarse ticks. This is a Lython source pair, not a new
CPython comparison. Compilation/startup and real I/O latency are unmeasured here.
Existing CPython rows keep their original source identities.

Decision: retain the complete producer/consumer change for actual collection
storage removal, about5.2% less allocation and modest supporting sync CPU.
The preparatory scanner alone was never promoted. Report/async uncertainty and
desktop adversity stay explicit. This does not close the whole-script objective;
next source gate examines prepared call arguments and operand transport, without
another call arena/control loop. Cancellation, refusal before body effects,
Unicode expansions/final sigma/all29whitespaces, default/keyword/direct calls,
generators, aliases, genuine I/O suspension, awaited cleanup and budget reuse pass.
Public checks also retain rsplit Unicode coverage and escaping mutable-list behavior.

The old ed5 working baseline and two exact raw copies were retired only after
receiving/verifying the new qualified-source baseline and matching all inode
aliases, UID/path and committed Git archive bytes. [Replacement proof](vm-baseline-replacement.json).
Qualified039/priorE294 survive; fixed240MiBfloor/12MiBsetupheadroom remain.
No remaining VM users or RAM fixtures. Publication/tagging remain postponed.

Retained runtime dba779a5 subsequently passes complete Windows/Ubuntu delivery:
both actual target frameworks, original Stress and installed Release consumers.
[Cached terminal proof](delivery-ci.json); no repeated qualification.
