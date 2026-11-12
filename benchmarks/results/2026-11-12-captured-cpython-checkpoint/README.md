# Captured-text and UTF8 report checkpoint — R282/B640

2026-11-12, committed **9bca1337** / frozen **e4cc65b2**, stock CPython **3.13.16**.
All 568 measured production sources match the commit with explicit newline
normalization. Full platform/stress/package qualification was pending at collection time and
subsequently [passes independently on that source](../2026-11-12-captured-slot-ownership/delivery/README.md) as CI43295336048; timing alone does not qualify a release. No new runtime/producer change or source-pair speedup claim.

| Complete script / phase | Lython | CPython | Lython / CPython | L / Python process spread; max IQR |
| --- | ---: | ---: | ---: | --- |
| Captured text / warm | 1.489–1.562 ms | 0.896–0.911 ms | 1.63–1.74× | 4.92/1.63%;3.74% — variable |
| Captured text / compilation | 45.02–45.51 µs | 54.44–54.52 µs | 0.83–0.84× | 1.10/0.16%;1.74% — diagnostic |
| Captured text / compilation + run | 1.497–1.582 ms | 1.030–1.043 ms | 1.44–1.54× | 5.36/1.21%;2.33% — variable |
| UTF8 report / warm | 3.035–3.074 ms | 2.303–2.338 ms | 1.30–1.33× | 1.28/1.50%;4.69% — variable |
| UTF8 report / compilation + run | 3.215–3.349 ms | 2.599–2.610 ms | 1.23–1.29× | 3.99/0.42%;6.44% — variable |

Ratio above one means Lython is slower. Ranges span two independent process
medians per engine; ratios include all four pairings, not confidence intervals.
The material captured-text/report gaps remain. Compilation is separate and does
not explain the captured-text warm gap. Moving controls and report IQR prevent a
precise current gain/debt conclusion. Original observations remain; no automatic
unchanged retiming is needed to decide that the broad performance goal is open.

Captured text:3,072lines/35,840B, helper plus live nonlocal counter. UTF8 report:
3,072JSON records/189,441B. Identical maintained sources/fixtures run on both engines;
these are not counter-only or JSON-only comparisons. Normal .NET11RC3 GC/tiering,
fixed fec79 worker/optional adapter0.4, isolated CPython3.13.16, quiet AzureF4as_v7,
Ubuntu24.04.4, four EPYCvCPUs/16GiB/no swap. Seven rotated250ms rounds,5s warmups
except12s captured-text. No modified GC/tiering or private execution quota.

Warm reuses compiled code. Compilation includes construction/disposal; compilation
+run includes both in a running host. Execution includes mediated RAM I/O, full
stdout/file validation and prior-result cleanup. Startup, fixture staging/reset,
protocol and final caches are outside the clocks. Lython checks capabilities/path
components and isolates per-run modules; CPython keeps imported modules and uses
builtin open. Separate phase medians need not sum. Pending I/O, RSS and embedding
tail latency remain unqualified. All rows use synchronous Run.

Five jobs finish238.674s under360sowner/30sstop/30smargin. Actual core/auxiliary/
CPython executable identities, all goldens, zero balances, before/after quiet,
Closed/exit0/join, fixture unmount and persistent/local archiveSHA gates pass.
Terminal cached; no remaining users or sole RAM evidence. Reuse the verified
existing image; no old evidence/images removed, OS240MiB/evidence12MiB floors
unchanged. Final free274,599,936B. All raw medians, controls, scripts, protocol,
source equivalence and preparation survive; prior guide is preserved separately.

[Summary](summary.json), [measured-source equivalence](source-proof.json),
[retained implementation/source pair](../2026-11-12-captured-slot-ownership/README.md).
Publication remains held; absent released-producer features do not block core work.
