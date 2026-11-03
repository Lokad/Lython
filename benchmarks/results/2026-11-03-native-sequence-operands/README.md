# B171: private native sequence iteration

2026-11-03. **Retain with the documented synchronous report tradeoff.** Source **b7a0f40a**, committed/pushed. Focused Release 802 public/214 white-box
per actual .NET 11/.NET 10 pass. Full delivery **41682308042 GREEN** at exact source: Windows/Ubuntu, actual
.NET 11/.NET 10, installed package consumers and original stress scales.
[Cached exact-source receipt](delivery-ci-status.json). Publication postponed; whole-script performance stays open.

Exact list/tuple loops keep their source in the existing private operand slot,
with one lazily admitted fixed-size position array. This removes recurring general
Cursor/CLR enumerator creation and generic next dispatch for those loops. No new
cursor pool, owner graph, compiler metadata, backend, configuration or boxed state.
General cursors remain for exposed iterators, generator-expression outer acquisition,
user protocols, resource-bearing iteration and genuine asynchronous I/O.

The position array shares frame storage admission/refund and remains funded through
generator/callback suspension. It contains no guest references. Removing operands
releases the sequence; GetIter resets positions before reuse. Terminal frame cleanup
relinquishes the array before refunding. First refusal is terminal before loop-body
effects, preserving its original source span. Non-allocating loops retain token
checks in the shared instruction driver. No runtime tracing, quotas or unsafe code.

## Complete-job comparison and decision

| Workload | Sync elapsed % / unchanged control | Async elapsed % / control | Allocation | Accounted peak change |
| --- | ---: | ---: | ---: | ---: |
| Text summary/helpers | −3.98 / +0.41 | −6.14 / −4.87 | −12.58% | +68 B |
| CSV summary/helpers | +0.60 / +0.18 | −1.38 / −1.23 | essentially flat | unchanged |
| Report rendering | +0.50 / −1.75 | +0.58 / −0.21 | +0.33% | +52 B |
| Repeated list loops | −9.82 / −1.42 | −15.84 / −1.10 | −27.13% | +52 B |
| Repeated tuple loops | −4.41 / +5.85 | −14.61 / +0.35 | −29.82% | +52 B |

Text sync CPU **−4.05%**, control +0.36%, supports a lower CPU cost on that complete
script. Async CPU −6.03%, control −4.94%, has substantial unchanged-control movement;
do not attribute the full async change to this implementation. CSV is near controls.
The loop controls preserve break/continue and repeated-entry semantics; their
large gains do not establish the same magnitude for arbitrary scripts.

Initial sync report is about 2.2% adverse relative to control. A bounded, report-only
confirmation swaps **source roles**, using exactly the same core bits: new B171 as
before/control, old B166 as the tool's candidate. It increases warmups/batch sizes
to 512/128 from 256/64, preserving nine rotated samples and both APIs. No source
refinement or favorable replacement of the original result.

| Source-role confirmation | New versus old | Identical-new control versus old | Identical-new separation |
| --- | ---: | ---: | ---: |
| Sync elapsed | +1.36% | +1.33% | −0.02% |
| Sync process CPU | +1.37% | +1.62% | +0.24% |
| Async elapsed | −3.07% | −2.33% | +0.76% |
| Async process CPU | −3.22% | −2.02% | +1.24% |

Sync report slowdown is reproduced with close controls; it is not dismissed as
noise. Allocation +0.33% is stable across both APIs/roles: the position-array
reference enlarges every interpreter activation even when it never iterates a
native sequence. This allocation explanation does not prove the CPU cause.
Accept the measured roughly 1.35% sync report cost for the simpler private-loop
representation, complete-text gain and lower repeated allocation. Carry it into
the complete activation milestone as explicit regression debt; do not claim every
script improves or automatically veto a useful redesign at a fixed percentage.
No additional unchanged timing run is selected. Later activation work must decide
remaining debt at its completion rather than silently forget it.

## Conditions, semantics and qualification

Baseline retained/fully qualified B166 **2323ccebc8d526e5a5e0214d85d2de9feba5bff2**,
core **3bed411efcd6e008fc04bca8634d32be64bafc71742509ff0192e8eee86ab8bc**.
Candidate **6d4b0c32961c01a1fe1f975585ceb01b3791ca24f1cd24b5750f60dac01161ed**,
frozen after joined focused checks, before its matching source commit.
Source snapshots include newly added tests that the unstaged patch alone omits.

Existing complete-job driver, same quiet Standard_F4as_v7/Ubuntu 24.04.4/.NET 11 RC3,
ordinary GC/tiering. Three isolated images share CoreLib/process hashing: before,
candidate and identical-before control. Five cases at existing large size: complete
CSV/text/report and two trusted native-loop controls independently verified under
isolated CPython 3.13.2. No external CPython speed ratio derived here.

Both APIs, actual mediated RAM-file I/O, fixture reset, full stdout/file validation
and result disposal included. Compilation/startup/final cached teardown excluded;
no frontend change or compile-only retiming. Initial 256 warmups, nine rotated
64-job batches, two-second settle/case. All goldens, identities, zero balances,
quiet gates, fixture removal and joined users pass. Initial service **126.296 s**;
report confirmation **75.439 s**; both exit zero and release the shared lease.

Focused correctness includes live list replacement/appends/removal, nested loops,
comprehensions, generators, protected break/continue/return, exposed iterators,
actual suspended iteration/disposal, cancellation, retained aliases and budget
reuse. New checks demonstrate interruption during repeated non-allocating list/
tuple loops; denial at acquisition leaves the source unchanged and precedes body
effects with original attribution. Unstarted generator outer acquisition retains
its real cursor. No timeout, stack rule or original stress fixture is relaxed.

[All initial observations](comparison-tables.md), [assessment](assessment.json),
[source-role confirmation](report-confirmation/assessment.json),
[raw initial samples](observations.tgz), [identity](identity.json),
[source snapshot](source-snapshot.tgz),
[allocation motivation](../2026-11-03-activation-storage-profile/README.md).
These are bounded source-pair observations and cooperative accounted peaks, not
embedding RSS/latency qualification or a current CPython multiplier.
