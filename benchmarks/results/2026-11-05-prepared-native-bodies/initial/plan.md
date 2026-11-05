# D28 implementation comparison, before capture

2026-11-05, baseline productioncf3dc9cd/frozen43fdf658, candidate parent8243b05c
plus exact patch/source snapshot. Prior source gate/design contract remains the
governing acceptance, including original B207 provenance/readonly lessons.

Coherent migration now replaces normal instruction native edges with16B prepared
operation values only for eligible helpers. Ordinary instruction expected32B.
Delete embedded native fields/copy parameters/WithNativeSources, runtime casing
fusion recognition, separate formatted-builder proof and formatted-value evaluator.
Keep dynamic actual-field type proof/pure lookup (no cached actual values across
later admission), readonly borrowing, native casing, operation helpers and all
existing ownership/cancellation/error/async routes. String operation kind and
delegate metadata is shared, not a guest cache. No new owner/arena/JIT/mode/policy.

Original broad focused Release:1744internal/5171public per actual11/10 pass.
One added independent all15string-method scenario is pending a test-only rebuild;
the first writer used Windows legacy decoding on an existing UTF8 test file and
caused a C# character-literal error. Restore that file from Git and insert only
the new UTF8 fixture. Preserve failed build log; production bits/source unaffected.
No repeated broad suite needed after an unchanged-core test-only rebuild.

Ten existing-driver jobs: CSV/JSON warm; text warm/async/fresh/compile; report
warm/async/fresh; branching ordinary-call warm control. The last is a simple1024
call/checksum source with >1block and empty admitted file fixture, preventing the
native shortcut. Inputs/full stdout/file goldens checked independently under
isolated CPython. Comparison host supplies the existing efficient mediated RAM IO.

Three isolated images/two unchanged baseline roles, identical frozen auxiliaries/
CoreLib/string hashes, normal runtime.6s guest warmup/image/2ssettle/9rotated200ms
batches; owned300s guard and awaited cleanup. Preserve all actual identities,
quiet endpoints, full goldens, live-budget baselines/finalzero, users joined and
fixtures removed. Record compilation separately from warm/fresh; allocation/stride
diagnostics separate from elapsed. No new CPython ratio/CPU/RSS/startup/pendingIO
claim. Stop retain/debt/withdraw; bounded refinement only for actual evidence.
Retained source requires full platform/framework/Stress/installed Release delivery.
