# Native JSON value entry

Immutable `b481e3fd` removes always-async forwarding from JSON value emission.
Native scalars complete synchronously; existing container operations return their
completion directly. One awaited tail handles default callbacks and replacements.
Shared checks, growth preflights, cycle/depth guards, callback effects and awaited
ownership lifetimes stay on their existing paths. No writer/framework, public API,
resource policy or configuration is added.

155 white-box JSON, 518 public JSON and 123 representative-script Release checks
pass. Existing controls cover native values, escaping, callbacks, cancellation,
admission refusal, retained owners, rollback and cleanup under both APIs. A new
control in `7f86159c` also proves fresh replacements hit `RecursionError` after
host I/O actually suspends, preserving earlier effects and returning allowance to
baseline. This tests managed stack safety without exact checkpoint/fuel counts.

One 7.789-second capture on 2026-10-19 uses the maintained complete
`scripts.config-edit.small` job: read a 181-byte UTF-8 configuration containing
16 paths, edit it and write the exact expected JSON back.

| Engine | Fresh compile and complete job | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `e69e5fc8` | 2.169 ms | 26.06% | Before; diagnostic |
| Lython `b481e3fd` | 2.153 ms | 27.00% | After; inconclusive change |
| CPython 3.13.2 | 2.192 ms | 15.61% | External context on this machine only |

The 0.70% lower candidate median is much smaller than the spreads. There is
**no demonstrated speedup or qualified regression**. No allocation, warm-only or
embedding-latency claim follows. Do not retime this small entry cleanup or replace
the dedicated Linux snapshots in [BENCHMARKS.md](../../../BENCHMARKS.md).

Each invocation compiles afresh in a running host, including disk staging/reset,
complete file work, exact stdout/output-file checks and compilation/result
cleanup. Worker startup and protocol are excluded. Compilation alone was not
separately measured. Seven alternating rounds follow warmup on shared workstation
LOKAD-0399, x64 Windows build 26200, 28 logical processors, .NET 10.0.13,
workstation GC. Outputs match before and after timing in all engines; the owned
parent and workers exited successfully. Existing tooling was reused unchanged.

Evidence: [raw samples and identities](micro.json), [owned process receipt](process.receipt.json),
[baseline identity](baseline.json), [scope and hashes](scope.json), and the unchanged
[shared sources, fixtures and independent goldens](../2026-10-19-json-scalar-completion/catalog.json).
