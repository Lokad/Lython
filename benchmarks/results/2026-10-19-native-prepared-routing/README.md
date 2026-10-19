# Native prepared-operation routing

Immutable `bdf777d6` moves six native prepared operations into the existing entry
as a synchronous `ValueTask` producer: comprehension start/finish, display creation,
tuple completion, call-accumulator creation and formatted-part joining. Operations
that can suspend remain in one async handler. The [source check](source-proof.json)
confirms every operation body is unchanged and the six cases occur only once.
No execution mode, opcode framework, public option or resource policy is added.

56 focused white-box checks pass, as do 194 public collection/call checks, 135
formatting/tuple checks and 123 representative-script checks. They cover scopes,
evaluation order, expansion protocols, retained aliases, failure/refusal,
cancellation and cleanup under both APIs. The existing replacement-depth control
also passes after actual host suspension. No redundant architecture-only tests
were added.

One 8.745-second capture on 2026-10-19 uses the maintained
`strings.formatted-report.small` kernel. An ordinary helper renders 256 formatted
rows and returns their aggregate length; every engine produces exactly `5012`.
This is a kernel diagnostic, separate from the complete file-processing jobs.

| Engine | Fresh compile and complete kernel | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `b481e3fd` | 0.679 ms | 8.15% | Before; diagnostic |
| Lython `bdf777d6` | 0.626 ms | 2.63% | After; unqualified |
| CPython 3.13.2 | 0.191 ms | 8.65% | External context on this machine only |

The candidate median is 7.86% lower in this single capture, comparable to the
baseline spread. This establishes no qualified speedup, general runtime
multiplier or allocation/embedding-latency improvement. There is no observed
reason here to expand the routing change; stop after these six cases and do not
retime them as a qualification campaign.

Each invocation compiles afresh and runs once in a running host, including exact
output checks and compilation/result cleanup. Startup and worker protocol are
excluded; compilation alone and warm execution were not separated. Seven
alternating rounds follow warmup on shared workstation LOKAD-0399, x64 Windows
build 26200, 28 logical processors, .NET 10.0.13, workstation GC. All workers pass
exact output checks before and after timing and exit with the owned parent.
The existing comparison command/workers were reused unchanged.

Evidence: [raw samples and identities](micro.json), [process receipt](process.receipt.json),
[baseline identity](baseline.json), [scope and hashes](scope.json), and the full
[frozen input catalog](catalog.json.gz). The catalog is gzip-compressed to avoid
duplicating 11.9 MB of repetitive fixture data; decompressed bytes must match
`catalogSha256` in the scope and raw receipt. The raw receipt also includes the
selected source, fixture and independent golden. Dedicated Linux snapshots in
[BENCHMARKS.md](../../../BENCHMARKS.md) remain separate.
