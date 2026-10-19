# Rejected native exact-add async entry

Local prototype `f163b67c` moves the existing exact BigInteger-add guard/kernel out
of the async binary-operator method into a non-async ValueTask entry. Other operators
forward to the same async remainder. A [source proof](source-proof.json) verifies
that remainder is unchanged. No new operator body, option, metadata or public API.
The prototype is **rejected and was never merged into the runtime delivery**.

48 white-box integer admission/protocol checks and 128 public arithmetic,
suspending-protocol and recursion checks pass. The first public selection lacked
the fresh worktree's Release probe; its sole unexecuted recursion check passed
after building that probe. No test assertion/deadline was changed.

One 7.680-second capture on 2026-10-19 UTC (2026-10-20 local) uses the existing
nested-comprehension helper with N=1024 and exact golden `1364 699050`. Its `x+y`
expression reaches binary add. The integer-loop case uses `+=` and a different
handler, so it was deliberately excluded from this diagnostic.

| Engine | Warm complete kernel | IQR / median | Status |
| --- | ---: | ---: | --- |
| Lython `db86f36f` | 1.064 ms | 1.62% | Before; diagnostic |
| Prototype `f163b67c` | 1.141 ms | 3.10% | Rejected; diagnostic |
| CPython 3.13.2 | 0.104 ms | 22.97% | External context only |

Lython uses RunAsync; CPython executes the same CPU-only script normally.
The candidate median is 7.25% higher in this one shared-workstation capture.
Timing is unqualified: no causal attribution, qualified regression, allocation
claim or general multiplier. An extra forwarding boundary is not justified by
this result. Stop after one capture; no retiming or broader operator campaign.
This experiment cannot explain published synchronous CPython gaps.

Compilation, startup and protocol transport are excluded. Execution/result
cleanup and golden checks are included; the cached compilation stays owned until
worker exit. Seven alternating rounds follow warmup on LOKAD-0399, x64 Windows
build 26200, 28 logical processors, .NET 10.0.13, workstation GC, unbounded explicit
timing root and no forced GC. All owned parent/workers exited within the bound.
The before worker is immutable; its runtime source matches the delivery's code
before this private prototype. Existing comparison tooling was reused.

Evidence: [source patch](candidate.patch), [raw samples/identities](micro.json),
[process receipt](process.receipt.json), [baseline identity](baseline.json),
[focused checks](checks.json), [scope and hashes](scope.json).
The unchanged full catalog is shared with [the earlier diagnostic](../2026-10-19-native-prepared-routing/catalog.json.gz);
its decompressed hash matches the receipt, which includes source, fixture and golden.
