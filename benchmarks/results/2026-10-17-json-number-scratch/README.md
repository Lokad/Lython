# JSON numeric scratch placement — 2026-10-17 UTC

| Engine | Measured source | Warm RunAsync job median | IQR / median |
| --- | --- | ---: | ---: |
| Lython | One frozen runtime file on e470ccc2 | 12.438 ms | 5.45% |
| CPython | 3.13.16, PGO/LTO, ordinary GIL and cyclic GC | 4.288 ms | 0.92% |

Nominal Lython / CPython: **2.901×, unqualified**. Earlier Lython e470ccc2
measured 12.283 ms with 1.57% spread. The candidate median is 1.26% higher,
within spread: **no whole-job benefit or regression is established**. Earlier
[shared-defaults measurements](../2026-10-17-json-default-options/README.md) are
separate data and cannot establish a trend across captures.

The existing four-times-token scratch reservation moves from the numeric wrapper
to immediately before the allocating large-integer text fallback. Ordinary span
conversion and hooks no longer allocate a redundant temporary lease. Integer
magnitude admission, result ownership and callback text admission remain. Hooks
keep their text funded through suspension and retained aliases. No classifier,
parser, continuation layer, execution mode, owner or allowance is added.

The pinned .NET 10.0.12 [integer and float span parsing](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Number.Parsing.cs)
uses scalar or type-sized working storage. The floating slow path uses bounded
internal scratch independent of token length; see
[conversion](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Number.NumberToFloatingPointBits.cs)
and [scratch representation](https://github.com/dotnet/runtime/blob/v10.0.12/src/libraries/System.Private.CoreLib/src/System/Number.BigInteger.cs).
Lython continues using existing safe framework calls; no framework implementation
is copied. The allocating text fallback retains its original working estimate,
overlapping magnitude admission and output ownership. See [source proof](source-proof.json).

Contract tradeoff: ordinary conversions and hooks need less unused headroom.
For the fallback, magnitude admission now precedes working-storage admission.
Identical tiny-budget thresholds or prior failure precedence are not promised.
Refusal remains terminal, without retry; actual storage is admitted before use.

Separate warmed native allocation diagnostic, **128 conversions**:

| Conversion | Earlier bytes | Candidate bytes |
| --- | ---: | ---: |
| Small integer | 12,288 | 8,192 |
| Float / slow float / overflowing float | 7,168 each | 3,072 each |
| Large-integer text fallback | 58,368 | 58,368 |
| Numeric hook | 23,552 | 19,456 |

The reduction is **32 bytes per affected conversion**, one temporary lease plus
its reserve/release traffic. This is not whole-script allocation, live-memory,
RSS or timing evidence. Precreated input/context/options and warmup are outside
the counts. Diagnostics used Windows 11, i7-14700KF, SDK
10.0.300-preview.0.26177.108 / runtime 10.0.13, Release and
`GC.GetAllocatedBytesForCurrentThread`, without forced GC. Raw
[before](native-baseline.json)/[after](native-candidate.json), process/TRX receipts
and [diagnostic source](allocation-diagnostic.cs) are retained. The temporary
probe is removed from supported tests and final assemblies.

The complete job is the unchanged JSON helper script: **4,096 records / 110,592
UTF-8 input bytes**, a sorted 25-byte report and stdout `3072\n`. Warm reused
compilation, fresh invocation, completed-host public `RunAsync`, file reset/staging,
prior-result disposal, output capture/projection and full file checks are timed.
Compilation, startup, transport, response hashing, genuinely suspended I/O and
embedding latency are outside this boundary. No fixture is retuned.

Dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, Linux 6.17.0-1022-azure,
four AMD EPYC 9V45 vCPUs, approximately 16 GiB RAM, no swap. Both Release builds
use SDK 10.0.401 / runtime 10.0.12. CPython uses GCC 13.3, PGO/LTO, ordinary
GIL/GC, no JIT/debug/free-threaded mode and `-I -S -X utf8`. Workers remove the
supervisor tiering override and retain ordinary GC/tiering, limits/checks and
explicit owners under an unbounded shared allowance. Instruction fuel is unset.
This is not finite-budget, RSS or host-latency qualification.

The one-file working candidate is on immutable
e470ccc2a62836cbebb3ed80b59acc1ebdda747d; it is not a later HEAD measurement.
[Source identity](frozen-source.json), [patch](candidate.patch),
[preparation](preparation.json), build logs and canonical source in the
[raw archive](raw-evidence.tar.gz) identify it exactly. Baseline binaries were
built from clean source and frozen before overlay; the source checkout then has
the one candidate modification. Assembly revision metadata still names the base.
Every response and loaded identity is in [micro.json](micro.json).

One capture used the existing 15-second collection / 30-second owned group cap
and shared lease, one-second warmups, two-second settling and seven rotating
25 ms batches. Total **9.473 seconds**; **37 responses / 471 invocations** match
goldens/digests. Twelve inputs, 55 frozen baseline files and candidate hashes
remain unchanged. Workers and fixture directories are gone, service is terminal
with an empty group, and the lease is released. See [validation](validation.json),
[ownership](ownership.json), [launch](launch.json) and [terminal](terminal.json).
No recapture, variant campaign, extra profile or full lane ran.

Local Release: **585 focused checks** pass for JSON binding/parsing/hooks,
genuine suspension/cancellation, ownership/admission and complete-file goldens.
Eight frozen numeric cases preserve 136 fuel/span/effect records after removing
only the obsolete temporary-byte component. Hooks are checked for committed
argument funding while pending; fallback denial is checked before decoding.
Six independent snippets match CPython 3.13.2 through each API. See
[local validation](local-validation.json) and [numeric corpus](numeric-corpus-proof.json).
Some raw wrapper receipts retain the previous `json-default-options` label;
recorded assembly hashes and frozen current source identify the tested runtime.
No timeouts, Stress scales, exact denial demands or containment assertions are
relaxed. Stress is retained, not rerun. Package/platform qualification belongs to
the coherent runtime delivery.
