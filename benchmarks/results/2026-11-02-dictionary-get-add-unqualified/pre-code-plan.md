# B90 guarded native dictionary get/add — before code

B89 source review closes without capture: JSON deserialization/options are
unchanged since B39's actual complete-job CPU assessment. Ordinary strings,
small integers and floats already use native UTF-8. Remaining generic container
awaiters support real callbacks; B53/B31 variants remain discarded.

Distinct hypothesis: the unchanged CSV/text/JSON aggregation scripts all execute
native dict.get(key,0)+integer through7 ordinary RHS instructions (load dictionary,
resolve direct call receiver, key, zero, call, amount, binary add). Native dictionary
calls already avoid bound method/argument arrays; do NOT claim to remove those.
B71/B39 show substantial VM transport among distributed costs, not an exact
removable CPU fraction. A single guarded executable RHS can remove6 dispatches,
operand transfers and direct-call routing in each ordinary aggregation. This is
not B72's local increment or another interpreter/value-layout replacement.

Compile only exact simple-local reads, direct get with2 positional arguments and
literal0, plus local amount or literal1. Runtime guards: sealed PyDict WITHOUT
protocol keys, PyString key, exact BigInteger amount/current-or-default. Guards
fail to the existing emitted RHS BEFORE allocation or guest callbacks; never catch
refusal/cancellation and retry. Native get retains managed frame admission/current
checks; add reuses existing exact-integer preflight and result ownership. Once
native work starts, exceptions propagate normally. Lookup with noninteger stored
value can repeat only a pure builtin read on the ordinary path, no guest effects.

Success pushes result and jumps to the RHS join. LHS name/key loads and generic
store remain ORIGINAL instructions AFTER addition, preserving rebindings, setters,
async protocols and side effects. Protect aliases, exception/finally/generator
regions, exact overflow promotion/id behavior and missing-local diagnostics.
Global/nonlocal/comprehension shadowed names remain ordinary code. No new engine,
fallback framework, quota, owner/ledger, type cache, unsafe code or dependency.

Compile metadata/control-flow adds one small binding and join per eligible RHS.
Increase existing coarse compilation headroom96->128 bytes/source char to fund
extra code/working overlap before allocation. This is an estimate, NOT an extra
cap or default allowance. Supplied budgets may deny compilation earlier; default
still unbounded. Whole code owner remains funded/disposable. Native execution
uses existing batch funding and current tokens; bounded loops retain checkpoints.

Focused Release semantic/refusal/cancellation/cleanup tests on actual.NET11/10;
independent CPython goldens cover native/missing, numeric/dict/key protocol fallback,
rebinding/evaluation order, finally/loop control and generators. Gated async custom
get/add/set proves no lost suspension. Native admission failure must leave target
unmodified; existing shared-budget/result lifetime suites stay. Verify source
success actually avoids old RHS and ordinary path still executes exactly once.

ONE<=45s actual VM comparison; failed readiness collects NO workload samples and
is preserved while preparing the box. Shut down owned build servers before
readiness. Existing ALC driver, shared CoreLib/hash witnesses, identical ordinary
0.4 auxiliary bits, normalGC/tiering/no forcedGC. Unchanged full warm
CSV-helper.large, text-helper.large, JSON-helper.large and unaffected file-manifest.large.
128 interleaved warmups/image/case,2s settle,7 rotating batches;32 CSV/text jobs,
16 JSON jobs,512 file-manifest jobs. Full fixture reset/mediated I/O/stdout+file
checks/result disposal; compile/startup excluded. Compiled owners end at case
cleanup, final0 and actual fixtures/users gone; both quiet gates pass. Label
boundary; no external ratio or estimated phase subtraction.

Retain >=10% elapsed improvement against BOTH originals on AT LEAST TWO affected
whole scripts; <=3% regression everywhere, original drift<=3%, inclusive-linear
IQR<=10%, batches>50ms, all semantic/admission/identity/ownership/machine gates.
ONE actual comparison, no retiming or weakened gates. Otherwise remove ENTIRE
prototype including headroom change, rebuild nonincrementally, preserve independent
regressions/evidence. Full Windows/Ubuntu ordinary both targets, originalStress
both and explicit Release installed-package consumers only if retained. No release,
tag or adapter package. Docs note coarse headroom change only if retained.
