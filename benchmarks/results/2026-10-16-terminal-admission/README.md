# Terminal memory admission — 2026-10-16

Refused memory admission now fails the command through guest exception handlers,
context-manager suppression and finally returns. The first refusal is retained
by the existing governor and checked at existing guest checkpoints/result
boundaries. Native cleanup remains available without publishing buffered writes
after refusal. Ordinary guest-raised MemoryError stays catchable; earlier side
effects, retained funding and later separate commands remain supported. The
optional regex-cache slot-admission fallback and six pure same-command recovery
fixtures were removed. See [MEMORY.md](../../../MEMORY.md) for the contract.

| Engine | Revision | Median | IQR / median |
| --- | --- | ---: | ---: |
| Lython before | 159cf7d4101785d2b07e4d238e3c0a306cdc796f | 9.713 ms | 7.5% |
| Lython after | ae37d1f93147651ead7bdaf276597672d2e9792a | 9.492 ms | 2.2% |
| CPython | 3.13.16 | 4.175 ms | 0.8% |

**No speed change established.** The candidate's 2.3% lower median is smaller
than the baseline spread. This is a correctness/containment correction, with no
performance benefit or qualified CPython multiplier claimed. Host tail latency
is unmeasured.

One existing `scripts.json-summary.large` warm diagnostic uses the unchanged
complete JSON-lines aggregation script: 4,096 records, 110,592 UTF-8 input bytes,
blank lines, a kind/amount dictionary aggregate, and exact stdout plus report.json
checks. Each invocation stages fresh files, performs host file operations and
publishes output; compilation and process startup are excluded. Independent
before/after correctness checks include empty, 16-block and 1,024-block fixtures.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC 9V45
vCPUs / 16 GiB RAM; .NET SDK 10.0.401/runtime 10.0.12. Normal memory/stack
protections, GC and tiering remain enabled; fuel unset. CPython uses normal GIL/GC
and `-I -S`, without equivalent in-process containment. Seven rounds use the
existing micro command. The one capture completed in 9.533 s including owned
command cleanup, within the 25 s remote service lifetime. No second capture,
retuning, additional lane, long lane or new producer.

[Raw samples](warm.json), [frozen inputs](comparison-declaration.json),
[catalog](catalog-micro.json), independent stdout/file checks
([before](baseline-correctness.json), [after](candidate-correctness.json)) and
[cleanup/source proof](cleanup-source-proof.json) preserve the evidence. Both sets
of all 524 production C# sources and the shared comparison/Python tooling match
the frozen archive-byte manifests; seven workers exited, services are inactive
and the exclusive lease is free.

[Initial preparation failure](initial-setup-error.txt): the general catalog
omitted representative file scripts, selecting zero cases. It stopped before
verification or timing. The completed baseline build was reused and the existing
representative profile selected; source archives remained unchanged and timing
ran once. The declaration records this correction.

Focused Release controls cover shared/local refusal, same-command suppression,
ordinary Python MemoryError, projection ownership, actual suspension, retained
results, and text/binary/gzip/ZIP buffered cleanup. All 2,617 ordinary white-box
controls and focused public groups pass; the 53 new command controls are part of
those groups. Each successful focused test process finishes under 30 s. An
oversized combined public group hit its existing 28 s bound and was split without
relaxing timeouts. Original stress scales remain separate; no new stress or host
latency qualification follows from this diagnostic.
