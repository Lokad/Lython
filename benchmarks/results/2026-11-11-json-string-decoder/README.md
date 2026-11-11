# R217 — complete JSON string decoding

Retain the coherent decoder replacement. It removes **156 net runtime lines**:
the second string-token parser, lenient-control rewrite buffers, temporary unescaped
copies, token-local error remapping, orphaned JsonException catches and an always-null
inner-exception argument. Existing final UTF-8 storage, admission and record-key
sharing remain; there is no alternate parser, option, owner or cache.

The original-byte scan validates escapes and measures exact decoded capacity before
allocation; the writer fills the final admitted string. Plain strings retain direct
copy/key reuse. Scans and fills observe current cancellation in bounded windows;
the bulk UTF-8 validity primitive remains atomic. Input and retained aliases remain
funded, and unpublished storage refunds on failure. Memory estimates can improve:
the existing 5,000-control-character plus escape workload now completes within a
30,000-byte execution allowance and returns it to baseline; its compiled script
has a separate surviving owner. No cumulative allocation
counter or hidden allowance replaces live ownership.

String errors now use Python messages and original scalar coordinates. Independent
CPython 3.13.2 goldens expose the EOF distinction: four Unicode hex digits ending
at EOF still produce `Invalid \uXXXX escape`. Unpaired surrogates remain outside
Lython's string subset and now fail explicitly with ValueError instead of backend
RuntimeError messages. Supported values, hooks, raw_decode, key sharing and cleanup
remain covered. [Independent goldens](cpython-string-goldens.json),
[before](baseline-probes.json), [measured candidate](candidate-probes.json).

Both actual .NET 11/10 Release builds pass with zero warnings/errors; **193 internal
and 811 public JSON checks per target** pass after final cleanup. The original
scratch-dependent failures are retained; their before-allocation refusal,
first-origin, cancellation, alias and cleanup assertions now target final storage.
Two duplicate cancellation scenarios for the deleted scratch phase are consolidated.
[All focused outcomes, including initial failures](focused-checks.json).
Native error-coordinate walks also observe cancellation; the dedicated regression
rejects before payload admission. Full platform/stress/installed-package
qualification is pending in [delivery CI](https://github.com/Lokad/Lython/actions/runs/42928757271).

## Source and measurement boundaries

Fresh forced-build b8b05ccc/qualified69644713 source is the reference. Only the
two JSON runtime files differ in the measured prototype. The final removal of the
unused error parameter/usings and addition of error-coordinate cancellation follow
those measurements; the exact measured
patch, images and final source are retained separately. Timings therefore describe
that prototype, not an exact timing qualification of the final cleanup.
[Complete source/binary identity and limits](identity.json),
[measured source](measured-source.patch), [final source](final-source.patch).

The existing complete-job desktop driver uses five unchanged representative scripts
plus the identical report script with Python-default escaped Unicode input. Plain
report input is189,441bytes; escaped input201,729bytes; decoded records and complete
report output are identical. Sources, schemas and goldens are preserved. CLR
allocation is essentially flat for these short strings, many of whose former
temporary decodes already fit managed stack storage. Compilation allocation is flat.

Desktop clocks remain diagnostic. Original synchronous escaped report is
**+3.15..+5.80% adverse**; asynchronous is−2.38..−2.15%. Plain report/class-report
are mixed and unchanged compile CSV moves+24..30%. Preserve those results without
assigning their cause or treating them as a design veto.
[Sync](warm/summary.json), [async](warm-async/summary.json),
[compile](compile/summary.json), with every raw sample beside its summary.

## One bounded quiet confirmation

B565 runs three complete jobs in **111.530seconds** on the existing F4as_v7
Ubuntu24.04.4/.NET11RC3 VM. Four independent source roles,5s warmup each,
seven rotated400msrounds, normal GC/tiering, identical fixed fec79afd worker/host
and optional0.4 adapter. Full mediated RAM-file I/O, per-invocation output checks
and prior-result cleanup are included. Compile, reset, separate verification,
protocol/startup and final cache teardown are excluded. Wider process CPU also
includes surrounding work and coarse ticks.

| Complete workload | Elapsed change across four source pairs | Wider CPU change | Reference / candidate control spread; max IQR |
| --- | ---: | ---: | ---: |
| Plain JSON-to-Markdown report | −3.67..+6.56% | −6.73..−0.79% | 5.48 / 4.86%; 3.74% |
| Same report, escaped Unicode JSON | −1.85..−0.22% | −5.30..−3.91% | 0.66 / 0.99%; 2.13% |
| Ordinary text control | −3.44..+0.92% | −4.51..+1.24% | 4.45 / 0.06%; 0.76% |

Change is `(candidate / reference − 1) × 100`, across both roles on each source;
ranges are not confidence intervals. This does not establish a general CPU gain.
Escaped-target confirmation supports modest benefit, while plain/text elapsed
uncertainty and possible regression debt remain. Retain for the completed phase
deletion, Python-shaped diagnostics and direct storage; do not erase original
adversity or repeat unchanged timing without new evidence.

Every identity, golden, stable ownership balance, quiet endpoint, Closed/exit/join,
fixture removal and archive/SHA gate passes. All users ended; no VM workers or RAM
fixtures remain. [Summary](quiet/summary.json), [receipt](quiet/archive-receipt.json),
[cached terminal](quiet/terminal-status.json), [raw observations](quiet/observations.tgz).
Retired2f9 aliases and B563/B564 trace/archive duplicates were verified against
committed canonical bytes before removal. [Recovery](quiet/space-recovery.json)
distinguishes pre-extraction277,127,168bytes from post-extraction264,781,824bytes;
post-setup264,728,576 and final260,538,368 preserve the fixed240MiBfloor/12MiBheadroom.
Do not probe those retired paths again. The qualified0e image remains preserved.

[Preparation, exact binaries, original conditions and checks](preparation.tar.xz),
[archive identities](archive-identity.json). Core remains fully managed and regex
decoupled. No producer pin, publication or single-change CPython gain is claimed.

Full delivery is pending against committed7714144f (runtimee122) in
[CI42931921242](https://github.com/Lokad/Lython/actions/runs/42931921242).
The earlier dispatch42928757271 actually checked out already-qualifiedb8, so it
was superseded and cannot qualify this rewrite. [Correction](delivery-tracking.json),
[original wrong-source snapshot](mistargeted-ci-42928757271.json). Original
measurement identities retain their then-pending run labels; no raw clocks changed.
