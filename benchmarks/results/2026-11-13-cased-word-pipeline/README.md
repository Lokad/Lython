# Cased-word pipeline prototype

R300 compares qualified runtime `d413392c` / core `d7b2ddaf` with an uncommitted
prototype / core `1fff156b`. The complete source snapshots and binaries are in
[local/source-checks.tar.xz](local/source-checks.tar.xz).

The hypothesis is that one bulk case writer plus plain UTF-8 ranges can replace
private per-word casing, the eight-byte private word buffer/cutoff and case-capacity
metadata in each range. Both eager splits and private iteration migrate together.
Existing execution-owned range loans, the canonical Unicode writer and ordinary
immutable string windows remain; no new pool or ownership controller is introduced.

The cost is one extra cased string per input line and an additional scan. Before
body effects, admission funds the cursor, all eventual word storage and one backing
header. Private native counting borrows final UTF-8; long observable words use
existing immutable windows. Their full cased backing remains conservatively funded
until command cleanup. Cursor retirement publishes relinquished owners before
refund callbacks. Unwind materialization requires neither new admission nor a long
copy. Eager splits retire the private cased source unless it is the whole result.

Release builds pass. Each actual .NET 11 and .NET 10 target passes 154 internal,
408 public and three affected original long-copy cancellation stress cases.
The long-word regression checks retained backing after cancellation/refusal and
cursor disposal. Four initial refund-reentry fixture failures depended on an exact
old reservation size; the corrected complete-word trigger preserves their binding,
first-refusal and cleanup assertions. Original failure logs are retained.

All seven existing complete scripts pass sync warm execution, async warm execution,
compilation and compile-to-result diagnostics, including golden outputs, zero final
balances and removed owned fixtures. Captured-text allocation increases
**29.13–29.16% warm** and **25.12–25.13% compile-to-result**. Desktop elapsed results
remain diagnostic: unchanged controls move substantially, including 46.66% in the
CSV fresh control. See [validation](local/validation.json) and every original
observation in the source/check archive. No CPU gain follows from these results.

B655's bounded quiet-VM comparison evaluates captured-text warm/fresh with CSV,
search and report controls. CPython is not part of this source-pair comparison;
the [source-labelled external guide](../../../docs/BENCHMARKS.md) remains the
external baseline. No prototype result is extrapolated to a published release.

## Controlled result and decision

B655 completes **266.593 seconds** on the existing quiet VM. Four independent
processes, seven rotated approximately 500 ms batches, normal GC/tiering and
12-second captured-text warmups retain the established comparison protocol.
All source/binary, output, balance, quiet-before/after, joined-worker and archived
fixture-removal gates pass. The terminal result is cached; no workers or RAM
fixtures remain.

| Complete script / phase | Elapsed change | Wider process CPU change | Reference / candidate control spread |
| --- | ---: | ---: | ---: |
| Captured text / warm | +10.41–16.23% | +10.75–16.33% | 3.22% / 1.84% |
| Captured text / compile-to-result | +11.82–14.30% | +10.85–14.84% | 1.07% / 1.12% |
| CSV validation / warm | −0.61–+2.08% | 0.00–+3.61% | 1.02% / 1.64% |
| File search / warm | −1.49–+1.25% | −0.47–+0.52% | 1.06% / 1.66% |
| UTF-8 report / warm | +1.11–4.20% | +0.70–5.60% | 1.95% / 1.09% |

Ranges span all four candidate/reference median pairings, not confidence intervals.
Process CPU includes work outside the inner elapsed clock; it is supporting
evidence, not guest-only attribution. CSV/search remain uncertain and the adverse
report control remains visible. These do not explain away the substantially larger,
consistent captured-text regression. [Full summary](b655/summary.json),
[original observations](b655/observations.tgz), [terminal proof](b655/terminal-status.json).

**Withdraw the complete runtime prototype.** A material execution/CPU regression
and increased allocation outweigh this particular consolidation. This is a design
decision based on controlled evidence and the added intermediate storage, not a
fixed percentage veto or permanent rejection of other cased-word designs. There
is no justified completion path for retaining this intermediate while eliminating
its new per-record work; do not leave dormant code or continue timing it unchanged.

All 571 production sources are restored exactly to qualified `d413392c`. The
independent long-word retained-storage regression and size-independent refund
reentry fixture remain; 34 affected cases pass per actual target after a forced
nonincremental Release solution build. A private binary-equality assertion was
invalid because generated version/source-link metadata include the later Git
revision; original successful build/test logs and the failed expectation are
preserved in [restoration proof](local/restoration.json) and
[restored checks](local/restored-checks.tar.xz). No new full qualification of
unchanged production source or speedup is claimed. Broader performance work remains
open.
