# Direct UTF-8 uppercase output

Ordinary `str.upper()` scalars now use managed `Rune.ToUpperInvariant` and write
directly into the existing governed UTF-8 builder. This removes ordinary per-scalar
CLR strings, string casing and short-string re-encoding. The existing full-case
expansion table is shared with title, capitalize and swapcase; it is not duplicated
or extended. Those legacy casing paths retain their behavior and allocations.
Lowercase and its contextual final-sigma handling remain unchanged.

Output keeps capacity/replacement preflights, final admission, ownership,
cooperative checks and scratch cleanup. No new runtime dependency, Unicode database,
builder, cache, unsafe code or native probing.

| Complete uppercase-report file job, warm synchronous execution | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| 1,024 input lines / 23,552 UTF-8 input and report bytes | 2.610 ms | 2.651 ms | 0.2444 ms |

**No elapsed-time benefit is established.** The observed 1.6% higher candidate
median is within baseline/candidate IQR-to-median 9.5%/10.8%; CPython spread is
1.0%. Keep the allocation removal without claiming a speedup, regression,
confidence interval or overall multiplier. One [diagnostic](warm.json) retains
all seven rotating batches, calibration and warmup. No retry, retuning or sample
replacement. The owned command took 8.845 seconds including cleanup under a
30-second bound.

The [ordinary script](../../scripts/text-uppercase.py) reads a bounded UTF-8 text
file, uppercases it, writes a complete report and prints its Python character
count. The fixture includes accented text, sharp-s and ligature expansions, and
supplementary characters. Expected output is independently specified. Both worker
timers include matched disk input/output staging, fresh invocation state,
execution/projection, exact report-byte checks and prior-result cleanup.
Compilation, process startup, transport and full response hashing are excluded.
Host operations complete synchronously for timing; yielding-host correctness is
checked separately. This is one bulk file job, not sustained streaming qualification.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 using
ordinary GC/GIL, `-I -S -X utf8` and explicit UTF-8 protocol I/O. Lython's normal
policies, allowance and GC/tiering remain. CPython has no equivalent containment.

Baseline is 29a1862ef5386c15c831fa03e623c87e22a120ba. The working candidate adds
one performance source frozen in [the declaration](comparison-declaration.json).
The new script and catalog entry are identical in both roles, with their own
hashes; timing/execution tooling is unchanged. [Baseline](baseline-correctness.json)
and [candidate](candidate-correctness.json) match independent stdout/report-byte
goldens at all three existing sizes. [Cleanup/source proof](cleanup-source-proof.json)
verifies seven workers and their fixtures cleaned, exact source differences,
identical shared script/catalog/tooling, inactive services and lease availability.

Eighteen [independent CPython 3.13.2 witnesses](cpython-goldens.json) cover the
existing Latin/Greek/Armenian/ligature expansion families through upper, title,
capitalize and swapcase. Both public APIs preserve shared-budget cleanup. The
same reference checks exact files at all three report sizes. The initial local
verifier assumed LF on captured Windows physical stdout; it now explicitly
normalizes universal newlines while preserving raw stdout bytes. Report bytes
remain exact. This is a verifier correction, not a changed Python expectation.

Ordinary white-box checks pass 2,230 cases in 11.541 seconds including cleanup.
Focused groups pass 36 white-box, 183 public compatibility/report cases, 34
comparison file-worker controls and 237 public governed/string-lifetime/CSV/text/
JSON/file checks. Groups overlap; each takes under 30 seconds.
[Validation](validation.json) preserves receipts. Allocation bounds cover ASCII
and mixed UTF-8 lower/upper without per-scalar CLR strings; fuel/cancellation,
source spans, UTF-8 growth/shrinkage, denial refunds and retained aliases pass.
No full stress or local full-public rerun; package/platform gates follow at delivery.

This closes ordinary uppercase scalar output. Legacy casing allocation work and
remaining JSON/aggregate/scalar/frontend work stay open.
