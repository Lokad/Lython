# Direct UTF-8 JSON tokens — 2026-10-16

One unchanged complete JSON-lines aggregation job, warm synchronous execution:

| Engine | Revision/version | Median | IQR / median |
| --- | --- | ---: | ---: |
| Lython baseline | f3e05ad96d5c4aad61f208c38db621b777a4ee41 | 10.250 ms | 17.2% |
| Lython candidate | b85fdde92aae2b300863a7f9c84350ce233685d5 | 10.548 ms | 3.9% |
| CPython | 3.13.16 | 4.651 ms | 0.5% |

The candidate median is 2.9% higher, within baseline spread. **No improvement or
slowdown is established.** This is one short diagnostic, with no repeat, retuning,
new producer or long lane. There is no qualified ratio, confidence interval or
host-latency qualification. The change removes a demonstrated intermediate
representation; this timing does not establish its effect on the complete job.

One production file changes. Well-formed unescaped string values and keys copy
validated token bytes into the existing governed UTF-8 factory. This removes the
temporary CLR string and UTF-8 re-encoding; the single-use ConvertJsonReaderString
helper is deleted. BCL Utf8.IsValid preserves rejection of malformed internally
supplied UTF-8. Escaped/invalid payloads retain existing backend decoding/error
handling. Lenient-control rewriting, token scratch and replacement overlap remain.
No second parser, owner, cache, configuration or runtime dependency is introduced.
The code stays managed and memory-safe; no unsafe/native APIs are added.

Owned output copies are admitted before allocation beside the unchanged 4x token
scratch. Returned strings never borrow the document array; empty identity remains.
Callbacks, exact offsets, cancellation and retained key/value funding remain.
Bytes input is still an explicit json.loads implementation limitation and is not
changed by this string-token path.

The unchanged scripts.json-summary.large has 4,096 records / 110,592 UTF-8 input
bytes. It reads a JSON-lines file, filters negative amounts, aggregates by kind,
writes an exact JSON report and prints the accepted count. Both roles pass
independent stdout/file goldens at all three existing sizes:
[baseline](baseline-correctness.json), [candidate](candidate-correctness.json).
Both reuse compiled code with fresh invocation state.

Timers include disk fixture reset/staging, imports, the complete parser/filter/
aggregation/output job, exact output checks, capture/projection, cheap goldens and
prior-result cleanup. Compilation, startup, transport and full hashing are
excluded. This is warm Run versus ordinary warm CPython, not compile-to-result.

Measured on the dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD
EPYC 9V45 vCPUs / 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16
with ordinary GIL/GC and -I -S. Normal Lython GC/tiering, cooperative allowance,
preflights, cancellation and managed stack admission remain enabled; fuel unset.
CPython has no equivalent in-process containment.

[Raw timing](warm.json) retains all attempts and role-specific binary identities;
[declaration](comparison-declaration.json) freezes the one source difference.
[Cleanup/source proof](cleanup-source-proof.json) verifies identical comparison/
catalog/Python sources, seven exited workers, seven removed file fixtures,
inactive services and an available exclusive VM lease. The owned timing command
finished in 10.076 s including cleanup.

[Validation](local-validation.json): 2,551 ordinary white-box, 303 focused public
JSON/file-worker and 320 governed/representative/shared-consumer cases pass;
groups overlap, each below 30 s. Fourteen native storage controls pass before and
after: malformed UTF-8 rejection, independent decoded copies, empty identity and
exact output-storage refusal beside scratch. Twenty-three public additions include
nine independent [CPython programs](cpython-goldens.json), eight real hook host
success/cancellation cases, retained aliases across another run and eight host-driven
64-record batches per API returning allowance to baseline under 1 MiB. No timeout,
containment or admission assertion was relaxed; no new Stress run is claimed.
Recursive async parsing, escaped-string conversion and bytes-input support remain
outside this bounded change. No broader parser redesign is claimed.
