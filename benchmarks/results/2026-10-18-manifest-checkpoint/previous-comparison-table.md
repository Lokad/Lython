# Previous leading comparison table

Preserved byte-for-byte from BENCHMARKS.md at `3ea431fd`, before the
2026-10-18 manifest checkpoint. These rows retain their original boundaries,
revisions and uncertainty; none describes a later HEAD.

| Workload | Input size | Lython time | CPython time | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Complete text summary with annotated helper [CG], fresh compile/run | 3,072 lines / 35,840 UTF-8 bytes | 7.354–7.451 ms | 1.527 ms | 4.818–4.881× diagnostic | Immutable `4acd532f`; identical controls; 16.27%/3.41% Lython spreads, 1.96% CPython; unqualified |
| Complete CSV summary with annotated helper [CE], fresh compile/run | 4,096 records / 57,361 UTF-8 bytes | 8.549 ms | 3.962 ms | 2.158× diagnostic | Immutable `34d4da35`; one capture, unqualified; 6.72%/0.63% spreads |
| Complete CSV summary with annotated helper [CF], warm `RunAsync` | 4,096 records / 57,361 UTF-8 bytes | 9.321 ms | 3.759 ms | 2.480× diagnostic | Immutable `34d4da35`; completed host operations, one capture, unqualified; 3.84%/0.59% spreads |
| JSON file round-trip [CD], warm `RunAsync` | One file / 42 UTF-8 input bytes | 1.571 ms | 0.16484 ms | — | Four frozen runtime files on 22648c5a; nominal 9.533×, unqualified; 7.24%/1.07% spreads |
| JSON configuration read/edit/write [CH], fresh compile/run | 16 existing paths / 181 UTF-8 input bytes | 2.584–2.769 ms | 1.399 ms | 1.847–1.980× diagnostic | Immutable `348998dc`; identical controls; 16.63%/9.79% Lython spreads, 8.93% CPython; unqualified |
| JSON configuration read/edit/write [AF], fresh compile/run | 16 existing paths / 181 UTF-8 input bytes | 2.817-3.142 ms | 1.572 ms | — | Two identical Lython controls differ by 11.5%; one diagnostic; no qualified ratio |
| Complete CSV summary [BP], fresh compile/run | 4,096 records / 57,361 UTF-8 bytes | 7.503 ms | 3.849 ms | — | Short diagnostic at27a61681; nominal8.3% below prior Lython,7.5%/6.4% spreads; inconclusive |
| Complete CSV helper with annotation-only local [BT], fresh compile/run | 4,096 records / 57,361 UTF-8 bytes | 19.745 ms | 17.014 ms | — | Local Windows, 3f799746 + one-line guard deletion; 48.9%/13.5% spreads, inconclusive |
| Complete text-summary file job [BR], fresh compile/run | 3,072 lines / 35,840 UTF-8 bytes | 14.994–16.539 ms | 11.188 ms | — | Local Windows diagnostic at 877f20e2; identical controls; 49%/107%/39% spreads, inconclusive |
| Complete JSON-lines helper [CI], fresh compile/run | 4,096 records / 110,592 UTF-8 input bytes | 9.466–9.607 ms | 4.441 ms | 2.132–2.163× diagnostic | Immutable `6584ddd7`; identical controls; 13.11%/3.52% Lython spreads, 0.36% CPython; unqualified |
| Complete JSON-lines aggregation [AU], fresh compile/run | 4,096 records / 110,592 UTF-8 bytes | 9.880–10.024 ms | 4.359 ms | — | Two identical Lython controls; unqualified (21.3%/8.7% spread); startup excluded |
| Complete JSON-lines aggregation with helper [CC], warm `RunAsync` | 4,096 records / 110,592 UTF-8 bytes | 10.854 ms | 4.283 ms | — | One frozen text-reader file on 56526237; nominal 2.534×, unqualified; 1.77%/1.06% spreads |
| Complete copy/checksum/manifest job [AD], warm | Two files / 4,107 input bytes | 6.080 ms | 0.4382 ms | — | Historical warm diagnostic; elapsed change within spread |
| File search with helper and early exit [P], fresh compile/run | 2,049 lines / 42,010 UTF-8 bytes; stop after line 1,025 | 6.041 ms | 0.395 ms | — | Fresh code per job; inconclusive (42.4% spread); startup excluded |
| Complete uppercase-report file job [AB], warm | 1,024 lines / 23,552 UTF-8 bytes | 2.651 ms | 0.2444 ms | — | Historical warm diagnostic; elapsed change within spread |
| In-memory JSON parse/edit/dump [BQ], warm `Run` | 1,024 records / 55,723 UTF-8 input bytes | 3.503 ms | 1.016 ms | — | Local Windows diagnostic at c9990b38; 4.6% higher median than earlier Lython; no elapsed benefit established |

Revision-specific sources and boundaries remain in the
[diagnostic evidence index](../../EXPERIMENTS.md#diagnostic-comparisons-by-revision)
and [BENCHMARKS.md](../../../BENCHMARKS.md).
