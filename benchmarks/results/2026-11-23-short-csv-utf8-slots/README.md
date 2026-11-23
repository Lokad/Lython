# B1520 consume short CSV fields before string publication

Retained `65d4207fb` keeps admitted short UTF-8 fields in existing
operands through exact builtin float conversion. Ordinary reads,
rebinding and aliases still publish the canonical Python string.
The explicit report's executable tape loses six loads across its three
float calls. Four control tapes match. The final refinement retains
direct publication of text fields in readers that already convert
unquoted fields to numbers.

| Workload and scope | Initial `bbc554798` | Call refinement `f943e1410` | Retained shape `0cc252af7` |
| --- | --- | --- | --- |
| Explicit-float warm elapsed | 8.58–11.79% less | 5.41–6.45% less | 4.43–6.22% less |
| Explicit-float warm CPU | 9.76–11.29% less | 2.68–5.74% less | 5.31–6.72% less |
| Explicit-float time to result | 7.36–8.24% less | 5.34–7.71% less | 6.14–8.31% less |
| Explicit-float time-to-result CPU | 6.25–10.33% less | 4.67–7.34% less | 6.48–9.09% less |
| Short-word report warm elapsed | 4.28–5.52% more | −1.68% to +4.39% | 0.37–1.39% less |
| Short-word report warm CPU | 4.17–5.59% more | −1.48% to +5.25% | 0.00–2.99% less |
| Typed Unicode warm CPU | −3.85% to +3.49% | 1.04–3.13% more | 0.00–3.88% more |

The retained typed Unicode elapsed interval is **0.11–4.27% higher**,
with 3.74% candidate-worker spread. Typed Unicode remains a control for
the next relevant checkpoint. Initial typed ASCII and quoted invoice
elapsed span zero. The word report has three existing forwarded unary
call sites. The call refinement sends only operands with actual deferred
text to the new float helper. Windows JIT shows interpreted dispatch at
12,698→12,688→11,970 bytes for the reference, initial and call refinement.
The native loop is 5,074 bytes in each. Host-level control timing
remains specific to these samples.

Windows diagnostics for the retained shape measure **1,966,998 →
1,221,296** managed bytes per explicit-float report, a **37.9%** reduction.
All five admitted peaks match and the shared budgets return to baseline.
The two typed readers match allocation exactly in the last comparison.
A separate Windows trace puts explicit-float string weights at 29.98%
on the initial variant, down from 58.51% on the earlier retained sample.
Trace weights are approximate allocation attribution.

The source pairs use the same quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3 on November 23, 2026 UTC. Each lane uses two workers
per image, eight seconds of warmup and seven measured batches per worker.
Warm elapsed includes mediated RAM-backed file I/O, verification and
disposal of the previous result. Time to result includes compilation and
code-owner disposal. Process CPU includes the wider request. All **392**
timed batches verify. Both quiet gates pass for each run, all workers join
and the persistent archives verify after removing the guest fixtures.

The retained revision passes 1,120 public and 334 white-box focused
Release checks on each platform. Both frozen images match five CPython
goldens. The hosted regressions cover failing short Unicode conversions,
signed zero, rebinding, alias identity, continued reading, budget reuse,
and asynchronous file work.

[Initial timing](summary.json) · [Call refinement](refined-summary.json) ·
[Retained timing](typed-reader-summary.json) ·
[Retained allocation](typed-reader-allocation.json) ·
[Retained identity](typed-reader-identity.json) ·
[Procedures and traces](procedures.tar.xz).
