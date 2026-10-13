# Rejected native activation array

The prototype is **rejected and fully reverted**. Its standalone activation
array introduces recurring callable bookkeeping before complete child/resource
coverage and traversal funding exist. The one short timing group does not
demonstrate an acceptable cost: the median rises 6.79%, with a similarly sized
candidate spread. This is an adoption decision under uncertainty, **not a claim
of a statistically established regression**. Forced-GC replacement remains open.

| Warm workload | Input | Before Lython | Rejected prototype | CPython | Status |
| --- | ---: | ---: | ---: | ---: | --- |
| Keyword-call accumulation | 16,384 calls | 3.814428 ms | 4.073303 ms | 1.119545 ms | Historical diagnostic; direction/external multiplier unqualified |

Measured on 2026-10-13, dedicated 4-vCPU x64 Ubuntu 24.04.4 VM, .NET 10.0.12
and CPython 3.13.16. Before library: `f57003a8`, production `8a6cc16d`, identical
production to the starting HEAD `e3b63c91`. Prototype:
`d159bbe619fd9e947560713e914d691bb3f26503`, production `1fbfa483`.
Revert: `db1ecc37`, restoring production `8a6cc16d`. Portable Release builds use
the pinned Windows SDK 10.0.300-preview.0.26177.108; the VM SDK directory is
10.0.401. The reused baseline observer carries revision `8366e832`; its unchanged
observer source loads the accepted `f57003a8` library, not that earlier rejected
runtime. Observer/library identities are frozen in the raw receipt.

The existing micro tool compiles once and warms persistent workers. Its fresh-
state synchronous invocations include ordinary projection and captured output;
compilation/startup are outside timing. All engines match independent exact
output. Seven rotated rounds retain all original samples, under ordinary settings.
The owned group takes 8.542 seconds, with no paging, major faults, steal or
runtime overrides. Its process and service finish, the cgroup is empty, and the
lease is free. There is one measured group, no recollection and no ten-minute lane.
IQR/median: before 2.29%, prototype 6.76%, CPython 0.91%. No confidence interval,
qualified multiplier, full-job/async throughput or embedding-latency claim follows.

The prototype rewrites 107 callable entry sites through the existing helper and
stores target/caller/raw/bound arguments, working storage and pending results in
one reused per-run activation array. Growth funds overlapping backing before
publication; completion clears references. Native result registration publishes
before admission, and `sum()` exposes its evolving accumulator. Existing frame
objects form an active executable chain; function binding publishes the actual
callee context. It adds no scalar identity ledger, unsafe code, root reflection,
configuration switch, universal retained operand extent or logical refund.
It changes 64 source/test files and charges native administrative storage; complete
working-state/child/resource/workspace coverage remains missing.

Eight focused probes pass across synchronous and genuinely suspended asynchronous
arguments, callback exceptions, evolving accumulation, registration denial and
scope cleanup. 37 existing call/binding/registration white checks and 54 public
root/native-streaming/stack/cancellation controls pass. Those checks demonstrate
the represented boundaries; they do not prove complete root coverage. No full
prototype suite or exact prototype package gate is claimed: the candidate stops
before those broader gates. The normal restored production is already qualified
by the cached `e3b63c91` CI; its fresh build is recorded in [validation](validation.json).

The new fixture first failed an xUnit analyzer rule; a subsequent filter ran on
the previous test DLL and matched no tests. That run is **not a pass**. The first
working fixture passed seven of eight cases: its external async check incorrectly
expected the module context instead of the callback's generator context. Capturing
the actual callback context corrects the fixture, after which all eight pass.
Original build/no-match/failure receipts, source edits and semantic rewrite records
remain private in `.git/agent-notes/native-activation-20261013/` and Git history.

R17's combined ownership objective remains intact. Do not repeat this producer,
silently adopt its helper/frame subsets, or substitute creation-scope/retry-flag
patches. The next representation should consolidate roots with existing invocation,
binding and working storage, with a complete funded traversal/release rule before
refunds. Reuse the [R16 inventory](../2026-10-13-ownership-inventory/README.md);
do not start another metadata campaign. Preserved managed stack, host mediation,
cancellation, preflight, cycle, projection, alias and streaming contracts remain
acceptance gates.

Raw evidence: [micro](micro.json), [process](process.receipt.json),
[input manifest](manifest.json), [catalog](catalog.json), [supervisor](run-micro.py),
[stdout](stdout.txt), [validation](validation.json). Binary bundles remain private
with recorded hashes. [BENCHMARKS.md](../../../BENCHMARKS.md) remains authoritative.
