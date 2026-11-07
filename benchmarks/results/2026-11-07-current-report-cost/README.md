# Current complete-report cost gate — R34/B385

2026-11-07, committed production2f1f2715/focused frozen07a6530e, unchanged b94fa98c
worker/host/optional0.4 adapter. One complete report-render.large warm script on
the quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3 VM. Normal GC/tiering; only PerfMap3
diagnostic override,199Hz cpu-clock/frame-pointer capture. No runtime prototype,
source-pair timing, allocation result, CPython ratio or new full qualification.

Six seconds guest warmup/two seconds settling, six-second wall window:4.906s checked
job clocks,4.62s actual guest CPU,916guest samples,63unknown leaves. Other thread
CPU, all stacks/maps/source/image identities and partial/inlining limitations are
retained. Complete requests include mediated RAM I/O, stdout/file goldens and prior
result cleanup; exclude compile/startup/staging/reset/final teardown. Diagnostic
clocks do not establish ordinary performance or isolate I/O by subtraction.

| Observed work | Samples | Interpretation |
| --- | ---: | --- |
| Instruction prefix |110 self|Mixes useful work, required checks and dispatch. |
| Formatting routing |241 inclusive/37 self|Includes actual UTF8/string/numeric work, not a removable percentage. |
| AppendInterpolatedStringValue |152 inclusive/50 self|Actual formatting plus validation/selection. |
| JSON container remainder |220 inclusive/25 self|Token parsing and collection construction compete with formatting. |
| ReadJsonString |50 self|Current plain UTF8/key reuse already avoids backend conversion. |
| BeginPendingCall |441 inclusive/10 self|The large total includes execution of helpers. It does not establish binding dominance. |

Inclusive stacks overlap and cannot be summed as saved CPU. Canonical argument/frame
and broad scalar rewrites are not selected from these totals. Source checks also
rule out already-delivered small-add admission bypass, completed-JSON synchronous
loops, bulk ASCII casing and the native compiler's exclusion of late mutable reads.

Select **R35 canonical formatted instructions**: formatting still detours through
ApplyOperation, record-type classification and the prepared async driver; native
values with already-evaluated dynamic specs unnecessarily demand a lexical context.
Emit these operations into the existing instruction loop, deleting the obsolete
wrapper records, TryExecuteImmediateFormatting and generic formatting branches.
Keep field descriptors/renderers, evaluation order, actual context promotion,
awaited hooks/cleanup, managed admission, cancellation and funded aliases. No
upfront value proof, alternate backend or public switch. Allocation/compile
diagnostics and controlled report warm/fresh with text/CSV controls must decide
retention/debt; this source gate predicts no percentage or speedup.

The owned service completes05:41:03UTC with exit0; all identity/golden/funding,
quiet, Closed, worker/profiler join, fixture cleanup and persistent under-lease
archive gates pass. Raw RAM evidence is archived/verified before removal. Observe
only after05:43:33UTC (120s owner+30s stop+30s margin); no restart or further service
queries. Archive retrieved and hash-verified independently. One redundant expanded
old trace is removed only after matching its committed independent archive,
checking current hash, ended users and absent link dependencies; no evidence lost.

The initial launcher fails before staging/starting a worker: command-line source
contains its own collector name and trips the existing idle guard. The corrected
transport stages that same launcher as a file; payload/source/capture conditions
remain unchanged. Original error, command source, transport correction and all
inputs are in development-evidence.tgz. A setup refusal is not a failed design.

[Raw capture](observations.tgz), [all mapped leaves/chains](guest-call-chains.json),
[receipt](archive-receipt.json), [source contract](source-contract.md) and
[development/failed setup evidence](development-evidence.tgz). Production delivery
42342955936 is independent and still pending original Stress at this observation.
The whole-script performance objective remains active; publication postponed.
