# B1606 Prepared default stores for direct definitions

The private candidate `bec00e82d` replaces the separate direct readers for
single definitions and pairs with one constructor. A compiler proof uses
spare instruction fields to link known default stores and bound arithmetic
ranges. The existing live default containers, preflight and Python source
spans are retained. The fallback path still runs the ordinary operands.

Two 32-file frozen images differ only in the core DLL. The reference
`d6437c26f` matches the runtime at `f6d06f638`. The quiet Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. After eight
seconds of request warmup per worker, each lane rotates seven batches per
worker, with two workers per source. Changes span all pairs of process
medians. Larger positive numbers mean more time. Warm time includes
applicable RAM-backed I/O, validation and prior-result disposal. Process
CPU also covers fixture reset and request framing.

| Workload and boundary | Elapsed change | Process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Captured rules, warm | −2.25 to +0.73% | −3.21 to +2.93% | 1.84 / 1.19% |
| Defaulted rules, warm | −6.00 to −3.53% | −5.01 to −3.97% | 1.42 / 1.19% |
| Eager annotated rules, warm | −0.76 to +3.93% | −2.00 to +2.24% | 1.45 / 3.22% |
| Postponed annotated rules, warm | −0.98 to +13.59% | +0.28 to +13.95% | 5.13 / 9.12% |
| Short Unicode report, warm | −5.70 to +4.28% | −3.10 to +6.56% | 7.25 / 3.10% |
| Quoted invoice, warm | −0.57 to +0.84% | 0.00 to +2.86% | 0.92 / 0.49% |
| Defaulted rules, compile to result | −1.41 to +0.48% | −1.89 to +0.86% | 0.81 / 1.10% |
| Defaulted rules, compile and dispose | −0.24 to +1.09% | −2.36 to +2.09% | 1.00 / 0.32% |

Defaulted warm elapsed and CPU improve. Compilation and compile to result
span zero. Startup stays outside all lanes. Local warm allocation is
effectively flat, admitted peaks match and all six compilations retain
the same bytes and return the allowance after disposal. The Windows
FullOpts constructor listing replaces 2,966 combined code bytes with
1,904. The shared `TryReturnDefinition` frame grows from 72 to 88 bytes.
That motivates removing the mutable cursor from the shared entry. The
postponed measurements leave the size and cause of the CPU change open.
Keep this first candidate private while testing the smaller shared entry.

Windows and Ubuntu each pass 246 focused Release checks. Six independent
programs match CPython 3.13.2 in both execution modes and images. All six
goldens, 224 timed batches, both quiet gates, worker joins and fixture
cleanup pass. The VM preflight recovers space from verified remote
replicas whose exact bytes remain in the tracked archives.

[Timings](summary.json) · [Image identity](identity.json) ·
[Runtime allocation](allocation-summary-six.json) ·
[Compiler ownership](compiler-owner-summary.json) · [Generated code](jit-summary.json) ·
[Raw observations](observations.tar.xz) · [Procedures and patch](procedures.tar.xz) ·
[Storage custody](storage-custody.json) · [Terminal status](terminal-status.json).
