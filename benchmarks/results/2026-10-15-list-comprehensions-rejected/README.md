# List-comprehension lowering: rejected bounded prototypes

Two distinct executable-list producers passed their structural, Python scoping,
cleanup and containment checks, but neither demonstrated a performance benefit.
Both are removed from the runtime. Ordinary list comprehensions retain their
existing lowered evaluator; executable coverage remains unfinished.

| Producer | Existing Lython | Candidate | CPython 3.13.16 | Candidate IQR / median |
| --- | ---: | ---: | ---: | ---: |
| Prepared iterator operation | 0.825 ms | 0.913 ms | 0.0990 ms | 1.2% |
| Direct iterator branch | 0.816 ms | 0.831 ms | 0.0793 ms | 1.6% |

The first producer's observed median is 10.7% slower. The corrected producer
removes a per-advance boxed boolean and extra branch-dispatch step, but its
1.9% median difference remains within the samples' spread. Existing-Lython
IQR/median is 0.9% and 2.3%; CPython spread is 23.7% and 38.2%. These are short
diagnostics, **not qualified speedups, regressions or CPython multipliers**.
They do not explain the overall performance gap.

Each producer has one independently declared, seven-round warm comparison:
[generic receipt](generic-warm.json), [direct receipt](direct-warm.json).
The complete owned groups take 7.798 and 7.712 seconds under separate 30-second
external hard deadlines. The second producer changes the iterator instruction;
it does not replace samples of the first producer. All warmup, calibration,
verification, order and loaded identities remain visible. There are no retries,
longer warmups, GC/tiering overrides or qualification lanes.

The [helper job](catalog-micro.json) transforms 1,024 integers through two loops
and filters, producing 1,364 list elements before printing count and checksum.
Both engines reuse compiled code and create fresh invocation state. The warm
timer includes initialization, execution and captured/projected stdout with the
cheap golden check; it excludes compilation, startup, transport and response
hashing. This is a synchronous helper diagnostic, not a complete file-backed
application or an async comparison.

Measured 2026-10-15 on the dedicated Azure Standard_F4as_v7: Ubuntu 24.04.4 LTS,
four AMD EPYC 9V45 vCPUs, 16 GiB RAM, SDK 10.0.401/runtime 10.0.12. CPython is
3.13.16 with ordinary GIL/GC and `-I -S -X utf8`. Ordinary Lython public limits
and a shared unbounded allowance remain enabled. CPython has no artificial
equivalent containment limit.

## Evidence and decision

The existing runtime is 3cad415789be6f294863eacd4c9f6002f105095e. Both candidates
were uncommitted producers on that revision; their informational versions still
name the parent. [Generic](generic-comparison-declaration.json) and
[direct](direct-comparison-declaration.json) declarations freeze source hashes.
Both roles use the identical updated catalog/harness. The first producer has
five changed/new production files; the direct variant has nine. Archived
[generic](inline-list-generic.patch) and [direct](inline-list-direct.patch)
zero-context patches preserve reproducibility without compiling dormant alternatives.
Apply to the named base with `git apply --unidiff-zero`; both patches pass that
command's `--check` mode.

Four structural tests failed before the first change, then passed without
expression fallbacks or fallback-driven local mirroring. Existing class/cell,
`locals()`, zero-argument `super()`, closure/reentry, cancellation and ownership
tests passed. New [independent CPython goldens](cpython-goldens.json) and public
regressions cover globals/nonlocals, nested scopes, unpacking, walrus accumulation,
escaped lambdas and exception restoration. Yielding writes, denied append and
cancellation controls preserve published bytes and return accounting to baseline.
[Existing runtime](baseline-correctness.json), [generic](generic-candidate-correctness.json)
and [direct](direct-candidate-correctness.json) workers match CPython at all
three helper sizes.

After removing the prototypes and architecture-specific tests, the new public
regressions and affected comparison/governed/streaming controls pass 168/168 in
13.437 seconds including cleanup. The quiet retained regression collection also
passes 11/11 in 2.293 seconds. [Validation](validation.json) preserves exact
counts and TRX hashes. Production sources match 3cad4157. The
[source/cleanup proof](cleanup-source-proof.json) verifies frozen candidates,
identical comparison sources, all six exited workers, absent staging, inactive
services and the released VM lease.

Stopping criterion reached: reject both implementations and retain the existing
runtime, useful public regressions, helper workload and evidence. The direct
branch did not remove dictionary-backed comprehension bindings or enough
dispatch work to demonstrate a benefit; this is a source-level remaining cost,
not a quantified attribution. Async compiled-function parity is the next bounded
runtime change. Comprehension lowering must return with a better binding/dispatch
design; simply wrapping the existing iterator loop in more instructions is not
an accepted solution.
