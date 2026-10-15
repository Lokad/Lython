# Direct dictionary get and awaited stored-key equality

Ordinary one/two-positional-argument calls to exact builtin `dict.get` reuse the
existing receiver/operation operands. They remove bound-method construction,
packaged call arguments and builtin bound-value arrays. Both drivers share one
lookup operation with exposed methods; unsupported signatures retain the binder.
Non-string keys use the existing general key-protocol lookup. No new traversal,
callable framework, pool or configuration is introduced.

Builtin-string lookups without protocol keys use synchronous execution in either
driver. Stored-key collisions use the existing awaited string lookup and funded
candidate snapshot. Exposed/expanded/lowered calls share that helper. Unbound
methods now share receiver binding between Invoke/InvokeAsync and forward to the
corresponding bound entry, fixing synchronous host access during async `dict.get`.
Arbitrary asynchronous hashing for other key shapes remains outside this change.

The builtin get signature now rejects keywords, including exposed, unbound and
expanded forms. Argument expressions still run before binding errors; user get
methods retain keyword support. Dictionary contents are read after arguments,
while method/receiver selection occurs before them. Evaluated receivers/defaults
survive callback mutation, suspension and cancellation. [SPEC](../../../SPEC.md)
records this contract. Missing values remain distinct from stored false/None.

| Warm `RunAsync` | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Word counts, 16,384 updates over four Unicode string keys | 18.038 ms | 15.146 ms | 1.313 ms |

One [diagnostic](warm-async.json) observed a 16.0% lower Lython median; baseline /
candidate / CPython IQR-to-median is 1.2% / 0.7% / 1.8%. The remaining gap is
substantial. No qualified speedup, confidence interval or overall multiplier.
All seven rotating batches, calibration, warmup and identities remain. No retries,
retuning or replacement samples. Dictionary writes, iteration and arithmetic also
contribute to this workload; do not attribute their remaining cost to get alone.

The owned command took 8.967 seconds including cleanup under a 30-second bound.
Timing includes new invocation state, execution, capture/projection, golden checks
and prior-result cleanup. Compilation, startup, transport and full response hashing
are excluded. Host operations complete synchronously in this CPU diagnostic;
actual host suspension/cancellation is tested separately, without timing ratios.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 with
ordinary GIL/GC, `-I -S`, UTF-8 mode off and explicit UTF-8 protocol I/O. Local
independent goldens use CPython 3.13.2 with `-I -S -X utf8`. Lython's normal public
policies/shared allowance and GC/tiering remain; CPython has no equivalent
containment. This does not establish current complete-file-script performance.

## Provenance and validation

Baseline is 7e2c1f27b5d6c16b1374f677205790d801d51ae1. Candidate was built before
commit on that revision plus seven production files identified by
[frozen hashes](comparison-declaration.json). Both roles use the identical new
word-count case; comparison/execution tooling is unchanged. [Baseline](baseline-correctness.json)
and [candidate](candidate-correctness.json) match CPython at all three sizes.
[Cleanup/source proof](cleanup-source-proof.json) verifies all seven workers exited,
exact producer differences, identical tooling, inactive services and lease access.

Sixteen [independent Python programs](cpython-goldens.json) cover binding, defaults,
stored false/None, hashability, non-string keys, identity, aliases, argument order,
receiver rebinding, dynamic fallback, callback mutation and generators. Each runs
under both public APIs with shared-budget cleanup. Five [host-file call forms](cpython-host-goldens.json)
independently match CPython outputs and the single equality side effect; file text
newlines are normalized, rather than claiming byte equality across platforms.

Actual delayed host I/O verifies direct, exposed, unbound, expanded and lowered
calls. Cancellation drains before returning allowance. Suspended misses keep the
evaluated default after its global binding is replaced; retained values/defaults
remain funded across another execution, then return accounting to baseline.

Internal controls prove absent method caches and unchanged argument pools across
different dictionaries, all nine fuel/span boundaries, and blocked 64 KiB admission
before candidate storage or guest equality, preserving old dictionary funding.
Existing append, class/method, defaults and invocation controls remain intact.
Focused 97 white-box / 190 public cases, 103 governed CSV/text/JSON/file controls,
and all 2,190 ordinary white-box tests pass. Groups overlap; each takes less than
30 seconds including cleanup. [Validation](validation.json) retains receipts/TRX
hashes. The first 62/63 public run exposed the unbound sync bridge; its failure
receipt remains, and forwarding fixes it. No timeouts/assertions were relaxed.
No new full stress run is claimed. Package/platform gates are recorded at delivery.

This closes this get family. Direct string methods, larger function-local layouts,
other async protocol families and the wider performance plan remain open.
