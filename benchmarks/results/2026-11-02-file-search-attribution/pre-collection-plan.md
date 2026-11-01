# B79 - one current complete-file-search attribution

Source gate: current reader uses native UTF-8 line windows and direct startswith;
no missing whole-line UTF-16 conversion is assumed. Existing B29 phases for this
script are historical/unqualified; no current actual CPU capture covers its I/O,
1025 small helper calls and early exit. Reuse existing drivers, not a new framework.

Freeze current installed-correction core4cae38f4 and ordinary adapter0.4.0, exact
hashes in identity.json. One unchanged scripts.file-search.large:42,010input bytes,
1024 ordinary lines then chosen Unicode line, remaining trailing data unconsumed.
Pinned isolated CPython3.13.16 verifies full stdout/output-file goldens, no timing.

One owned24s VM service, lease held, no runtime/GC/tiering changes except external
PerfMapEnabled=1. Before/after quiet gates required. Success-only existing reflected
phases/public warm/fresh:128 warmups and16 observations per lane, actual host I/O,
full checks, owner/result cleanup. Compilation nested in fresh, recorded separately;
no subtracting medians or CPU-fraction inference. Reset/verification outside these
phase clocks; startup not measured.

One actual CPU capture of unmodified public warm jobs:128 warmups,2s settle,
3s monotonic single-job requests, <=4s total capture. perf cpu-clock/fp at199Hz;
actual named guest thread and all other thread deltas. Full stdout/file checks,
normal quit/final budget0, joined worker/profiler and fixture removal. Save original
maps/jitdump after users stop. No source variant, new candidate or latency claim.

If a gate fails, preserve results, stop without retry/retiming. If host I/O or
unattributed work dominates, or no substantial removable runtime mechanism is
identified, close without local tweaks. A runtime redesign requires separately
frozen semantic/admission/cancellation/lifetime and before/candidate/control gates.
No unchanged full platform/package/adapter requalification; no GitHub polling.
