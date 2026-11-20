# B1209 Current compilation CPU attribution

Profiles production `ef171a35b` with the source-matched `7f583edfa`
core on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on
2026-11-20 UTC. Each worker warms before a roughly five-second all-thread
capture. It compiles a new code owner for each invocation and disposes it.
The measured phase excludes guest execution. The profiler also sees
worker request handling and surrounding activity.

| Samples | Configuration manifest | Generated validator |
| --- | ---: | ---: |
| All process threads | 1,008 | 1,081 |
| Identifiable compiler ancestry | 993 | 1,071 |
| Parser | 185 | 197 |
| Lexical scope preparation | 100 | 75 |
| Abstract interpretation | 263 | 180 |
| Container effect analysis | 30 | 26 |
| Executable construction | 266 | 438 |
| Call emission | 65 | 101 |
| Block reachability and normalization | 11 | 51 |
| Operand layout and linearization | 68 | 83 |

Phase counts use inclusive ancestry and can overlap. The generated
validator has 76 nearest frontend frames in `CompileCallExpression`, 63
in `CompileCodeObject` and 59 in `CompileLoadIdentifier`. The manifest has
52 nearest frontend frames in expression traversal. The corrected phase
mapping includes `RetainReachableBlocks` and `ForwardValues`. The first
mapping remains in the procedure archive. These counts describe where
work occurs. They predict neither a removable CPU fraction nor a speedup.

The next source gate is ordinary call-site preparation. A positional-only
site currently copies one argument form per operand and scans those forms
again to derive positional eligibility. Test representing ordinary forms
with the existing arity while keeping explicit forms for keyword calls.
Share the final representation with synchronous and asynchronous binding.
Measure compilation, compile-to-result and an affected warm control.
The current profiles also keep container-invalidation analysis and block
layout visible for later work.

All 32 source-matched image files and both goldens passed local validation.
Both quiet gates passed. Workers joined and the raw archive was verified
under the VM lease before removing the guest fixtures. The local copy
matched the archived hash.

[Attribution](attribution.json) · [Raw profiles](observations.tgz) ·
[Source and procedures](procedures.tar.xz) ·
[Earlier frontend profile](../2026-11-19-current-frontend-profile/README.md).
