# UTF-8 JSON numeric tokens — 2026-10-16

One unchanged complete JSON-lines aggregation job, warm synchronous execution:

| Engine | Revision/version | Median | IQR / median |
| --- | --- | ---: | ---: |
| Lython baseline | 09f44e94b7815f172bc1ae40bbe97e16bd4883c8 | 9.814 ms | 17.6% |
| Lython candidate | dc950cdecf1bbe469ba7880dfefd45b821d2e4d3 | 9.587 ms | 4.1% |
| CPython | 3.13.16 | 4.401 ms | 0.3% |

The observed 2.3% lower median is within baseline spread. **No improvement is
established.** One diagnostic, no repeats/tuning/new producer/long lane. There is
no qualified ratio, confidence interval or host-latency qualification. The earlier
string-token comparison remains separate evidence and was not repeated.

Two production files change. Numeric tokens classify once from already-matched
ASCII bytes. Default fixed-width integers and floats use managed UTF-8 parsing;
integers still become Python integers, and larger integers use the existing exact
BigInteger string fallback. The shared magnitude preflight adds a count overload;
the previous string caller delegates by Length, keeping its saturating estimate.
No new numeric parser, tagged values, stack buffers or scalar ownership machinery.

The parse_int/parse_float branches share the existing funded UTF-8 text factory
and callback transport. This removes unconditional CLR text decoding and duplicate
raw-hook preparation. Immutable full-token storage may be reused by that existing
factory; retained text stays conservatively charged. Numeric scratch, admission
before limb parsing, fresh magnitude ownership, hooks/raw spellings, signed zero,
overflow, lexer boundaries, errors, fuel and cancellation remain.

The unchanged scripts.json-summary.large reads 4,096 records / 110,592 UTF-8 bytes,
filters negative amounts, aggregates by kind, writes exact JSON and prints the
accepted count. Both roles pass independent stdout/file goldens at all three
existing sizes: [baseline](baseline-correctness.json),
[candidate](candidate-correctness.json). Compiled code is reused with fresh state.

Timers include disk fixture reset/staging, imports, the complete parser/filter/
aggregation/output job, exact output checks, capture/projection, cheap goldens and
prior-result cleanup. Compilation, process startup, transport and full hashing
are excluded. This is warm Run against ordinary warm CPython, not compile-to-result.

Measured on dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs / 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 with
ordinary GIL/GC and -I -S. Normal Lython GC/tiering, cooperative allowance,
preflights, cancellation and managed stack admission remain enabled; fuel unset.
CPython has no equivalent in-process containment. This is unqualified timing.

[Raw timing](warm.json) preserves all attempts and binary identities;
[declaration](comparison-declaration.json) freezes two source differences.
[Cleanup/source proof](cleanup-source-proof.json) verifies identical shared
comparison/catalog/Python sources, seven exited workers, seven removed file
fixtures, inactive services and the available exclusive VM lease. The owned timing
command finished in 9.626 s including cleanup.

[Validation](local-validation.json): 2,569 ordinary white-box, 326 focused public
JSON/file-worker and 320 governed/representative/shared-consumer tests pass;
groups overlap, all below 30 s. Eighteen native controls pass before and after,
including exact magnitude/text denial demands before parsing or hook invocation,
long representation, negative-zero bits and overflow. Twenty-three public additions
include eight independent [CPython programs](cpython-goldens.json), nine real
host hook success/cancellation cases through loads/decode/raw_decode, retained raw
text through another execution, and eight host-driven 64-record integer/float
batches per API returning allowance to baseline under 1 MiB. The native harness's
nullable return annotation was corrected after a failed build; no test ran against
that failed build. No timeout, budget or containment assertions were relaxed;
no new Stress run or general parser redesign is claimed.
