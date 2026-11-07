# Complete text workload cost gate — R42/B396

2026-11-07. Retained productionfba865ef/frozen5d2ca8e1 with precommit SourceLink1971487a.
Same worker7cf37ca5/adapter27211dbe/normal0.4, .NET11RC3, quiet AzureF4as_v7/
Ubuntu24.04.4. Full R40/R41 delivery42357660230 on exactfba865ef is green separately.
This is one current-source CPU gate, not a prototype, source-pair timing or CPU
improvement claim. Current primary text warm gap1.75–1.79× motivates it.

One maintained complete text-helper script:6s warmup,2s settle,6s wall199Hz
cpu-clock/fp capture with PerfMap3 and selected JIT listings. Job clocks5.5628s,
actual guest5.49CPU seconds,1089guest samples,44unknown leaves preserved. Includes
mediated RAM I/O, full output/file checks and prior-result cleanup. Compile/startup/
staging/reset/final cached teardown excluded. Normal GC/tiering, diagnostic maps/
listings explicit. All other threads/mappings/raw stacks/unknowns are retained.

| Method | Self samples | Inclusive samples | Interpretation |
| --- | ---: | ---: | --- |
| ExecuteInstructionPrefix |229|1056|21.03%self, including inlined guards/slot work; not all removable routing. |
| TryExecuteNativeCall |38|445|Includes useful native helper work. |
| SplitWhitespaceCore |22|198|Useful fused cased splitting/materialization. |
| ExecuteOperandValueOperation |34|129|Actual operators plus transport. |
| InvokeDictionaryGet |41|101|Key query/call work, not proof of binding dominance. |
| ExecuteTargetInstruction |32|74|Target semantics plus transport. |
| CollectWhitespaceForward |44|58|Actual token scanning. |
| ExecuteStackTransfer |23|58|Includes real values/slots. |
| WriteCaseUtf8 |22|55|Actual casing/emission. |

CompleteLocalResult38self, SetSubscriptValue38self, ComputeStructuralHashCode36self
and AddExactIntegers41self also remain. Inclusive stacks overlap. Sampling skid,
inlining and managed/native attribution limit interpretation. Scalar-add~3.76%self
alone does not justify universal tagged values. R39's closed CSV frame gate is not
repeated; its useful/inclusive helper work is not called saved CPU.

Compile-only reuse of the existing metadata census, no guest execution:main module
85instructions,17LoadLocal/5LoadConst,9physical operand capacity, no local mirroring.
words_from_line already has NativeBody and6instructions/2operand capacity. Constants
include admitted PyString/BigInteger and separate span metadata; not every metadata
constant is a guest value. Static counts are not dynamic frequencies or speedups.
Imports already use executable code; builtin functions and prepared helpers are
shared. Do not revive these as missing optimizations.

Next source/design boundary:coherent ordinary-operation input/retirement transport,
not a new arena or scalar representation. Assess replacing pure adjacent local/
constant push/dispatch with explicit input sources across binary/comparison/subscript
read/store families. Name the representation/storage/dispatch removed, preserve
original read spans/order, snapshots before callbacks/admission, genuine suspension,
native-helper proofs, exception cleanup, current cancellation, managed stack and
shared owners. R37's warning remains:deleting leaves alone does not remove physical
capacity and needs snapshot/span metadata. Account for that cost; no narrow opcode
or predicted percentage is selected from these samples. Concrete source deletion
and semantic acceptance precede a prototype; bounded whole-script comparisons with
controls decide retention/refinement/debt. No additional capture selected.

B396 terminal/all gates,120s owner+30s stop+30s margin. Persist/verify raw RAM data
under lease before removal; all users joined/fixtures/maps retired. Independent
retrieval confirms5a609f1c. Windows extraction rejects Linux FIFO metadata ack/control;
preserve the exact archive, skip only those zero-byte pipes when extracting regular
evidence locally. No runtime change, service query or restarted collection for that
format correction. Publication remains postponed; performance objective active.

[Raw profile/code/records](observations.tgz), [all call chains](guest-call-chains.json),
[compile-only census](census.json), [identity](identity.json), [receipt](archive-receipt.json),
[extraction correction](extraction.json), [all commands](development.tgz)
and [full runtime delivery](../2026-11-07-receiver-methods/delivery-ci.json) preserve evidence.
