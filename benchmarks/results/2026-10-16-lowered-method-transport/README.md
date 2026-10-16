# Direct positional transport for ordinary methods — 2026-10-16

Eligible positional calls to ordinary class methods now bind the receiver and
existing operands straight into the existing invocation-owned parameter array.
They avoid caller CallArgumentValue packaging and the bound method's freshly
allocated prepend-self array. The positional binder/invocation methods are shared
with executable functions. Class bodies remain PyFunction, with their original
lowered execution, namespace mirroring, defaults, frame admission and cleanup.
There is no class compiler, changed stack policy, new binder or ownership layer.
Keywords, expansion, variadics, generators and other dynamic calls keep their
existing general route; exposed bound methods preserve receiver/function identity.

| Engine | Measured revision | Warm method-job median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | ba6086f3fb59efa02363ba68ff926c3145a9a8d4 | 1.14515 ms | 1.72% |
| Lython after | d88d64406f53b68910a43fbe62f4f85c0744d7b1 | 1.05985 ms | 1.14% |
| CPython | 3.13.16 | 0.11428 ms | 1.25% |

The Lython median is **7.4% lower in one short diagnostic**. This remains
unqualified: no milestone ratio, confidence interval or general speedup claim.
The much larger external gap remains. These revisions do not describe later HEAD.
The old AP warm diagnostic and rejected AZ compiled-body experiment have different
source pairs; their percentages and execution boundaries must not be combined.

The unchanged `calls.method.medium` source constructs an Accumulator, performs
2,048 add calls with field updates and prints `2102272\n`. Its canonical source
has 207 UTF-8 bytes. Independent integer-formula goldens verify exact output at
256, 2,048 and 16,384 calls under both engines before timing. The micro command
reads case/lane from the declaration; both pre- and post-capture proofs check the
actual case, size, source hash and lane. The historical AZ setup error remains
visible and is not repeated here.

Boundary: warm synchronous public Run over reused compiled code; fresh module and
instance state, construction, loop, updates, output projection and cheap exact
checks are inside the timer. Compilation, worker startup, transport and full
hashing are outside it. Prior-result cleanup is timed; the last result remains
owned through its response. Normal Lython admission, ownership, cancellation and
managed stack protections remain enabled, optional fuel unset. CPython has no
equivalent in-process containment. Fresh compile-to-result, startup, async
throughput, whole file jobs and embedding-process latency are unmeasured here.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS x86_64, four AMD EPYC
9V45 vCPUs, 16 GiB RAM without swap, SDK 10.0.401/runtime 10.0.12. Release workers
retain ordinary GC/tiering; CPython 3.13.16 uses ordinary GIL/GC, `-I -S`, GCC 13.3
PGO/LTO. Only supervisor tiering is disabled. Seven rounds span 6.947 s under a
30 s process-group cap. One capture, no retry, retuning, new producer or long lane.
All 524 production sources per role match frozen Git manifests; comparison tooling
and backend are unchanged. Seven workers/fixtures are gone, services inactive and
VM lease free. Uploaded scripts are LF-validated.

[Samples](warm.json), [declaration](comparison-declaration.json),
[catalog](catalog-micro.json), independent correctness
([before](baseline-correctness.json), [after](candidate-correctness.json)),
[pre-capture gate](before-proof.json), [cleanup/source proof](cleanup-source-proof.json),
[frozen original call checkpoints](baseline-checkpoints.json) and independent
[CPython witnesses](oracles.json) preserve evidence. Source archives, scripts and
local build/test receipts remain in private notes.

All 2,717 ordinary white-box tests, 273 public class/scope controls, 145 public
method/protocol/terminal-refusal controls and 57 stack/attribution/copy controls
pass locally. Six positional pool witnesses fail on the unchanged baseline and
pass afterward; keyword controls still use the general route. Frozen real-callsite
15/16-step matrices retain every cutoff, source span and body effect under both
APIs. Actual suspended native hooks keep receiver/argument storage and funding
until completion or cancellation; cleanup clears the owned box and closes the
owner to zero. Public checks preserve live defaults changed during argument
evaluation, target lookup before effects, arity errors, positional-only behavior,
class/super cells, aliases, real host I/O and terminal refusal. Original 300-frame
traceback and 100/600 copy-hook fixtures remain unchanged. Six independent CPython
probes match both public APIs. No limits, unsafe/native APIs or collector machinery
change. Package/platform gates are separate delivery checks; no Stress claim.
