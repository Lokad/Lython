# Current compilation CPU attribution (B45)

One fixed 2026-11-01 capture, same frozen worker as
[B44](../2026-11-01-execution-state-consolidation/README.md): source 44e29e05 +
B44 patch/new files, identical frontend to **a0eae244**. Core SHA256
`170166c60465465730040072bd4d847c1661091d9a7b39c1e34ff23a213d2251`.
Dedicated Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, .NET 11 RC3.
No runtime change, new timing multiplier or CPython comparison.

The earlier compiler profile predates Python tokenization. This capture asks which
current phases merit a coherent consolidation; it does not reopen that lexer work.
Existing worker/profiler protocol, ordinary GC/tiering, one batch of **32,768 fresh
CSV-helper compilations**, **3.084 seconds sampled / 10.628 seconds collection**.
Before/after complete stdout/file verification and quiet gates pass. Each prior
compiled owner is disposed, the final cache owner ends through normal worker quit.
All workers/profilers join; service inactive/dead/success, exclusive lease free.
The compiler thread is identified by actual Compile caller chains, not maximum CPU.

| Component present in a compiler-thread chain | Samples of 613 |
| --- | ---: |
| Static analysis | 182 |
| Parsing | 161 |
| Executable construction | 91 |
| Syntax traversal, overlapping the above | 65 |
| Scope preparation | 48 |
| Syntax validation | 45 |
| Mutation analysis, within static analysis | 25 |
| Lowering | 17 |
| Tokenization | 12 |

Inclusive groups overlap and omit inlined/missing frames; they are not removable
CPU fractions. No chains are empty, but **105 unknown leaves (17.1%)** remain.
One truncated trailing perf-map entry is reported and skipped. Compiler CPU is
3.08 s; background tiered JIT is 0.37 s. These short profiler observations are
not steady-state or embedding-latency qualification.

## Bounded source decision

Current CSV parsing already consumes UTF-8 directly; old UTF-16 conversion samples
cannot justify reopening that pipeline. Scope facts are already prepared once and
shared. Static analysis performs real, state-dependent mutation/alias handling;
its sample count does not justify dropping diagnostics or adding syntax caches.

The arithmetic/bitwise expression parser makes every ordinary operand traverse
six fixed precedence layers through ParseLeftAssociativeExpression/ParseNextLayer.
Those two routines have 38 self samples; their shared helpers also serve boolean
operators. Select a bounded precedence-climbing design assessment/prototype for
the six arithmetic/bitwise levels, preserving separate boolean/not/comparison,
unary and power semantics. It removes that fixed descent for ordinary operands,
without a parser framework, AST cache or runtime value/frame redesign. Magnitude
is unknown. Freeze syntax/diagnostic/bounds acceptance and complete-compilation
before/candidate/identical-before controls in PLAN.md before code.

Stop this capture after that source decision: no recapture or favorable retiming.
Independent runtime/regex work continues; only final released adapter qualification
waits for the producer. The parser prototype is a separate milestone, not a result
of this profile or an explanation of the warm CPython gap.

[Assessment](assessment.json), [all leaf counts](guest-cpu-leaves.json),
[native origins](native-origins.json), [record](record.json) and
[raw archive](raw-capture.tar.gz), SHA256
`67132d28a9eb6e699d1b5ee2546b3951a05eb4d64722003e1468ebc333c5bdcd`, preserve all
maps/chains, identities, goldens, machine gates and collector sources. The known
old collector name collision is fixed before this capture. All diagnostic tooling
is external to production. No new dependency or benchmark framework enters core.
