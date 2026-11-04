# B224 bounded comparison, before observing timings

Compare ee8020f0 /38c03e5e to the final tested source snapshot. Nine existing
complete jobs: text compile/fresh/warm; CSV fresh/warm; report compile/fresh/async;
config warm control. Three isolated images with two unchanged old roles; shared
CoreLib/GC/hashes, ordinary GC/tiering, no overrides. Six-second guest warmup/image,
two-second settle, nine rotated batches targeting200ms. Entire owned service330s.
No CPython speedup claims from this source pair. Independently validate full
CPython output/file goldens with the existing reference checker.
Both images use the same previously frozen benchmark/adapter auxiliaries, verified
by SHA before timing. The candidate changes only the core DLL. A pre-upload gate
initially compared unused newly rebuilt auxiliaries and stopped because their
assembly identities changed; correcting it does not change any timed host logic.

Hypothesis: remove repeated signature partition arrays/name arrays/dictionaries,
and one per-function plan object. Compiler reuses its existing names/map, adds only
compact counts; defaults remain isolated/mutable. Conservative320B function charge.
No predicted threshold. Separate allocation reduction from stable elapsed change.
Retain adverse/control-limited observations; one bounded refinement only with new
concrete evidence, otherwise explicit delivery/debt or withdrawal decision.

Compile includes Compile, validity and compiled Dispose. Fresh adds Run, actual
mediated RAM file I/O, full output/file checks, result cleanup and compiled Dispose.
Warm/async include invocation, completed mediated I/O, checks and result cleanup;
async is completed I/O, not pending-host latency. Fixture reset/staging/startup/
hash-witness/final cached teardown excluded; all users/fixtures joined. Allocation
is process-wide and includes reset/background noise. Initial untimed warm setup
peaks are witnesses only, not fresh compilation overlap, RSS or embedding latency.
Preserve source/binary identity, baselines/final zero, quiet gates and raw evidence
in persistent storage outside RAM-backed guest fixtures; no sole /dev/shm evidence.
