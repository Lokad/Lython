# Native call attribution after the CSV checkpoint

Windows .NET 11 RC3 sampled the exact `fbef27903` core after the
nested bound call and borrowed CSV changes. The complete 32-file image
matched B1440. The fixture set included the nonlocal word count, quote-heavy
invoice, postponed factories and ordinary split. Factory first result used
a separate fresh worker. Four CPython goldens and all 359 traced requests
verified, and each worker and trace collector joined.

EventPipe exposes too little detail inside the hot native calls to choose a
new design. For example, the nearest resolved native entry accounts for
731 of 743 runtime-matched thread milliseconds on the word report and
1,116 of 1,121 on ordinary split. On the warmed factory, a single
attributed interval spans 189 milliseconds. This resolution does not
separate nested dispatch, inlined ownership checks and waiting. The
observations provide no isolated explanation for the split control or
the remaining factory gap. Use a more discriminating source count or
Linux sample before changing those paths.

Warm requests include mediated I/O where applicable, output checks and
previous-result disposal. The first-result requests also compile and
dispose the code owner. These traces provide no quiet-machine timing or
updated CPython ratio.

[Exact image and run summary](run-summary.json) ·
[Resolved stacks](attribution.json) ·
[Profiles and procedures](trace-and-procedures.tar.xz).
