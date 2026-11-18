# Current operand and collection source gate

R519/B948, 2026-11-18 UTC, committed9e8eaba1 / frozen84b1a159. Full delivery
44250743374 pending independently. One complete floating JSON report, warm;
8-second guest warmup, 5-second capture, 12-second guest capture guard. Existing
profiler and exact32-file worker/catalog goldens, default GC/tiering plus perf
maps only. All quiet/hash/golden/Closed/join/SHAarchive-before-unmount gates pass.
No VM replica retirement; current images/catalogs/toolchains/floors preserved.

1,069 all-thread samples / 4,096 invocations / 5.078 guest seconds; 152 unmapped
leaves. Name resolution has72leaf samples, sequence-index61, generic instruction
prefix124, JSON construction72, native loop68. Inclusive groups overlap useful
body work; neither a removable share nor a CPU/elapsed comparison is established.
Parent thread/reset/protocol/I/O/cleanup are wider than guest execution clocks.
Original source failures/controls in earlier profiles stay retained.

The dominant path still includes operand/name/iteration work rather than a proven
redundant complete call-entry stage. Inspect prepared live module locations next.
R479's old spare-instruction fixup was withdrawn after adverse fresh results and
unstable warm controls; preserve that evidence. Since R509, global producers have
been removed. A new design must remove repeated name-to-slot discovery via shared
lexical facts and exact module-code identity, with no extra AST traversal/value
cache. Independent or detached namespaces retain generic resolution. Validate
rebindings/deletion, builtins, errors/spans, class/closure/import behavior, actual
suspension, concurrent owners, refusal and cleanup. Compare compilation separately
from warm/fresh whole floating execution with helper-text/call controls. No speedup
is predicted; source refinement/debt/withdrawal remains evidence-based.

[Attribution](attribution.json), [raw](observations.tgz), [identity](identity.json),
[verification](actual-worker-preflight.json), [procedures](procedures.tar.xz).
Publication postponed; broader performance and independent regex work continue.
