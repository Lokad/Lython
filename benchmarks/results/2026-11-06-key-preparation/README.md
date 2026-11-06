# One contextual key-preparation walk â€” 2026-11-06

Retain Q01: remove recursive tuple lookahead and separate contextual
classification/hash sweeps. One semantic walk returns normalized hash plus the
equality-dispatch requirement. Dictionary lookup/store/removal and set operations
use it. Known-hash copies retain their original callback-free path. No new cache,
key representation, query plan, public option or memory infrastructure.

Source62d2c9ca/F05 runtime1134d6e8 plus the [recorded patch/source hashes](observations.tgz),
frozen candidate1507216f versus F05/core520b3eb0. Both are Release before source
commit; assembly metadata describes their build base, not the complete source.
[Contract](contract.md), [identity](identity.json), [validation](validation.json).

## Correctness and deletion

Independent probes demonstrate a Python bug: for `(Key(), [])`, dict store/get/
contains and set.add previously rejected the later list without invoking the
earlier hash. An earlier ValueError was replaced by that later TypeError. Public
hash(tuple) was already correct. Remove ValidateTupleKey, ValidateKeyShape
indirection, QueryHash's separate classification input and the now-orphaned
contextual slice hash helper. Coarse top-level shape preflights remain where
construction needs them; nested hashability belongs to actual left-to-right work.

Nine new Python order controls include nested tuples, slices, namedtuples and an
earlier callback disabling a later hash slot. All21 family examples agree with
independently run CPython3.13 through both APIs. Four added callback-refusal/
cancellation cases stop before later unhashability, preserve the container and
first origin, clear active traversal/frame state, and return funding on cleanup.
Stored-hash copies, numeric compatibility, reentrant equality and managed-depth
coverage remain. [Goldens](independent-goldens.json), [before/after and build/check
history](correctness-history.tgz). Initial build local-name collisions and a
private golden reader's JSON casing error were corrected; no assertions relaxed.

Focused Release454 internal/1220 public per actual.NET11RC3/.NET10.0.8 pass,
plus22 hash/ownership/depth cases after adding the new refusal/cancellation variants.
Committed/pushed bf741ec2: full local Release4,214 internal/16,101 public per
actual runtime and both installed Release consumers pass. Platform/stress delivery
42166983759 is GREEN/all eight on exact bf741ec2, [terminal receipt](delivery-ci.json).
This excludes the later R02 prototype; never query the terminal run again.

## B325 complete scripts

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3/CPython3.13.16; unchanged b94 worker/protocol2,
normalGC/tiering, five guest warmup seconds/role and seven rotated150ms rounds.
Two identical candidate processes, CPython and unchanged F05 reference.112.65s/four
jobs; full goldens, quiet endpoints, Closed/final funding, joins and fixture
cleanup all pass. [Raw observations](observations.tgz), [summary](summary.json).

| Warm script | Candidate / identical control | F05 reference | Changes versus F05 |
| --- | ---: | ---: | ---: |
| Complete compound-key aggregation, width2 |3.845 /3.845 ms|4.550 ms|-15.50% /-15.49%|
| Complete compound-key aggregation, width8 |7.230 /7.230 ms|9.093 ms|-20.48% /-20.48%|
| Primary text summary |2.511 /2.333 ms|2.302 ms|+9.09% /+1.36%, inconclusive|
| Primary CSV summary |4.700 /4.680 ms|4.692 ms|+0.15% /-0.26%, roughly flat|

Compound-key identical-role spread below0.015%; IQR0.37-1.44% for Lython.
These affected-script elapsed improvements are measured. They do not eliminate
the CPython gap: width2/8 remain8.43/7.87x CPython in this diagnostic comparison.
No allocation or broad whole-script CPU improvement is claimed.

B325 text's identical candidate binaries differ7.09% despite low within-process
IQR. This establishes uncertainty, not a source regression or failed design.
Preserve the original adverse result and run one predeclared unchanged-source
confirmation with8warmup seconds/role, nine150ms rounds, two complete-text jobs
and reversed startup roles in the second. No runtime refinement or sample removal.

## B326 bounded text confirmation

85.62s/two jobs/all quiet, golden, funding, cleanup and join gates pass.
[Predeclared contract](text-confirmation/contract.md), [all observations](text-confirmation/observations.tgz),
[summary](text-confirmation/summary.json).

| Warm text block | Candidate / identical control | F05 reference | Changes versus F05 |
| --- | ---: | ---: | ---: |
| Original startup order |2.271 /2.329 ms|2.253 ms|+0.82% /+3.39%|
| Reversed startup order |2.307 /2.312 ms|2.307 ms|-0.03% /+0.20%|

The9.09% adverse result is not reproduced. First block still has2.54% identical
role spread; second has0.22%. Retain Q01 for correctness, simpler traversal and
material aggregation gains; accept possible modest text regression/variation as
explicit debt. No further text patch is justified by these observations alone.
A later coherent source checkpoint can revisit it; no permanent closure rule.

Execution clocks include mediated RAM-file I/O where present, full golden/output
validation and prior-result cleanup. Compilation is excluded in these warm jobs;
startup, staging/reset, transport and final teardown are excluded. This is not a
new primary CPython checkpoint; the published four-script compile/warm/fresh
table remains tied to1134d6e8. Custom hash callbacks still require synchronous
completion, even under RunAsync; genuine suspension needs a separate continuation
and ownership milestone. Whole-script performance work remains active. Publication
and tagging are postponed.
