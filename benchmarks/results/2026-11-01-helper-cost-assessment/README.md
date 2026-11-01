# Whole-script helper-call cost assessment (B58)

2026-11-01. One23.108s owned source-shape comparison finds a material CSV effect:
expanding the per-record helper expression reduces elapsed16.31%/17.88% against
both originals. Text13.95%/13.59% does not pass the declared15% gate. No runtime
change, accepted implementation speedup or removable CPU fraction is established.

## Question and controls

[Plan before collection](pre-collection-plan.md): retain helper definitions,
annotations, run setup, inputs and outputs. Replace only
`parse_amount(row['amount'])` with `int(row['amount'])`, or
`words_from_line(line)` with `line.lower().split()`. Four independent isolated
CPython3.13.2 source/output/file comparisons pass. [Reference validation](reference-validation.json).
These two known substitutions preserve these scripts, not arbitrary Python
inlining semantics. Code shape, name resolution and JIT behavior also differ.

Use the same frozen original core/host bits in all three images: production
95b5955c, worker frozen20e208c0. Current7050bf5b and frozen `src` tree hashes
match; no new production build/candidate or runtime overrides. [Source proof](source-identity.json).
Distinct Lython images share default CoreLib and actual key/word hashes; all
core/host/adapter files agree. Separate-image state and shared process GC/JIT
pressure limit interpretation; no context-unload or broad repeatability claim.

Dedicated Ubuntu24.04.4 Azure F4as_v7, four EPYC9V45 vCPUs/16GiB, .NET11RC3.
Ordinary GC/tiering;128 interleaved warmups/image/case,2s settle/case, seven
rotating32-job batches. Complete warm public runs include fixture reset, real
mediated I/O, full goldens and result disposal; startup/compilation excluded.
No phase subtraction, CPU attribution or new CPython ratio.

| Full warm job | Original ms | Expanded expression ms | Original control ms | Expanded vs both |
| --- | ---: | ---: | ---: | --- |
| CSV helpers /4,096 records | 8.18683 | 6.85193 | 8.34381 | -16.31% /-17.88% |
| Text helpers /3,072 lines | 6.53515 | 5.62380 | 6.50809 | -13.95% /-13.59% |

Original controls+1.92%/-0.41%, within3%; all relative IQR0.36–2.00%, all batches
>100ms. Every outlier retained. Quiet/hash/output/funding/fixture cleanup gates
pass. Service exits0 within45s; inactive/dead/success, MainPID0, empty group and
exclusive lease free. [Every observation and identity](summary.json).

## Source/lifetime decision

Ordinary calls already bind operands into final locals, including named calls;
no second argument package is demonstrated. The lexical context and interpreter
slot identity remain real state while nested calls, closures, callbacks and
suspension observe them. Current interpreter/slot-state fusion is a retained prior
storage simplification; separating it again without removing per-call control
objects would regress that design. Reusing a mutable interpreter would require
stable saved slot views and complete control/cleanup state, plus admitted capacity
through actual users and generator suspension. This assessment does not prove
that state or its cost can disappear, nor authorize a frame pool, shared-buffer
replay, universal tagged values or unchecked async shortcut.

Stop this assessment. CSV justifies further concrete call-state design work;
it does not justify assuming16–18% can be recovered. No retiming or new profile.
Independent current frontend correctness work can proceed while that design is
unproven; only final released regex-adapter qualification waits on a producer.

The [catalog/source forms](catalog.json), readable [driver](Program.cs)/[collector](collect.sh),
[logs](validation-logs.tar.gz) and [raw records](raw-results.tar.gz), SHA256
`00fe71f57ae82be37a19adc2616eb64030e9ae3b9a9df196792481c84936b278`,
preserve complete identities, source hashes, full output checks, machine gates,
all samples and cleanup. No production/test/benchmark-framework change.
