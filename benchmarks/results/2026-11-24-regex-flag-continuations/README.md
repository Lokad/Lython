# B1576 Await guest flag operations before regex work

Six fixtures reproduce a synchronous-host capability failure under
`RunAsync`. The module compiled-pattern truth check and dynamic flag
operators could call a guest method that reads a file. Those fixtures
now match CPython 3.13.2 in output and callback order. The same flag
sequence runs through existing augmented dispatch and awaits guest
callbacks. Exact flags retain completed resolution. `re.compile` uses
one shared callable definition, consistent with the other module methods.

Retained in `f5bbd0cc9`. Windows and Ubuntu each pass 584 focused
.NET 11 Release checks. A held-close fixture confirms that cancellation
waits for the file user and its cleanup. A distinct refusal fixture
proves terminal failure, owner refund and reuse of the same allowance.
The original six failures are preserved in the procedures.

| Complete script | Warm elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Compiled-pattern log scan | +0.52 to +2.53% | +0.51 to +2.86% | 0.31 / 1.69% |
| Saved bound-method log scan | +1.18 to +1.74% | +0.86 to +2.87% | 0.03 / 0.52% |
| Module-function log scan | −1.88 to −0.83% | −2.00 to −0.90% | 1.04 / 0.03% |
| Quoted invoice | −0.27 to +2.01% | −0.69 to +3.31% | 1.86 / 0.42% |

The measured forms use ordinary flags. The comparison retains both
compiled-path regressions and the module-path gain. Admitted peaks match
and all allowances return to baseline. Local allocation differences are
within 56 B per invocation. The focused tests establish actual guest
suspension and cleanup. The timings describe the pinned adapter.

Frozen `531499f09` and `bd0f0ca93` match the reference runtime
`f17898c19` and the retained runtime `f5bbd0cc9`. Only the core
differs across the verified 32-file images. The quiet Azure F4as_v7
runs Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC.
Two workers per source warmed for eight seconds and alternated seven
samples each. Elapsed includes applicable mediated RAM-file I/O,
golden checks and prior-result disposal. Wider CPU includes request
and reset overhead. All 112 batches match CPython 3.13.16 goldens
with quiet gates, worker joins, fixture removal and archive verification.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures and regression evidence](procedures.tar.xz) · [Terminal status](terminal-status.json).
