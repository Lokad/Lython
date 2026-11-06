# Text storage and operation provenance — 2026-11-06

R17 removes PyString's source-of-birth reference, the unused IPyGovernedValue
interface/declarations, constructor funding flag and historical-span fallbacks.
Payload factories admit explicitly; values keep their owner/capacity/cache.
Errors use the requesting operation's explicit/current provenance. A source-less
internal helper may have no span; no recorded first refusal is replaced.
No new owner, table, string window, shared payload, policy or public option.
Conservative storage estimates and meaningful ownership tests remain.

Archived patch after fully qualified62141d4d, frozen core `e1d7755d`, versus its
source-equivalent B350 reference `9e05892e`. Normal optional adapter0.4; private
dev44 work is separate. Same b94 worker/protocol2 and complete goldens.

## Allocation, separately from elapsed

Existing managed object-allocation probe: preallocated payload, escaping objects,
unchanged/current/current/unchanged processes. Actual PyString shells are64B in
both baseline observations and56B in both current observations on each actual
.NET11RC3/.NET10.0.8. Ignore the tool's historical synthetic window model.
This does not reduce the deliberately conservative128B+capacity admission estimate.

Existing eight-case isolated-image diagnostic, precise process allocation, normal
GC/tiering,32warmups/5rounds/16jobs. It includes fixture reset, mediated I/O, complete
validation and prior-result cleanup; busy Windows elapsed/CPU are diagnostic only.

| Whole checked job | Run allocation | RunAsync allocation |
| --- | ---: | ---: |
| CSV helpers | −2.67% | −2.43% |
| Text helpers | −4.28% | −4.28% |
| JSON helpers | −3.37% | −3.37% |
| Report | −1.77% | −1.75% |
| Configuration edit | −3.49% | −3.48% |
| Above-range scalar control | −0.01% | −0.01% |
| Fixed text padding | −2.24% | −2.23% |
| Fixed text slices | −3.73% | −3.73% |

Identical-role allocation differences are at most0.157% (configuration sync);
other controls are about0.01% or smaller. All goldens, ownership balances, fixture
cleanup and image-isolation gates pass. The initial hash witness printed values
where the existing driver expects an array return; its failure and source remain.
It failed before workload sampling. Only that witness contract was corrected.

## Complete-script timing and retained debt

B351, quiet Azure F4as_v7/Ubuntu24.04.4/.NET11RC3, five-second warmups per role,
seven rotating150ms rounds, two independent identical-current roles and CPython.
Allocation's CSV helper recipe differs from the ordinary-function timing recipe.

| Complete job / boundary | Elapsed vs reference, current / identical role |
| --- | ---: |
| CSV ordinary function, warm | +0.64% / +1.17% |
| CSV ordinary function, compilation plus execution | −0.40% / −2.72% |
| Text helpers, warm | +2.22% / +1.59% |
| JSON helpers, warm | −1.56% / −1.65% |
| Report, warm | −0.91% / −4.13% |

Retain the simpler representation, physical allocation reduction and provenance
correction. Accept text's approximately1.6–2.2% elapsed regression explicitly;
do not claim broad CPU improvement or debt repayment. JSON elapsed is favorable;
CSV warm is slightly adverse. Fresh CSV/report have2.34%/3.24% role divergence.
Process CPU ticks are coarse and mixed. These results do not explain the entire
remaining CPython gap. Subsequent coherent runtime work must decide this debt;
do not compound independent historical source pairs or repeat unchanged timing.

Five jobs completed140.60s. Actual loaded hashes, stdout/files, funding/Closed,
quiet endpoints, worker joins and removed RAM fixtures pass. Mediated RAM I/O,
validation and prior-result cleanup included; fresh adds compile/disposal.
Startup/staging/reset/transport/final teardown excluded. No SSH/profiling during
collection. Raw evidence archived/verified under the lease; service terminal,
inactive/PID0/success/exit0. Never query that completed service again.

## Validation scope

Both-target Release builds: zero warnings/errors. Focused328internal/1,122public
per actual runtime pass. The original full.NET11 internal run passed4,260 and
failed one ownership fixture that assumed birth-span retention. Its original
failure remains; the fixture now establishes the requesting operation's scope,
then observes/rolls back natively after the stopped operation. Every original
funding, unchanged-source, first-failure and rollback assertion remains. Targeted
correction passes.NET11; all5 related cases pass.NET10. Full public.NET11 passes
16,187. Logs/final regression sources are retained, including the intermediate
fixture failure that correctly refused guest length work after admission failed.

Platform/stress/installed-package qualification for R17 is pending the next
coherent delivery boundary. Baseline621's terminal CI42243450458 is fully green
and cached separately. No publication, RSS, exact live-heap or host-latency claim.
The performance objective and independent regex track remain open.
