# Complete managed native-block prototype: withdrawn

R456/B860,2026-11-16UTC. Qualified9e8d1cf5/frozene92d9598 reference versus private
9e8d1cf5+archived complete prototype/frozen477e1d92. Main production is unchanged.
The same32-file workers differ only in core; optional adapter remains0.4.0.
**Withdraw this shape.** It adds compilation/storage cost without an established
whole-script CPU gain. This is an evidence-based decision, not a permanent design ban.

All30 existing native opcodes use prepared managed control flow over existing
slots. The prototype removes NativeExecution.RunCore's switch and the separate
frontend whitelist, retains real operation helpers and transfers once to the
ordinary executable engine before unsupported protocols/actual suspension.
Class/lambda/import compilation receives explicit ownership and current tokens.
No raw IL, unsafe code, package, execution counter, watchdog or public mode is added.

| Complete workload | Phase | Candidate elapsed change | Wider process CPU change |
| --- | --- | ---: | ---: |
| Numeric squared-error report,4,096 records | Warm | +5.45..8.05% | +5.30..9.23% |
| Same numeric script | Compile and run once | +31.20..41.53% | +51.48..63.01% |
| Report rendering,1,024 records | Warm | −0.59..+2.56% | +0.60..1.63% |
| Same report script | Compile and run once | +48.48..51.91% | +51.14..71.94% |
| Same report script | Compilation only | About33× baseline | About33–35× baseline |
| Positional helper/range,16,384 calls | Warm diagnostic | −7.12..+2.83% | −6.56..+2.05% |
| Text normalization,4,096 lines | Warm control | +1.41..6.06% | +2.53..4.59% |

All four process-median pairings are retained, not confidence intervals. Numeric
warm reference/candidate controls move0.10/2.37%; numeric fresh6.05/1.72%; report
warm2.02/1.13%; report fresh0.54/1.76%; calls10.23/0.44%; normalization3.32/1.23%.
Maximum IQR5.29%. Moving call/reference and text controls remain uncertainty;
they do not erase the consistent numeric and new-script adversity. Compilation
alone is about33×, not a33× whole-script slowdown. No causal claim about the
entire CPython gap or earlier regression debt follows.

Quiet AzureF4as_v7/Ubuntu24.04.4,4EPYC9V45vCPUs,16GiB/no swap,.NET11RC3
normal tiering/natural workstation GC. Seven jobs finish201.311s; four independent
workers/job,4s full-request warmups,five rotated approximately0.5s samples. Warm
includes mediated RAM I/O, full stdout/file goldens and prior-result cleanup;
startup,compilation,reset/protocol and final cache teardown are excluded. Fresh
adds compilation and compiled-owner cleanup. Compilation includes owner replacement/
cleanup. Wider process CPU includes reset/protocol. First verification request
times are preserved separately: one compilation/two full runs plus checks/protocol,
not isolated compilation, process startup or a causal framework-startup measurement.

Natural-GC diagnostics show essentially flat invocation allocation and unchanged
conservative execution peaks. The compiled reservation increases36KiB for numeric,
45KiB for report,34KiB for calls and100KiB for CSV. Construction additionally
admits16KiB+2KiB/op scratch; retained estimates are32KiB+1KiB/op. These are rough
ownership allowances, not actual CLR heap measurements. Every complete run returns
to its compiled baseline and final disposal to zero. Local first-compilation/run
clocks are diagnostic only and are not substituted for the controlled VM pair.

An initial successor-generation bug treats a consumer's cased-split recipe as
proof of a later call. Existing tests yield47pass/1fail; recognizing the actual
adjacent shared-recipe pair corrects code without relaxing assertions, then48pass.
Additional101internal/97public Windows.NET11 checks pass. Broader actual.NET11/10
each pass135internal/242public, with four original positive small-budget import
cases refusing additional code/scratch storage. Those budgets/assertions are
unchanged. This remains explicit qualification debt; full platform/stress/package
checks are not run for a withdrawn prototype. Both exact workers match CPython on
six complete scripts. Existing cancellation/refusal/unwind checks pass, including
eight compiler admission-window cases through annotations/functions/classes/lambdas.

A separate generated-code inspection of the numeric helper is FullOpts,39inlinees,
696native bytes. Small wrappers substantially inline; a blanket missed-inlining
explanation is unsupported. Arithmetic/fallback helpers and initial-slot unboxing
remain visible; cold-path calls are not hot CPU attribution. An initialization
cleanup alone would not address the33× compilation cost, so no preferred-sign
retiming is scheduled. Dynamic-code/AOT compatibility, retained storage and lack
of interruption inside framework compilation/JIT remain additional tradeoffs.

After freezing, three independent stale-compilation-token tests are added; all11
compiler cases pass on the private image on actual.NET11/10. Only the three
general lifetime cases are retained on main: together with its original two,
five pass per target through both execution APIs with shared-budget cleanup.
The six new admission-window rows depend on discarded codegen and remain private.
No runtime speedup is claimed from retained coverage; no publication or tagging.

All VM gates pass, users are joined and fixtures removed. Evidence was archived
and SHA-verified under the lease; four exactly proved committed raw replicas were
retired with fresh UID/path/hash/all-alias checks. Canonical evidence, toolchains
and selected images remain. No source/observation was discarded or relabeled.

[Decision and all pairings](decision.json),[exact source/image inputs](identity.json),
[complete sources](candidate-sources.tar.xz),[observations](observations.tgz),
[original failures/checks/JIT inspection](focused-checks.tgz),[procedures](procedures.tgz),
[prospective design](design.md). The broader whole-script objective remains active.
