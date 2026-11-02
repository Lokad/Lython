# Current compilation allocation attribution — 2 November 2026

One bounded diagnostic capture of the unchanged complete text-summary helper
source selects a compiler experiment. It is **not a latency measurement, CPU
breakdown or retained-memory estimate**. No external performance ratio is claimed.

Source baseline f9fc488d, runtime source 4cae38f4; core SHA-256
`cb0030174b3e11db90fd16136175582e88a3cd74512771746814c265fcb53957`.
The ordinary optional adapter is 0.4.0. Azure reference VM, .NET 11 RC3;
CPython 3.13.16 validates complete outputs separately. Exact identity, source,
input, reference check and quiet-machine records are in the archive.

The owned service completes in 6.000 seconds. After 128 warmups and two seconds
settling, a 2.01042-second EventPipe window records 2,208 compilations. This
compile-only boundary excludes script execution; normal pre/post invocations
validate stdout/files. All fixture cleanup, worker/collector joins, quiet-machine
checks and final zero allowance pass. Sampling itself affects execution cost.

Offline analysis identifies actual compiler stacks across four worker threads,
not the named guest or busiest thread: 3,519 allocation ticks, 3,427 rooted in
compilation. Approximate sampled allocations total 375.80 MB. Another 8.04 MB of
byte-array allocations have missing stacks, explicitly reported rather than
attributed. Protocol work can share compiler threads.

The stored origins directly attribute 13.11 MB to repeated FinalJumpTarget sets
and their arrays, about 3.49% of the observed approximate total. Other int arrays
have different origins. Do not treat the whole int-array family as jump work.
Statement traversal, abstract-state caches, tokens and source spans account for
larger separate families. Lowered-specific types alone do not justify discarding
Lython's supported fallback evaluator.

Source confirms repeated target walks, dense-ID hashing, ordering and remapping;
this selects B81's graph-normalization experiment, whose outcome is
[recorded separately](../2026-11-02-compiler-normalization-unqualified/README.md).
Immutable lexical binding facts are already shared; annotation and abstract
analysis maintain distinct flow semantics. No general compiler rewrite follows
from pass count or allocation shares.

[assessment.json](assessment.json) and [allocations.json](allocations.json) retain
numbers and origin chains. [reader/Program.cs](reader/Program.cs) is the offline
analyzer; its three declaring-namespace controls pass. The compressed
[evidence.tar.gz](evidence.tar.gz) contains the original nettrace, manifest,
worker responses, reference validation, machine gates and joined process proof.
The original trace remains unmodified. No duplicate runtime qualification.
