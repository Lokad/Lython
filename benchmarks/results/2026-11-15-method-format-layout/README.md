# Reusable method-format layouts

R418, retained prototype**9d071eba**, qualified baseline**b06b8593 /8afb146e**.
Same complete JSON-to-Markdown report,1,024records/189,441B JSON; final stdout/file
goldens match the original f-string variant. No CPython source-change claim.

One execution-owned bound-method layout replaces per-call UTF8 template/field
scans, decoded root/suffix/spec strings and constant native spec descriptors.
Both str.format/map use one layout executor and the existing native formatter.
Literal byte runs append in bulk. The old async parser/executor interleaving is
removed; no parallel grammar, global/literal cache, new owner, allowance or policy.
Actual root/attribute/item lookup, value validation and hooks stay at execution.
Grammar errors are stored at their original position, preserving earlier effects;
custom hooks still receive native-invalid raw specs. No caller token is cached.
Long native numeric scans use the active operation token. Argument packet/copies,
native-to-durable promotion and dynamic nested spec materialization remain debt.

**Whole report allocation−18.47%**,7,046,314→5,745,137B/invocation; f-string control
flat(+8B). Same strengthened natural-GC driver checks full stdout/file hashes and
budget baseline after cleanup. Compilation/startup excluded. Baseline byte counts
are not compared across separate collections as if exact constants.

**B812 succeeds142.308s**: warm elapsed**17.42–19.28%lower** /CPU15.58–16.41%lower;
compile-to-result elapsed**18.24–19.83%lower** /CPU17.14–20.56%lower. Source control
spreads warm0.48/1.81%,fresh0.07/1.92%. F-string elapsed−7.00..−0.62%,CPU−7.66..+0.59%
with moving3.20/3.55%controls; CSVelapsed−0.03..+2.79%,CPU−0.64..+2.74%,controls1.67/1.13%.
Retain that adverse/uncertain control debt, not neutrality or a universal gain.
No unchanged rerun merely to erase it; justified different designs remain eligible.

Layouts reserve conservative part/field/name/spec/capacity/replacement headroom
before allocation, charged until execution/result cleanup. They are separate from
compiled literals and refresh when an internal method receives another owner.
Many distinct one-use methods can reject earlier, even after references disappear;
bounded host executions remain the ownership boundary. Operation scratch and argument
copies remain funded through pending callbacks; returned strings retain ownership.
No heap tracing, forced GC, retry, abandoned work or exact tiny-threshold promise.

Focused**12internal/682public each actual.NET11/10** pass after clean nonincremental
Release solution builds. New repeated error-order/type/nested programs independently
pass CPython3.13.2. Existing held-format hooks pass success, in-flight cancellation,
terminal refusal and awaited file cleanup before allowance reuse. Three semantic
public cases and two ownership/admission internal cases added; original assertions
remain. Initial mechanical parser-extraction compile errors and helper-signature
migration were corrected before freezing; no failing timing result discarded.

Quiet AzureF4as_v7/Ubuntu24.04.4,4EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 normal
tiering/workstationGC. Two processes/source,5s full-request warmup,five rotated~0.5s
batches. Warm includesRun/mediatedRAM I/O/goldens/prior-result cleanup; excludes
compile/startup/reset/protocol/finalcache teardown. Fresh includescompile/compiled-owner
disposal; widerCPU includesreset/protocol. No standalone compile, pending-I/O,RSS or
embedding-tail claim. Every identity/golden/budget/quiet/Closed/exit/join/archive gate
passes. Evidence persists/hash-verifies under lease; no workers or RAM fixtures remain.
254/252MiB floors preserved. Exact archived historical0ed core's12aliases and committed
B811working trace archive retired only after fresh UID/path/hash/link/all-alias proof;
current8afb/preceding55ba images and canonical evidence retained. Terminal cached forever.

**Full platform/stress/package delivery pending; publication held.**
[Prior attribution](../2026-11-15-method-format-attribution/README.md),
[medians/all controls](summary.json),[allocations](allocation-summary.json),
[source/image identity](identity.json),[checks](checks.json),
[raw](observations.tgz),[source/binaries/procedures](source-checks-and-procedure.tar.xz).
The broader call/frame/frontend and independent regex roadmap remains active.
