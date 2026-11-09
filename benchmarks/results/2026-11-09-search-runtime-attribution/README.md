# Current protected file-search attribution

R143/B501, 2026-11-09. Fresh nonincremental Release build from committed
`081052fa`, core `3f8df65a`; exact core and production source hashes retained.
The maintained `benchmarks/scripts/file-search.py` source and existing fixture/output
goldens are unchanged. Existing worker/adapter/dependencies are pinned by hash.
This is one current attribution capture, not an elapsed/source-pair/CPython comparison.

Source inspection rules out two presumed phases: the helper's string affix return
already borrows operands through the native return path without binding/frame
construction; text iteration already pulls through `IPyIteratorValue` and the native
cursor. Buffered reads complete inline until actual refill suspension. Ordinary
slot-local calls already bind into their final workspace. No replacement arena,
frame pool, iteration framework or affix-specific shortcut is selected.

One six-second wall capture after six seconds of execution warmup, normal GC/tiering,
only perf-map diagnostics, quiet F4as_v7/.NET 11 RC3. Recorded execution clocks total
4.842s; whole-batch sampling also includes fixture reset/protocol/validation.
Named guest worker uses 4.29 CPU seconds. All thread CPU, maps, unknowns and raw stacks
survive. 843 guest samples, 328 caller/other, 53 tiering; 41 guest unknown leaves (4.86%).

| Innermost observed guest category | Samples | Share |
| --- | ---: | ---: |
| VM prefix, including useful inlined work | 267 | 31.67% |
| Host/filesystem | 128 | 15.18% |
| Native call selection | 80 | 9.49% |
| Memory admission | 76 | 9.02% |
| Native return guarding | 66 | 7.83% |
| Native return evaluation | 58 | 6.88% |
| Scalar operations | 44 | 5.22% |
| Context / builtin initialization | 3 / 3 | 0.36% / 0.36% |

Guard leaves include 50 `GuardNativeReturn` and 16 `ReturnShape`; evaluation includes
real string-affix work. These are not removable-cost estimates. Prefix leaf 148
likewise includes useful dispatch/reads/publication. Caller samples mostly fixture
reset (230), host work (69) and protocol (27); do not attribute them to guest frames.

The bounded source gate/capture is complete. A justified next design gate examines
shared native-helper proof/execution, eliminating repeated invariant shape work or
duplicate representation across helper families. Preserve guard-before-effects,
live callable/field rebinding, fallback before work, actual cancellation/refusal,
managed stack and retained ownership. Do not add an affix-only path, new guard VM,
per-call cache/arena or second capture from these counts alone. A source proposal
must demonstrate a coherent deletion before implementation; allocation and CPU
claims require their own controlled complete-script comparison.

Source also confirms bulk casing already writes directly to final UTF-8 storage,
with one Unicode mapping pass and a byte-only capacity scan. The fused whitespace
split already writes final words without materializing a cased source; commitment
does not resize the host reservation. These are completed phases, not new targets.

All quiet/identity/golden/Closed/join/map-retirement/RAM-unmount/archive-under-lease/
local-SHA gates pass. Archive SHA256
`e1a26dde8470764396be8be7d2f44d072e929f3581cd18b1a3831cca4a0665b7`.
Terminal cached; no users/fixtures. No space recovery, floor changes, runtime edits
or extra qualification. Broader performance work remains open; publication postponed.

[Attribution and limitations](attribution.json), [raw samples/maps/core](observations.tgz),
[current source proof](core-source.json), [pinned auxiliaries](identity.json),
[selection/stopping criterion](selection.md), [terminal receipt](terminal-status.json),
[build/preparation](preparation.tar.xz).
