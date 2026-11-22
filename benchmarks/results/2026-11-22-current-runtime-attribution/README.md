# B1399 Postponed factory and word-count CPU attribution

Linux perf sampled the exact `70ea1083d` image retained at
`586a90c72`, on November 22, 2026 UTC. The quiet Azure F4as_v7
uses Ubuntu 24.04.4 and .NET 11 RC3. Each case and execution
boundary gets a dedicated worker with its managed JIT map. The
199 Hz profiles include all worker threads, verified output and
prior-result cleanup. The word report also uses mediated RAM-backed
file I/O. Warm and compile to result use independent processes.

| Complete workload | Samples | Native instruction loop | Callable construction | Nearest admission | Dictionary loop | Casing preparation |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Nonlocal word report, warm | 1,094 | 128 | 0 | 83 | 171 | 104 |
| Postponed factories, warm | 1,087 | 120 | 145 | 107 | 0 | 0 |
| Postponed factories, compile to result | 1,101 | 82 | 164 | 101 | 0 | 0 |

Columns use the nearest resolved runtime method and count partial
groups. They leave out other runtime and worker work. Inlining and
unresolved frames limit attribution. The counts cannot predict a
speedup from removing a method. This is a single-image profile.

Of the 107 warm factory reserve samples, 65 have callable
construction as their nearest resolved caller and 36 have default
preparation. The corresponding first-use counts are 67 and 28 of
101. Consider eliminating a complete default or callable storage
stage. The earlier singleton-default experiment already shows the
identity and observer tradeoff. For the word report, the 83 reserve
samples include 32 from creating a text window value, 30 from
casing preparation and 19 from copying UTF-8 text. The report
contains 6,144 short words over 3,072 lines. The current profile
supports looking at the prepared short-word loop and line-to-cased
storage boundary before changing long-word storage.

The prior Windows EventPipe capture on `32c50290d` verifies 185
batches. Its nearest runtime frame concentrates in native call
entry. The postponed factory trace includes an interval of about
245 ms with the same attribution. Keep those captures as limited
evidence alongside the higher-resolution Linux observations.

All 169 Linux profile batches matched their goldens. Both quiet
gates passed. Workers joined and all raw traces verified in the
persistent archive under the lease before guest fixture removal.

[Linux attribution](attribution.json) · [Windows attribution](windows-attribution.json) ·
[Raw Linux traces](observations.tgz) · [Profiles and procedures](procedures.tar.xz).
