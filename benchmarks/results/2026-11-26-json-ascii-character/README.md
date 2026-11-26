# B1790 Reuse one-byte JSON characters

The [recorded source patch](candidate.patch) on `e74c6ceaa` reuses Lython's
existing immutable ASCII character when a JSON string decodes to one byte.
The same lexer validates source, escapes and control bytes. For a single
ASCII escape it already has the scalar, so the decoder needs no second
fill. Existing record-key reuse still applies.

The 2,048-row service-total fixture has 802 one-byte string values.
Across the frozen source pair, per-run CLR allocation drops from 860,412 B
to 815,500 B, down 44,912 B or 5.22%. Admitted peak drops from 1,909,869 B
to 1,806,411 B, down 103,458 B or 5.42%. That matches 802 removed
129-byte string admissions. The other three fixtures have matching
admitted peaks. All compiler balances match and both owners restore
their allowance. [Allocation and admission](allocation-summary.json).

Both complete 32-file images match four independent CPython goldens.
Windows .NET 11 and Ubuntu .NET 10 each pass 223 white-box and 940 public
focused Release checks. The final identity, scalar position and projected
contents assertions pass in both modes. A focused recheck of the string
boundary tests and original subclass stress case passes on both targets.
Local CPython 3.13.2 independently reproduces the new regression's
values, identity and raw-decode end position.

## Initial quiet attempt

The first launch verified both images and completed the service-total sync
lane before the root filesystem ran out of space while saving the second
lane. The durable first lane has 28 batches and lower elapsed and CPU.
Its difference ranges are −2.34% to −1.46% elapsed and −1.64% to −0.95%
process CPU. The campaign ends before the final quiet check, so these
timings are diagnostic. The incomplete archive and disk failure remain
available. [Initial observations](initial/observations.tar.xz) ·
[Initial terminal status](initial/terminal-status.json).

## Reordered quiet comparison

The same 32-file source images and four independently verified CPython
goldens ran on November 26, 2026 UTC on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3. Four lanes warm two workers per source for five
seconds, then take seven rotated batches per worker with a 440 ms target.
Ranges span all pairings of worker medians. Negative values mean less time.
Warm execution includes mediated RAM-backed I/O, output verification and
prior-result disposal. Process CPU also covers request work and fixture
reset. Compilation, startup and final teardown stay outside these clocks.

| Script and hosting mode | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Unicode text control, sync | −11.54% to +7.15% | −11.45% to +6.08% |
| JSON service totals, async | −3.58% to −1.23% | −3.23% to +0.53% |
| JSON Lines helper, async | −9.32% to +2.44% | −10.04% to +2.40% |
| JSON service totals, sync | −4.77% to −0.99% | −4.86% to +0.32% |

Retain the reuse for the source-level allocation and admission reduction.
Service totals use less elapsed time across both modes in the completed
block. All CPU ranges cross zero. The Unicode and JSON Lines controls
have reference CPU spread of 16.86% and 11.30%, respectively. Treat the
timing magnitude as diagnostic. The escaped-Unicode report remains a
correctness and allocation control.

Staging removed verified duplicate VM worker DLLs and expanded records.
Complete local worker backups and the committed archives verified before
reclamation. The retry reused its existing images and started with
11,456,512 B free after staging. Both quiet checks and all 112 timed
batches pass. Workers join, fixtures are removed and the durable
observations archive verifies.

[Timing and spreads](summary.json) · [Frozen images](identity.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Checks, procedures and disk custody](procedures.tar.xz)
