# Typed synchronous JSON object keys

Both JSON object readers now call the existing string decoder through a typed,
synchronous key helper. This removes two generic value-reader awaits and their
impossible non-string-result failures. The decoder's return type now expresses
its existing PyString invariant. Value recursion and callbacks still use their
existing awaited path. There is no second parser, builder, mode or owner.

**No speedup is established.** Allocations are unchanged; the single short native
timing comparison has noisy controls. The change consolidates a known string-key
contract and removes unnecessary generic dispatch. External performance remains
documented in [BENCHMARKS.md](../../../BENCHMARKS.md).

| Native 64-call parse sample | Baseline median | Candidate median | Allocated bytes, both |
| --- | ---: | ---: | ---: |
| 32-entry string dictionary, synchronous | 2.786 ms | 2.25435 ms | 998,912 |
| 32-entry string dictionary, async | 2.47425 ms | 2.0514 ms | 998,912 |
| 32-string array control, synchronous | 1.32085 ms | 1.108 ms | 421,376 |
| 32-string array control, async | 1.50415 ms | 1.50375 ms | 421,376 |

Dictionary sample spreads range from 3.8% to 31.3%; array controls reach 587%.
Nominal elapsed differences cannot establish a causal gain or a whole-job trend.
There is no CPython, RSS or embedding-latency claim and no repeat campaign.

Measured 2026-10-18 UTC on an Intel Core i7-14700KF, Windows 10.0.26200 x64,
Release net10.0. Baseline is immutable
`35683fb4bf9aff1e5655dac0e035fd4f0fdb4fb3`; candidate is the frozen
[one-file patch](candidate.patch) on that revision. [Validation](validation.json)
records exact source, assembly and receipt hashes. These are not measurements
of arbitrary later HEADs.

The [temporary recipe](JsonKeyParseDiagnostic.cs.txt) prepares an admitted UTF-8
document, context and native json.loads callable before measurement. Each role
uses four warmups and eight samples of 64 calls. Both APIs must complete inline
on the same managed thread; the counter/timer includes native binding, parsing,
guards and count accumulation. Compilation, setup, public scheduling/startup,
actual suspension and owner disposal are excluded. No requested GC or altered
protections. Final fields/counts, frame/reservation unwind and shared-owner
refunds are checked. All [before](diagnostic-before.json),
[after](diagnostic-after.json) and [summary](comparison.json) observations remain.

All 18 [boundary records](boundary-records.json), captured before production
edits, match afterward, including every fuel cutoff, source span and hook effect.
All 734 distinct focused Release checks pass: JSON strings/keys, strict and
lenient controls, Unicode/errors, raw decode, repeated keys, pairs/object/number
hooks, actual suspension/cancellation, retained funding, terminal refusal, stack
admission and 58 complete CSV/text/JSON/file goldens. No permanent test was added
or changed; the diagnostic was removed before final builds. Original assertions,
timeouts and Stress scales remain.

The helper preserves the generic reader's checkpoint and frame admission/finally
leave. Existing string decoding, allocation preflights, key funding while values
or hooks suspend, error coordinates and Python semantics remain unchanged. No
public, accounting or regex/dependency contract changes. Explicit Release-package
and platform qualification are recorded separately at delivery; this precommit
artifact alone does not claim their completion.
