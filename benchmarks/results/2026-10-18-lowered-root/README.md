# Executable compilation: release the unused lowered-module root

The change removes unconditional retention of a whole `LoweredScript` from
executable programs and their runners. This is a representation-lifetime result,
**not a timing comparison or byte-savings measurement**. The source baseline is
`4d3c90f9734c53a51b64da7f928ef8a851c6dc9c`; candidate files are identified by their
[canonical hashes](validation.json).

| Known compiler reference | Baseline | Candidate |
| --- | ---: | ---: |
| Live lowered-module field in compiled runner | 1 | 0 |
| Whole lowered module on ExecutableScript | Yes | No |
| Original syntax field in runner | 1 | 1 |

The temporary diagnostic inspects only these known compiler fields/property.
It does not walk guest objects or supply runtime accounting/reclamation.
[Before](baseline-captures.json), [after](candidate-captures.json),
[diagnostic source](capture-diagnostic.cs), [baseline process](baseline-captures-process.json)
and [candidate process](candidate-captures-process.json) preserve the evidence.
The diagnostic was removed from the test sources before final matching builds.

Three small lifetime regressions fail on the baseline: scalar, helper-function/
comprehension and class-fallback programs retain the lowered module while their
executable program is alive. The permanent regression now covers both APIs and
annotation initialization, verifies the lowered module can be collected, then
executes the retained program successfully. Direct lowered-route annotation
controls remain. All ten final cases pass. These GC tests use the existing
nonparallel AllocationSensitive collection, no stress-sized fixture or wait loop.
See [failed baseline](baseline-lifetime.trx), [initial fixed cases](candidate-lifetime.trx)
and [final contract/lifetime group](candidate-white-contract.trx).

ExecutableScript stores the existing original ScriptSyntax and entry point.
Native async execution accepts that same executable representation, alongside
the existing lowered route. Both share the same async execution body; only the
annotation syntax and fallback statement inputs change. The compiler clears its
unneeded lowered reference after successful executable compilation. No new
strategy, representation class, cache, owner or execution switch is introduced.

Original syntax remains for annotations and host-requirement diagnostics.
Function metadata and genuine per-operation fallback subtrees remain where
needed. This does not establish that every lowered node is dead. Compilation
still admits the same conservative estimate, charged until the compiled owner
ends; CLR collection does not refund allowance. Runtime code does not force GC
or introduce weak tracking. Managed stack admission, synchronous async prefix,
cancellation, resource cleanup and retained-alias ownership remain unchanged.

**543 focused Release checks** pass: compiler lifetime/annotations/stack/thread
contracts, all 78 existing white-box JSON names and checkpoint records, public
host-capability changes/cache behavior, postponed annotations, async cancellation/
worker reuse, retained owners, class/function definitions and complete CSV/text/
JSON/file goldens. The async body matches the baseline after substituting only
annotation syntax and lowered statement inputs. See [local validation](local-validation.json)
and [source proposal](proposal.json). Original Stress scales and containment
assertions remain; full Stress was not rerun. No new profiler, comparison lane,
speedup, RSS or exact heap-size claim is made.
