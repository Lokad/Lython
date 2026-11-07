# Current-token native hashing — R33

2026-11-07. Runtime structural/builtin hash requests now require the active context.
Cold strings and bytes pass its token through the actual byte scan; bytes replaces
one whole-buffer HashCode.AddBytes call with the existing4096-byte streaming hash.
Native hash methods receive their caller rather than discarding it. Native
already-funded dictionary writes and native-format proof reads also carry that
context, retaining their sealed-key/no-protocol eligibility. Context-free CLR
comparers keep their separate, guest-free behavior. No new policy, counter,
watchdog, background matching, owner or hash framework.

Source8c4059b6 plus the preserved twelve-file runtime patch/new regression file.
Final checked net11 07a6530e / net10 6c5dcc32. Existing standalone forwarding probe
changes from sync success under a cancelled token to refusal on both APIs; explicit
token overload still refuses, allowance returns to zero. Baseline source/images,
all implementation stages and original raw probe remain retained.

## Validation

Release solution builds without warnings/errors. Initial broad native-hash/text/
bytes/ownership coverage passes667internal/2,640public per actual target. After
native shortcut correction, broad676internal/2,680public pass per target. Final
native-only shortcut refinement passes127internal/660public per target, including
native helper/formatting and deferred contexts. No result from an earlier variant
is silently presented as full qualification of the final image.

New regressions cover cold native requests without ambient state, separate caller
tokens on cached values, cancellation before native dictionary reads/writes,
unchanged destination/funding, and first admission refusal taking priority over
cancellation. Native str.__hash__/bytes.__hash__ run on a dedicated test worker
with admitted64MiB immutable inputs. Both tests observe the active call, cancel,
receive cancellation without a VM exit, and verify the scan did not publish its
completed cache. A fresh caller can finish/cache the same value. All users join
before cleanup; balances/depths recover and input funding lasts through the retry.
Chunk-boundary cases preserve the old whole-buffer hash values on both runtimes.

The substantial in-flight fixtures are isolated in the existing AllocationSensitive
collection, using existing10s watchdogs. No timeout or assertion is relaxed; no
checkpoint-count hooks are added. Compilation and other CLR/BCL operations retain
their documented cooperative/non-preemptible boundaries.

## Short affected-workload diagnostic

The existing isolated forwarding probe adds a direct native-hash diagnostic mode:
4MiB+3 immutable bytes, fresh str/bytes wrappers, active uncancelled caller token,
one-second warmup per kind, five32-call batches. Before/current/identical-before
processes use normal GC/tiering. Normalized hashes, unchanged input funding and
final zero balances pass. No input allocation, compile/startup or I/O is timed.
This is a native primitive diagnostic, not a whole-script/CPython comparison.

Observed allocation including minor process instrumentation is unchanged:75.25B
per string hash and67.25B per bytes hash in all three images. Windows elapsed
string0.387ms current vs0.402ms controls; bytes~0.389ms in all images. Coarse CPU
is0.488ms per hash throughout. Retain all raw observations; these desktop clocks
do not establish a CPU gain or quiet-machine performance parity.

Full platform/original Stress/installed-package qualification is batched with the
corrected R30–R32 default-storage fixture. Previous delivery42339537256 failed
that obsolete policy assertion after all ordinary/package gates passed; it does
not qualify R33. Publication remains postponed. Continue the representative
execution redesign roadmap after this bounded cancellation correction.
