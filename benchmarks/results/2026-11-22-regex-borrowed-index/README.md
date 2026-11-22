# B1328 Borrow an existing regex subject index

The private candidate borrows the original subject's existing UTF-8
scalar index for window bounds and capture projection. Borrowing adds no
index allocation, copy, owner or provider requirement. Indexed capture
projection avoids sorting capture endpoints.

Compared `318bf0e0a` with `da513d987` on November 22, 2026 UTC on the
quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Both images use the
optional PythonRe 0.4.0 adapter. Two processes per revision warmed for
at least twelve seconds and supplied seven rotated batches each. Ranges
cover all process-median pairings. Warm elapsed includes request output
verification and prior-result disposal. Process CPU covers the wider
request. Compilation, startup and final teardown are separate.

| Complete warm workload | Elapsed change | Process CPU change | Control spread reference / candidate | Maximum IQR |
| --- | ---: | ---: | ---: | ---: |
| Indexed bounded search | −10.52 to −8.89% | −10.00 to −8.57% | 0.78 / 1.04% | 1.21% |
| Indexed cursor with capture spans | −1.65 to +1.69% | −0.72 to +3.13% | 2.29 / 1.09% | 0.60% |
| Unindexed Unicode cursor | +2.08 to +3.59% | +2.21 to +4.52% | 0.56 / 0.91% | 0.80% |
| ASCII cursor | +1.10 to +4.53% | +1.83 to +3.32% | 1.64 / 1.73% | 1.04% |

Keep the full candidate private. The bounded-search improvement is
reproducible, while both cursor controls use more CPU. Preserve the
original observation and isolate one-shot bound lookup before further
promotion. Managed allocations are effectively flat. Accounted peaks
are unchanged except for 80 fewer bytes in the indexed cursor. All
four allowances return to baseline.

A separate Windows desktop diagnostic compared `5ce21de63` with
`f829925e3`. Bounded search took 22.33–37.11% less elapsed time with
about 14.7% reference-process spread. All three cursor ranges spanned
zero. The quieter campaign exposes adverse controls obscured by that
initial measurement.

Windows .NET 11 and Ubuntu .NET 10 each passed 67 white-box and 425
public focused Release checks. The added case exercises lookbehind
before the search window, bounded lookahead, unmatched groups, scalar
spans and the original subject after pattern-cache purge. Both complete
32-file images passed all four CPython goldens. All 112 timed batches
and both quiet gates passed. The persistent archive verified under
the VM lease after workers joined and RAM fixtures were removed.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Local timing](local-timing.json) · [Raw observations](observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz).
