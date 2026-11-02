# B128 partial source gate

Current7da7335a/corea3a53e2e, ten-source compile-only census, no guest execution or
production edits. Native vs named-intermediate helpers: canonical4/4/6/5 and
variants6/6/10/7 instructions. Range repeats8 instructions across2 blocks with2
jumps. Static instruction counts are not CPU shares. All native calls already bind
final local slots; factory only allocates separate locals for other entry shapes.
The operand array is separate and admitted with cells/cache/handler arrays.

No full register/arena rewrite is justified solely by these facts. Next: bounded
existing script-style comparison, then a coherent design/deletion choice. Preserve
failed arena/dispatcher lessons and2-3% range debt from B127.

I/O hypothesis check used managed reflection only, on local and actual VM runtime.
Actual Linux WriteAllBytes span IL begins arguments path/Create/Write/Read/None,
ldc.i4.0;conv.i8;call OpenHandle: zero preallocation. This does not support an
asymmetric fallocate fixture explanation. No fixture patch or extra retiming.
IL scanner is a narrow call-site inspection, not a general decompiler. Raw bytes
retained, no pointer/native/Unsafe operations introduced to Lython. The diagnostic
process was synchronous, exit0; previous B127 units inactive/lease free.

Current partial evidence remains private until a useful bounded source-gate
decision can be recorded. No performance or delivery qualification claim.
