# Prepared patterns and compile-owned literals

P01 prototype on9d40a6db, measured2026-11-06, core`0873ec46`; reference
9d40a6db/core`887002c2`. [Frozen source identity](identity.json) and
[raw observations](observations.tgz) preserve the exact changed source, workers,
roles, machine gates and measurements. [Validation](validation.json): zero-warning/
error Release build,3388public/76internal per actual.NET11/10, six independent
CPython goldens and two additional frame-admission cases. Baseline9d is qualified
GREEN/all8; P01 full platform/stress/package qualification is pending.

One compile-owned lowered pattern is consumed by executable and lowered class
paths. It replaces raw syntax/expression matching, runtime name walks, capture
Dictionaries and OR copies. Frontend uniqueness/equal-OR-name validation means a
successful branch overwrites all its private slots, requiring no rollback copy.
Constant mapping keys skip duplicate work proven at compilation; dynamic keys
retain ordered evaluation, native duplicate checks and value/subpattern phases.
Sequence extraction snapshots positions before callbacks, including starred tails.

Executable frames admit known capture/working capacity with scalar headroom and
clear it before guards/bodies. Lowered-only consumers use existing temporary
reservations. Earlier frame denial is intentional. Native seen/rest cleanup,
first terminal refusal, captured aliases and suspended body funding remain. No
new owner, pool, tracing, allowance, execution backend or policy is introduced.
The global lowered-literal ConditionalWeakTable/three wrappers and duplicate
executable literal construction disappear; immutable payloads belong to lowered
nodes and existing compilation ownership. Executable cases do not retain unused
lowered guards/bodies. Existing builtin-class positional and class-property/value
protocol limitations are separate semantic work, not claimed fixed here.

## Controlled source pair

B306:111.20s/four jobs; normalGC/tiering, unchanged worker/protocol2,
five-second warmup/role/job, seven rotated150ms rounds. Quiet Azure
Standard_F4as_v7 Ubuntu24.04.4/four AMD EPYC9V45 vCPUs, .NET11 RC3 SDK,
CPython3.13.16. Candidate/control are independent processes with identical core
bytes. All workers joined, fixture directories removed and both quiet gates pass.
The first launcher failed before measurements because of CRLF; its original bytes
and journal are retained. Correcting the launcher changed no runtime/conditions.

| Workload / boundary | Reference ms | Current ms | Identical-current ms | CPython ms | Current/reference |
| --- | ---: | ---: | ---: | ---: | --- |
| Text summary3072lines / warm | 2.524972 | 2.554518 | 2.500564 | 1.251216 | +1.17% / -0.97% |
| Record patterns128rows / warm | 0.192504 | 0.136307 | 0.139711 | 0.031520 | -29.19% / -27.42% |
| Record patterns128rows / compile | 0.034158 | 0.034420 | 0.033434 | 0.032630 | +0.77% / -2.12% |
| Record patterns128rows / compile-run | 0.241902 | 0.181237 | 0.168662 | 0.068273 | -25.08% / -30.28% |

Record warm improves27–29%, compile-to-result25–30%. Fresh-role spread6.94%
limits precise magnitude; both roles improve substantially. Compilation alone
+0.77%/−2.12%, text+1.17%/−0.97% are inconclusive. Retain for coherent deletion
and supported record gains. Do not add deltas to earlier B305 or claim an exact
net improvement against its earlier reference. Record warm still~4.3–4.4× CPython;
the complete text control~2.0×. These diagnostic cases do not close the whole-script
performance objective, nor measure allocation, embedding latency or CPU causation.

Warm clocks include invocation, mediated RAM I/O, full output checks and prior-result
cleanup; compilation and process startup are excluded. The pure record case has
no file I/O; text performs mediated RAM-file reads/writes. Compilation includes
program creation/disposal; compile-run adds fresh execution/result cleanup. Fixture
staging/reset, worker transport and final process/program/result teardown are outside
clocks. Both CPython and Lython use the existing comparison-host boundaries; persistent
CPython imports and per-execution Lython imports remain a disclosed asymmetry.
