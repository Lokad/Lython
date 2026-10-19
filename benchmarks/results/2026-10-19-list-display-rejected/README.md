# Rejected guarded list-display append

Prototype `dbb1f019` added a native route for ordinary non-unpacked `PyList`
display appends. It inspected the receiver below the operand and active iterators,
guarded malformed-stack indices, then used the existing insertion helper and
cancellation checks. Sets, dictionaries and unpacking kept their prior paths.
46 white-box, 197 public protocol/lifetime and 123 representative-script checks
passed; this was not rejected for an observed semantic failure.

The extra receiver/stack dispatch adds complexity without a demonstrated benefit.
`abd98e0e` removes the prototype. At that checkpoint, production and architecture
sources again match the fully qualified `66c050d0`; its six native-operation
simplifications remain.
Do not revive or retime this guarded route as another qualification campaign.

One 7.833-second capture on 2026-10-19 uses the maintained
`comprehensions.helper-list.large` kernel: an ordinary helper builds a nested,
filtered list from `range(1024)`, then reports its length and sum. All engines
produce exactly `1364 699050`.

| Engine | Fresh compile and complete kernel | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `bdf777d6` | 0.942 ms | 4.05% | Before; diagnostic |
| Prototype `dbb1f019` | 0.966 ms | 30.89% | Rejected; timing inconclusive |
| CPython 3.13.2 | 0.133 ms | 14.90% | External context on this machine only |

The 2.55% higher candidate median is small relative to its spread. This proves
neither a speedup nor a regression. Allocation and embedding latency were not
measured. The decision favors simpler dispatch rather than interpreting noisy
timing as a performance result.

Each invocation compiles afresh in a running host, including exact output checks
and compilation/result cleanup. Startup and worker protocol are excluded;
compilation alone and warm execution were not separated. Seven alternating rounds
follow warmup on shared workstation LOKAD-0399, x64 Windows build 26200,
28 logical processors, .NET 10.0.13, workstation GC. Exact outputs match before
and after timing, and the owned parent/workers exited. Existing tooling was reused.

Evidence: [candidate patch](candidate.patch), [raw samples and identities](micro.json),
[process receipt](process.receipt.json), [baseline identity](baseline.json),
[scope and hashes](scope.json). The unchanged full input catalog is shared with
[the prior diagnostic](../2026-10-19-native-prepared-routing/catalog.json.gz);
its decompressed hash matches the raw receipt. The receipt includes the selected
source, fixture and independent golden.
