# B141 native wildcard cursors; B142 translation correctness

2026-11-03. Implemented and focus-qualified; full delivery qualification pending.
Publication remains postponed. B141 removes whole-string rune materialization and
its cache/rollback. B142 fixes independent translation defects uncovered during
qualification; no translation speedup is claimed.

## Complete-script source-pair observations

| Workload / phase | Elapsed candidate vs baseline | Unchanged control | Allocation / control |
| --- | ---: | ---: | ---: |
| Unicode file-list filter, warm first | -48.056% | -1.776% | -84.940% / -0.0195% |
| Unicode file-list filter, warm second | -48.606% | +0.623% | same allocation diagnostic |
| ASCII file-list filter, warm | -10.836% | +0.075% | -43.562% / -0.00039% |
| Text helpers control, warm | +3.432% | +2.198% | -2.881% / +0.00064% |
| Report control, warm | -0.740% | -1.059% | -0.794% / -0.00005% |
| Unicode filter, compile-to-result | -49.491% | +1.176% | not measured |
| ASCII filter, compile-to-result | -13.571% | -0.120% | not measured |

Unicode warm weighted process CPU -76.14/-76.57%, controls +1.31/+1.59%; ASCII
-36.18%, control -1.11%. Fresh CPU controls move -11.55/-16.99%; preserve that
uncertainty rather than treating fresh CPU deltas as exact source effects. Text/
report timing does not establish CPU improvements. Their allocation reductions
are consistent with removing one reference field from every PyString; no admission
estimate was relaxed. Raw adverse/control observations remain in the assessments.

Each complete filter reads768 names from a mediated file (124,050 UTF-8 bytes
Unicode /74,898 ASCII), filters with a range/question/star pattern and writes all
256 selected names to a checked output file. Unicode names contain64 Greek
scalars; ASCII names have the same structure. Isolated CPython3.13.2 verifies
stdout and every output byte. These are new representative file-list workloads,
not isolated matcher loops. Canonical text/report scripts are unchanged controls.

**Retain:** a single UTF-8 matcher removes the only production use of GetRunes,
its per-string arrays, non-ASCII per-scalar PyString/backings and partial-build
rollback. ASCII arrays were recreated per match; class ranges converted rune
strings back into CLR strings. All that storage/conversion disappears. The
existing iterative star fallback and class grammar remain, using original byte
positions and scalar decoding. No second matcher, token program, cache, pool,
owner or work/time policy. Selected-name aliases and output collections keep their
existing admission/lifetimes. Offset tables have other consumers and remain.

Quiet Azure Standard_F4as_v7, Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering.
Four-second warmup/image, nine interleaved approximately200ms batches, seven jobs.
Before/after quiet gates and all identities/goldens/cleanup checks pass. Warm
excludes compile/startup; compile-to-result includes per-invocation compile/dispose.
Both include reset/staging, efficient mediated file I/O, full stdout/file checking
and prior-result disposal. Final results/programs/processes/fixtures join outside
the batch timer. Process CPU is Linux/proc TGID user+system through response,
including runtime/JIT/response, not guest-only CPU. Allocation is a separate
192-warmup/nine-by32 complete-Run diagnostic; its elapsed is not the timing verdict.
Owned service exited0 in179.037s, units inactive, workers/fixtures joined/removed,
shared VM lease free. No forced-GC performance profile or external ratio is claimed.

Timing describes the **frozen B141 patch before B142 translation fixes**:
baseline802fb318 core72acfe0c01eda35ebcfb9a94a1364c481c1d6b7ccfad8d7b3641f04cfe2e0438;
candidate83c18f392dac8683c9c7cdfc78b1e8f21b88a9b0821a726075f67a35eecb3e9f.
Source base dd81f26f; source.patch.gz is that exact matcher patch. Later B142 does
not run in these filter scripts, but is not represented by that binary timing.
combined-source.patch.gz records both changes. Final-source CPython comparisons
belong to the following coherent checkpoint. Previous B138 unique-name costs
remain documented separately; this work does not correct that algorithmic tradeoff.

## Correctness and contracts

Focused B141:29 internal/185 public per actual.NET11/10, plus18 streaming/Unicode
cases per target after additions (overlaps preserved in logs). The real in-flight
backtracking cancellation test keeps its original timeout/controller/cleanup;
architecture prewarming is removed. Matching with a completely filled allowance
uses only funded inputs, admits no scratch, and still observes cancellation.
Repeated20,000 non-ASCII matches and budget reuse remain external regressions.

The native54,832-case scalar/grammar matrix agrees with CPython. B142's translated
fnmatch matrix also agrees for54,832 cases and asserts match/fullmatch/native
agreement. A separate6,120-case glob matrix covers recursion, hidden names, empty
segments, multiple/astral separators, malformed classes and Unicode, agreeing with
CPython. Full reports are compressed; compact hashes/counts are preserved.

B142 confirmed an existing CLR ArgumentException for fnmatch.translate('[😀-🙏]'):
UTF-16 range iteration entered a low surrogate. It also found inconsistent newline/
end matching and glob's extra recursive slash, empty-star segment and surrogate-
separator splitting. Initial public failure reports remain. One Rune-aware class
translator replaces the two divergent implementations. Glob segment cases follow
CPython3.13's source (24d048d3), with streamed fragments and scalar-safe splitting.
It admits decoding/segment/output capacity and overlap before conversion, even
with an empty pattern and large separator text. Conservative working estimates
can deny earlier; there is no independent limit or fallback.

Generated regex text intentionally changes to Python-compatible **(?s:...)\Z**.
Use match/fullmatch for whole-name behavior; search can find a suffix as in Python.
Native matching/enumeration remains provider-free. Compiling a translated regex
still requires the supplied provider; integration uses the existing optional0.4.0
adapter, with no new dependency or producer-release requirement. Original semantic,
refusal, alias, host and cleanup assertions remain; exact output-format assertions
are updated to the compatible scoped/strict template. Two rune-cache-only tests
are deleted with their architecture. The independently useful offset-table funding
check remains; it no longer sits in a GC-sensitive collection because it performs
no process-wide collection or finalizer work.

Final focused checks:29 internal/203 public per actual target, plus16 new glob
goldens and the search/end semantic regression per target (all PASS). The final
UTF8-bound admission adjustment passes97 focused public cases per target.

Full Windows/Ubuntu, both actual targets, original Stress and installed consumers
will be recorded at the combined delivery boundary. Cached41554275552 covers the
previous802 runtime only; it is terminal and must not be queried/replayed.
