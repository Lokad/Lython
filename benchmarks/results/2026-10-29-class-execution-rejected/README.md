# Executable class-suite prototype: rejected

Base: `d5c31951`, 2026-10-29, .NET 11 RC3 Release on Windows. The
[complete candidate patch](candidate.patch) is evidence only; it is **not applied**.
Production and test sources were restored before further development. The
[failure record](failures.json) contains the exact base/patch identity, completed
public results, partial white-box results and full failure messages. No timing
was collected and no speedup is claimed.

The prototype compiled class suites through the existing executable builder and
prepared class-header operation. Class methods/lambdas/generator expressions then
used existing executable constructors. It preserved a live class dictionary,
ordinary/global/nonlocal lookup, the separate hidden class cell, existing header
and type construction, and a coarse preflight for new private class-frame arrays.
It introduced no runtime backend, unsafe code, policy switch or collector.

Focused validation reached 70 internal boundary passes and eight new independent
CPython class-namespace/generator goldens, plus two construction-refusal controls.
An earlier coherent selection passed 3,126 public cases. Those results did **not**
establish stack safety. The full ordinary public run completed with **14,991 passes
and four failures**:

- Deep ordinary method failure at 300 recursive calls returned RuntimeError
  instead of the existing ValueError with all 301 guest frames. Moving methods
  onto executable bodies changes interpreter-frame admission depth.
- After real host suspension, the isolated `setdefault-hash` fixture suffered
  process stack overflow while recursively rethrowing through guest hash methods.
  This is a real containment regression, not an acceptable earlier rejection.
- Two LA3103 expectations were independently stale after the committed format_map
  mapping-contract fix. Removing those expectations belongs to the live test
  cleanup; they are not class-prototype defects.

The white-box run was deliberately stopped after 2 minutes of repeated obsolete
dictionary-local/type assertions and the resulting 5–10-second pause-signal waits:
2,993 passes, 59 recorded failures, then an aborted run. Only its verified owned
test host was terminated; the public run had completed. It is not a passing full
check. Further assertion repairs would not fix the demonstrated stack overflow.
Timeouts, containment assertions and original stress scales were not relaxed.

The design did preserve ordinary semantic/ownership witnesses after correcting
hidden-cell/nonlocal and class-annotation boundaries. Those corrections remain
inside the rejected patch because their triggering implementation was removed.
The new generator controls likewise describe a proposed extension, not today's
class-site behavior. Do not claim it shipped or leave those unsupported tests live.

Next work must address call-frame/admission and exception-unwind structure before
resurrecting this migration. Preserve the current measured synchronous method
optimizations and validated managed stack policy meanwhile. A replacement must
pass the existing 300-call traceback and all genuinely resumed callback/finally
fixtures on both runtimes/platforms; lowering a constant or weakening those checks
does not qualify it. Explicit managed guest frames are a possible direction, not
a demonstrated solution to recursive native callback/unwind paths.

After restoring the runtime, Release builds on both targets have zero warnings or
errors. All ordinary Windows net11 tests pass: **3,878 internal + 14,985 public**.
Focused net10 mapping, compilation-tooling, resumed-stack and traceback checks
pass: **51 internal + 616 public**. The published 0.10.0 package/symbol hashes and
release tag are unchanged. Full dual-platform/stress/package delivery validation
of later 0.11 changes remains separate; these checks do not complete the plan.
