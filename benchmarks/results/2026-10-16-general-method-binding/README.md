# General Python method receiver binding — 2026-10-16

Ordinary PyFunction methods now pass their receiver separately into the existing
general binder. The receiver follows its original first-positional binding rules,
including *args, positional-only names, keyword collisions and live defaults.
Keyword calls and calls from lowered bodies avoid the fresh prepend-self array
and copy. Caller argument packaging remains on these general routes. Arbitrary
wrapped callables retain their fresh arrays because they may keep them. Compiled
and generator methods retain their prior general entry behavior.

The existing unbound invocation entries stay unchanged. Receiver overloads keep
the same invocation depth/checkpoints, then share binding, InvokeBound frame entry,
body dispatch and cleanup. No interface, binder/storage abstraction, body/compiler,
owner, stack policy or configuration switch is introduced. The previous positional
executable-call transport remains separately qualified.

| Source snapshot | Bound entry allocation / call | Prefixed control / call | Excess / call |
| --- | ---: | ---: | ---: |
| Before: 7d3f02704a577ea83efe7cf4e93f252ba8d981a3 | 640 bytes | 568 bytes | 72 bytes |
| After: source committed as 90be96838e6987ed66f10e133622ad0798f47fe4 | 568 bytes | 568 bytes | 0 bytes |

This is **allocation evidence for one method-entry shape**, not an elapsed-time
or CPython comparison. Every one of three samples has the same result. Production
sources compile into the white-box test assembly; this does not establish the
allocation profile of a complete job through the published library. Source and
counter evidence support removal of one prefixed array; overall performance,
async throughput and the external gap remain unmeasured. The prior BC timing
uses another source pair and must not be extended to this revision.

The unchanged diagnostic constructs a real class with
`method(self, value, *, flag=0)`, then compares the stored bound method with direct
function entry using a prebuilt `[self, value]` control. Both return the same object
and use identical signature/body/frame cleanup. Argument arrays and class setup
are outside the counters. After 32 warmup pairs, three alternating-order samples
each run 256 calls per route using GC.GetAllocatedBytesForCurrentThread. There is
no forced collection, timing threshold, retry, tuning, VM lane or new benchmark
producer. Owner disposal returns the shared allowance to zero. The temporary
measurement test is retained only as evidence, not as an architectural regression
assertion in the ordinary suite.

Machine: development Windows 10.0.26200 x64, Intel Core i7-14700KF (20 cores /
28 logical processors). Local build SDK is 10.0.300-preview.0.26177.108; the
post-capture installed host inventory reports 10.0.13. The actual diagnostic
process did not emit its runtime patch, so the inventory is not a loaded-runtime
identity proof. This is separate from the dedicated Linux VM / SDK 10.0.401
elapsed comparisons. Both captures were Release, with normal GC/tiering; no quiet
machine or cross-platform allocation qualification is claimed. Captures happened
before the candidate source commit, and both builds may carry parent revision
metadata. Their assembly hashes, commands and source mapping are recorded in the
[declaration](measurement-declaration.json).

[Before counts](entry-allocation-baseline.json), [after counts](entry-allocation-candidate.json),
[counter proof](allocation-proof.json), [diagnostic source](allocation-diagnostic.cs),
[machine inventory](machine.json), [frozen original guard matrices](baseline-checkpoints.json),
[independent CPython witnesses](oracles.json) and [local validation](local-proof.json)
preserve evidence. To inspect the counter locally, temporarily include the
supplied diagnostic source in the white-box project, build Release, run only its
class and remove it afterward. Process-wide collectors are not required.

Local validation: all 2,723 ordinary white-box tests pass (18.927s); 279 public
class/scope tests (15.480s),180 public method/default/protocol/terminal-refusal/shared-
budget tests (3.646s) and81 stack/attribution/copy/native-slice tests (8.628s) pass.
Frozen 15/16 step keyword-call matrices preserve every cutoff, source span, body
effect and unwound counter under both APIs. Actual native suspension in default
lookup retains the receiver before body entry, then releases bound storage only
after success/cancellation cleanup; retained values stay funded until owner cleanup.
Six CPython 3.13.2 goldens match both APIs and are permanent public cases. Existing
wrapped-callable tests preserve fresh retainable input arrays. Original 300-frame
traceback and 100/600 copy-hook fixtures, public cancellation and terminal admission
assertions remain unchanged. Platform/package checks are separate delivery gates;
no Stress claim.
