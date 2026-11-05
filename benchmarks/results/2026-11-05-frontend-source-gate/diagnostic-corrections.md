# Diagnostic corrections, without recollection

The adapted profiler retained DOTNET_JitDisasm=ExecuteInstructionPrefix and its
output-file setting because a text replacement did not match. The complete
worker identity records all three diagnostic settings; the short per-record
diagnosticOverrides field lists only PerfMapEnabled and is incomplete. The raw
script, identity and prefix-jit.txt are retained. This disassembly targets the
execution interpreter, not compiler methods; verification executes before/after
the captured compilation batch. No normal-settings elapsed comparison or CPU
speedup is claimed. This source-attribution gate requires no replacement capture.

The ordinary-user archive attempt could not read root-owned perf.data. After
successful process/quiet/golden/fixture checks it failed before producing a complete
archive. The exact archive script was rerun with sudo solely for file access; no
profiling or samples were repeated.

The first offline stage classifier matched whole method signatures, so type names
in argument/return types inflated the apparent lowering stage. Corrected analysis
matches declaring owners before :: only, on the same saved stacks. No original
sample was removed or recollected; the corrected stage JSON is authoritative.
Frame-pointer chains remain incomplete/overlapping and are not predicted speedup.
