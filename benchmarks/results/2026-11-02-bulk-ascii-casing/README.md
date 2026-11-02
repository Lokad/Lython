# Bulk ASCII casing — B118

2026-11-02, baseline ffa3f6f8; source patch and exact loaded identities retained.
One bounded ASCII-prefix writer serves lower/upper/capitalize/title/swapcase in
the existing UTF-8 casing pipeline. Existing funded builder windows receive the
final bytes directly. ASCII bypasses per-scalar decoding, expansion/property
lookups and append/preflight calls. No per-character strings existed to remove.
Unicode mappings, original-subject sigma and Python3.13 compatibility stay intact.
No new builder API, owner, cache, backend, unsafe code or configuration.

## Measurements and decision

Both original and refined campaigns have twelve complete source-pair observations:
two blocks, six scripts, separate baseline/candidate/unchanged-control processes,
four-second warmup each, nine interleaved approximately200ms batches. Quiet
F4as_v7 Ubuntu24.04/.NET11RC3 VM; normal tiering/workstation GC. Full stdout/file
goldens, identities and joined cleanup pass. Original226.667s and refined250.219s
services exited0; units inactive and shared lease free before the next capture.

All deltas below are against each block's baseline; controls are identical source.
CPU is external Linux TGID user+system ticks through command/response, weighted
over nine batches. Runtime/JIT/response are included; this is not guest-only CPU.
No allocation measurement or universal whole-script speedup is claimed.

| Complete script | Initial CPU, blocks0/1 | Refined CPU | Refined unchanged CPU control | Refined elapsed |
| --- | ---: | ---: | ---: | ---: |
| CSV/helpers |+3.90% /−0.65%|+1.30% /−0.38%|+1.55% /−1.05%|+0.66% /+0.51%|
| Text/helpers |+1.66% /−2.10%|+1.68% /−5.18%|−4.38% /+2.78%|−0.38% /−3.41%|
| Helper report |−6.14% /−4.91%|−4.76% /−3.17%|+0.76% /+2.53%|−4.08% /−2.65%|
| Mixed-text uppercase file |−6.35% /+0.74%|−4.94% /−4.88%|+24.24% /−1.33%|+1.26% /−0.54%|
| ASCII uppercase file,45,056B |−39.14% /−38.70%|−37.14% /−36.86%|+19.32% /+0.76%|−0.32% /+0.35%|
| Unicode-only uppercase file,65,536B |+7.28% /+3.41%|+1.69% /+6.17%|−3.44% /+3.89%|+0.77% /−0.47%|

The helper report beats its elapsed controls by about4–5% in both refined blocks;
CPU about5.5%. ASCII-file process CPU is consistently lower, while file elapsed
is effectively unchanged. Some file controls are unstable: refined ASCII elapsed
control−8.95%/+1.42%, mixed-file−5.70%/−0.18%; these adverse comparisons stay visible.
Text is mixed, CSV approximately flat. Unicode-only CPU remains about2–5% above
its controls: explicitly accepted regression debt, not declared fixed.

Retain the shared bulk path for its substantial ASCII CPU reduction and repeated
complete helper-report benefit. The one source-backed refinement consolidates
cancellation at byte-window boundaries for both ASCII and Unicode, at most4096
input bytes plus one scalar; sigma context scans retain their existing checks.
Final publication and admission checks remain. It does not establish that all
Unicode regression disappeared. No more local retiming is required at this
boundary; later coherent redesigns can revisit this documented debt if justified.

Warm timing includes actual mediated I/O, fixture reset, full output/file checks
and previous-result cleanup. Last result/final compiled/process/fixture cleanup
is outside batch timing and joined. Compilation/startup are excluded. Existing
named B115 CPython comparisons remain separate; these medians do not refresh them.

## Correctness, evidence and qualification

248 internal and60 public focused Release tests pass on each actual.NET11/.NET10:
Unicode/context/expansion hashes, title/capitalize/swapcase boundaries, denial,
retained aliases, cancellation during output funding and cleanup/budget reuse.
Three additional independent CPython3.13.2 vectors cover all five methods, all
ASCII controls, chunk boundaries and Unicode transitions; old goldens unchanged.
One private outer Python display initially hit Windows CP1252 after generating
the hashes; UTF-8 display corrected it, without regenerating different expectations.
No timeout/assertion relaxation or exact-checkpoint tests.

[Initial data](initial/assessment.json), [refined data](refined/assessment.json),
[refinement rationale](refinement-plan.md), [catalog](catalog.json), exact images
in each identity, original/refined compressed raw observations and machine gates,
collector/analyzer sources, CPython goldens, focused logs and compressed source
snapshots/patch preserve the complete conditions. Decompress confirmation.json.gz
to retrieved/confirmation/confirmation.json to use the unchanged analyzer.

This source93ee4513 is FULLY QUALIFIED by
[cached delivery41497475403](delivery-ci-status.json): Windows/Ubuntu ordinary
.NET11/10, original stress both and installed Release consumers both PASS.
Never requery this terminal run. The proof does not qualify later formatted-output
changes. Publication remains postponed.
