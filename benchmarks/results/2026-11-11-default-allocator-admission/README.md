# R232 — canonical default construction and attribute admission

2026-11-11, qualified4aa/frozen9ee7 baseline, evidence HEADc0b42f43. **Runtime retained;
full delivery qualification pending.** Only three production files change. Existing
ordinary classes inherit raw PyStaticMethod(ObjectNewMethod), but the canonical
constructor proof previously accepted only raw ObjectNewMethod. Recognize the sealed
wrapper without invoking guest descriptor code. Normal eligible construction now
uses the existing linear frame runner, avoiding generic type-call packet preparation.
Allocator/metaclass guards, post-allocation initializer selection, same-instance
dynamic handoff, real suspension and cleanup remain. No new runner, public API,
option, cache, pool or dependency. [Decision](decision.json),[frozen source/core pair](diagnostic-identity.json).

## Independent correctness

Host admission can grow the same instance attribute table. Charge actual additional
capacity once; a callback that fills the prepared table must stop the outer write
before unadmitted growth. Native refund can also trigger first refusal; stop before
publication and preserve that exact origin. Existing table capacity stays funded
through cleanup. Conservative earlier refusal on invalidated admission is an explicit
ownership contract, not another memory cap or retry policy.

All **three new observable accounting/publication regressions fail on frozen4aa**:
equivalent reentrant construction charges928B instead of544B; filled-capacity and
refund-refusal cases incorrectly publish instead of failing. The isolated copied
test image uses exact9ee7 and the same new test binary. Failure reasons are inspected,
not inferred from exit code; no dependency/type-loading failure. [Red evidence](baseline-reentry-final.json).
The corrected source passes **93internal/143public focused checks per actual.NET11/
.NET10** after fresh nonincremental build21.75s/zero warnings/errors. Existing
constructor ownership fixtures now assert their canonical-route precondition.
Coverage includes metadata mutation without allocation restart, dynamic hooks,
descriptors, cancellation/first refusal, pending cleanup, retained aliases and
shared-budget reuse. [Checks](focused-checks.json). No timeout/assertion relaxed.

## Bounded complete-script comparison

Same six complete scripts and fixedfec79 worker/optional0.4/auxiliaries; only core
differs. The frozen R151 constructor CSV replaces the CSV-counter diagnostic;
ordinary CSV/text/report/JSON and captured-text controls retain their exact sources.
Windows/.NET11RC3, ordinary GC/tiering;256warmups/image,5rotated16-invocation rounds,
two identical baseline ALCs. All goldens, image isolation, stable/final-zero shared
balances and fixture cleanup pass. Compile construction/disposal is separate.
Warm envelope includes mediated file I/O, fixture reset, full checks and result
disposal; excludes compile/startup/final caches. Run-only clocks exclude reset,
post-return validation/disposal. Coarse process CPU is wider than guest execution.

Constructor CSV CLR allocation is **3.77% lower through both APIs**. Other scripts
and compile allocation are unchanged within diagnostic noise. Constructor desktop
envelope is sync−3.55..−1.72%, async−9.20%; these are **diagnostic elapsed observations**,
not a CPU claim. Text/report adversity remains: captured-text async+7.95..+8.00%,
ordinary text async+3.93..+7.26%; some unchanged controls vary substantially.
Neutrality/regression attribution is unresolved. [Canonical sync](canonical/warm-sync/summary.json),
[async](canonical/warm-async/summary.json),[compilation](canonical/compile/summary.json)
link every original sample and wider CPU. No new CPython ratio or pending-I/O/RSS claim.

Two earlier field-storage variants have flat allocation and mixed/adverse clocks.
Inspection proves their intended constructor route was blocked by raw allocator
recognition; an additional dynamic-name barrier exists. Both entire variants are
removed: no field-write-count metadata, speculative capacity prepayment, native
field hooks or root-name special case. Their [initial](initial/warm-sync/measurements.json)
and [refined](storage-refinement/warm-sync/measurements.json) observations, exact
source/cores and private source probes survive. They are not CPU/permanent design
rejections. The smallest concrete canonical correction is retained instead.

Full platform/original-Stress/installed-consumer qualification is next, independently
of deferred regex publication. A bounded quiet constructor/text comparison may
resolve elapsed debt; no full lane or broad campaign. The broader whole-script
performance objective remains open. [Preparation/all source, binaries, probes,
failures and checks](preparation.tar.xz),[archive identity](archive-identity.json).
Publication/tagging postponed.
