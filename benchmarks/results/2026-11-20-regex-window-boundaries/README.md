# Resolve regex windows from the nearest known boundary

Compares `cbec0f2eb` with `378742dcc`. Both static regex analysis
and runtime preparation compute original-subject byte bounds with
the same core routine. It finds the bound closest to either end and
uses that location when it helps find the second bound. Traversal
counts valid UTF-8 leading bytes. Reversed bounds retain their
original order. The active token is checked during both directions
and before the resolved window is returned. The routine allocates no
index and the provider still receives the original subject bytes.

Windows .NET 11 and Ubuntu .NET 10 each passed 90 focused white-box
and 578 focused public Release checks. One new test compares 324
mixed-width/clamped/reversed windows with independent rune offsets.
Both source images matched eight CPython 3.13 semantic probes in both
execution modes, including original-subject lookbehind near the end,
bounded empty matches and retained captures. Provider cancellation,
memory refusal and ownership coverage uses the existing suites. The
published 0.4.0 adapter pin stays in place.

## Local coordinate diagnostic

Windows .NET 11 RC3 on 2026-11-20 UTC. The subject is already encoded
and its scalar count is known. A standalone loop times only window
resolution. Tiered compilation is disabled in this diagnostic. Two
independent processes per image run seven samples after warm-up.
Ranges cover both process medians. Both images return identical ranges
and the timed loops allocate zero managed bytes.

| Subject and window | Reference | Candidate |
| --- | ---: | ---: |
| ASCII suffix, 32,768 scalars | 2.28–2.38 ns | 2.28–2.30 ns |
| Short mixed-width prefix, 16 scalars | 16.32–16.39 ns | 12.52–12.91 ns |
| Mixed-width prefix, 32,768 scalars | 37.59–38.57 ns | 23.91–24.23 ns |
| Mixed-width suffix, 32,768 scalars | 84.60–85.14 µs | 16.24–16.48 ns |
| Mixed-width middle, 32,768 scalars | 42.94–43.60 µs | 10.92–10.96 µs |
| Mixed-width bounds near opposite ends, 32,768 scalars | 82.52–88.42 µs | 24.70–25.36 ns |
| Mixed-width reversed suffix, 32,768 scalars | 84.01–86.04 µs | 16.10–16.27 ns |

These observations isolate Lython's coordinate preparation. Pattern
compilation, backend matching, input creation and capture projection
are outside the diagnostic. Published backend performance and
final adapter qualification remain separate.

[Raw timing and image identity](summary.json) · [Source and checks](source-and-checks.tar.xz).
