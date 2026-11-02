# Shared formatted output — B120

2026-11-02; parent17511dbf/runtime93ee4513 versus the exact recorded source patch
and loaded core hashes. Retained implementation, focused-qualified; full delivery
is batched at this boundary. No public API change or publication.

Multipart f-strings now write into one governed UTF-8 builder. Native padded
fields no longer create intermediate PyStrings to push and join. The builder
uses the existing operand-stack temporary cleanup directly, with no owner/wrapper
class. ExecutableJoinFormattedParts, LoweredFormattedStringBuilder and CLR
padding repeat/concatenation are removed. Real lowered/syntax callers share append
formatting and finally release. Numeric/complex digit renderers and actual custom
hook results remain inputs, preserving the existing Python grammar and hooks.

## Complete-script results

Quiet Azure F4as_v7 Ubuntu24.04/.NET11RC3, normal tiering/workstation GC. Two
blocks, independent baseline/candidate/identical-control processes, four-second
warmup per image, nine interleaved approximately200ms batches. Every source/input/
stdout/file identity and joined fixture/worker cleanup passes. Source-pair timing
is separate from CPython and from the allocation diagnostic below.

| Complete script / lane | Candidate elapsed, blocks0/1 | Candidate process CPU | Unchanged elapsed / CPU control |
| --- | ---: | ---: | --- |
| JSON-to-Markdown / warm |−7.97% /−9.12%|−9.67% /−7.53%|+0.42%/+3.19%; −0.86%/−0.57%|
| JSON-to-Markdown / compile-run |−8.73% /−8.08%|−9.09% /−16.50%|−0.07%/+19.59%; +2.52%/−13.71%|
| Same report, explicit format calls / warm |−1.68% /−0.79%|−4.27% /−0.82%|−1.18%/+1.58%; −1.07%/−2.34%|
| CSV/helpers / warm |+0.42% /+1.01%|−0.96% /−1.34%|+0.14%/−0.65%; +0.27%/−2.01%|
| Text/helpers / warm |+1.71% /−5.43%|+4.99% /−7.57%|+1.88%/+5.38%; −4.07%/−8.21%|
| JSON-lines/helpers / warm |+1.35% /−1.03%|+0.64% /−9.00%|+1.33%/+0.64%; −1.65%/−8.62%|

The f-string report is about8.3% faster elapsed than its unchanged control in
both blocks, with warm CPU about7–12% lower relative to controls. Compile-run
elapsed also improves; its CPU controls are unstable, so there is no precise
fresh CPU claim. Explicit-format report is effectively flat/inconclusive, and
other scripts track their controls. Keep all adverse and unstable observations.
No universal speedup or claim closing older CSV/Unicode debt.

CPU is external Linux TGID user+system ticks through response, weighted across
nine batches; runtime/JIT/response included, not guest-only CPU. Warm timing
includes staging/reset, actual mediated file I/O, full output/file checks and
previous-result cleanup. Compile-run also includes compilation/disposal. Last
result/final compiled/process/fixture cleanup is outside batches and joined.
Startup is excluded. Independent phase medians are not additive.

## Allocation and admission

Separately reuse the existing complete-job allocation driver:192 warmups per
image,9x32 complete warm reports, isolated Lython images sharing CoreLib, precise
process-wide allocation counter covering worker threads. Hash witnesses, complete
goldens, owner balances and final cleanup pass. Shared-runtime elapsed/CPU clocks
remain diagnostic; use independent processes above for the timing decision.

| Complete report | Before allocation/run | Candidate | Change | Unchanged control |
| --- | ---: | ---: | ---: | ---: |
| F-string |11,950,003 B|10,245,770 B|−14.26%|−0.0008%|
| Explicit format calls |14,662,222 B|14,506,584 B|−1.06%|−0.0011%|

Private output capacity/replacement overlap is admitted before writes, and input
aliases remain charged. The builder survives real asynchronous hooks and yields;
unwind/close releases it after users finish. Surviving results retain their owner.
Prefix/window admission can fail before later field callbacks, an intentional
conservative threshold change. First admission refusal remains terminal. No
tracing, forced GC, weak ledger, replacement allowance, quota or unsafe code.

## Validation and preserved failures

Focused Release113 internal/676 public tests pass on both actual.NET11/.NET10,
then the complete19-case formatted-string followup passes on each, including
new generator close/throw cleanup. Old semantic/denial assertions remain.
Three independently captured CPython3.13.2 vectors exercise unchanged single-field
identity, Unicode fill/sign/grouping/complex values and real class-body lowering.
Multipart retained-result/refusal cases cover both hosting APIs; existing pending
conversion/specifier/format cancellation tests are unchanged. No timeout or exact
checkpoint-count relaxation. Removed the stale final-join test name, keeping its
target/cleanup assertions.

An initial C# local-name collision is preserved in first-focused.log. The private
collector then used unsupported lane name fresh instead of compile-run, failing
after its first complete warm observation. Initial source/journal/partial data
remain; the corrected supervisor reuses that exact warm prefix and executes only
the eleven missing jobs, with prefix hash recorded. There was no runtime fallback
or remeasurement of that warm point. Initial service20.786s/exit1, completion
209.489s/exit0, allocation40.789s/exit0. Owned units inactive and VM lease free.

[Timing assessment](assessment.json), [allocation assessment](allocation-assessment.json),
[prospective boundary](measurement-boundary.md), [identities](identity.json),
compressed raw initial/completed/allocation observations and catalogs, original
and corrected collectors, allocation-driver source, independent goldens, focused
logs and compressed source patch preserve all conditions. Decompress observations
into the named retrieved directories to rerun the unchanged analyzers.
Existing B115 CPython points describe their named source; this source-pair result
does not refresh them. Full platform/stress/installed-consumer qualification is
separate from these focused checks. Publication remains postponed.
