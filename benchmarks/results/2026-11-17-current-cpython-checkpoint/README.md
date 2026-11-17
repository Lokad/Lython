# Representative CPython checkpoint

R497/B919,2026-11-17 UTC. Committed0d2a6ab7/frozen8d26963c against stockCPython3.13.16,
quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3. Four existing trusted complete scripts,
three phases each;12jobs447.28seconds. Two independent processes per engine,
rotated6second full-request warmups/five rounds, natural GC/tiering. Same catalog
6a661e8e and command-v2 timing/protocol. No new comparison framework.

| Numeric CSV quality report | 4,096 records / 127,031 B | 4.066–4.166 ms | 4.574–4.693 ms | 0.87–0.91× | Diagnostic; controls L/C 2.46/2.60%; IQR≤1.47% |
| Text normalization | 4,096 lines / 52,224 B | 0.916–0.925 ms | 0.503–0.520 ms | 1.76–1.84× | Diagnostic; controls L/C 0.99/3.37%; IQR≤0.98% |
| JSON-lines aggregation with a helper | 4,096 records / 110,592 B | 3.548–3.613 ms | 4.077–4.102 ms | 0.86–0.89× | Diagnostic; controls L/C 1.85/0.62%; IQR≤0.39% |
| Floating JSON numeric report | 4,096 pairs / 56,495 B | 1.582–1.621 ms | 0.820–0.821 ms | 1.93–1.98× | Diagnostic; controls L/C 2.48/0.14%; IQR≤1.09% |

CSV fresh0.90–0.94×, text1.66–1.67×, JSON-lines0.87–0.88×, floating1.69–1.78×;
floating fresh Lython control4.78%limits magnitude. Compilation ratios1.26×/0.96×/
0.99–1.00×/0.92–0.95× respectively. Do not add phase medians or infer causal source
gains versus prior collections. Raw medians/CPU/IQRs/all controls stay visible.

Boundary: warm includes Run/mediated RAM I/O/goldens/previous-result cleanup; fresh
adds compile/compiled-owner disposal; compile includes compiled-owner disposal.
Startup/reset/protocol/final cache teardown excluded; external CPU is wider.
CPython native I/O does not supply Lython's containment. All actual32file image,
golden/quiet/Closed/exit/join gates pass; archive SHAverified while lease held.
Guest fixtures removed and no workers remain. No startup/tail-latency/general CPU
claim. The runtime's failed Windows stress fixture/qualification remains separate.

Initial local freeze assumed unchanged worker metadata, but a fresh committed build
changes informational revision data. Copy/verify every actual changed image file
instead. Staging then refused the254MiB floor before mutation or workers. A proposed
B875 raw path was already absent; read-only proof refused. Add an exact committed
B631raw replica after fresh idle lease/path/UID/hash/nlink/all-alias proof; keep
original floor/config/logs. Only exact canonical-backed superseded core/raw replicas
retired. Qualified reference/current new image/catalog/toolchains remain. No
measurement conditions or resource guards relaxed. Final free265,588,736B>252MiB.

Changed image files: Lokad.Lython.Benchmarks.dll, Lokad.Lython.dll, Lokad.Lython.Utf8Regex.dll. Exact2383source inputs/585production
preserved; full32file manifest and actual Ready reports in raw evidence. This does
not imply rebuilt-binary equality with CI. Publication postponed/objective active.

[Summary](summary.json),[raw](observations.tgz),[identity](identity.json),
[changed binaries](changed-image-files.tar.xz),[source](source-inputs.tar.xz),
[goldens](preflight.json),[custody](archive-receipt.json),[procedures](procedures.tar.xz),
[previous guide](prior-BENCHMARKS.md). Current user-facing account: [BENCHMARKS](../../../docs/BENCHMARKS.md).
