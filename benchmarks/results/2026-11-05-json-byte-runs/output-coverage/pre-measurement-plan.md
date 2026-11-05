# D27 output-coverage qualification, unchanged completed source

2026-11-05, exact completed source eb1d8de3, original baseline cf3dc9cd/43fdf658.
B243 encoder-only162s conditions remain: report does not encode JSON; controls
vary and no encoder CPU claim follows. B244 completed reader/encoder161s all gates
pass: async report -1.1–1.7%, warm near flat, JSON -0.7–2.0% with1.3% baseline
spread, text/compile controls near flat. Allocation flat; no broad speedup.

Material remaining coverage gap: maintained JSON summary encodes only two keys.
Before retaining the encoder scan, qualify actual output using the EXISTING
canonical1024-row json.transform-roundtrip data (id/text='aλ😀'/active). Lift this
read/edit/write job into the existing mediated file fixture, without a new
benchmark API: json.load input, toggle active, write compact ensure_ascii JSON
through (1) one json.dumps/write or (2) json.dump streaming. Same independent
canonical output,1024-line count, full stdout/file checks. Retain text warm as
unchanged control. Label these output qualification jobs, not the maintained
CPython primary workloads. No long-string favorable fixture or output substitutions.

Three jobs, unchanged binaries, existing3isolated-image driver/two baseline roles,
6s guestwarmup/2ssettle/9rotated200ms batches, normal runtime. Owned guard150s,
anticipated~81s. Independent isolated CPython full goldens; exact binaries/quiet/
balances/normalclose/fixtures/users joined. Measure warm only here, no new fresh,
startup, backendCPU or externalratio claim. Preserve adverse results and both
original collections. This closes encoder coverage; stop with retain/debt/withdraw,
no automatic series of scan hints. Source changes would need their own justified
decision/validation. Full platform qualification only if source retained.
