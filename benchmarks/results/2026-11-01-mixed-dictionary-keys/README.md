# Mixed dictionary keys and direct native copies (B38)

This is a **correctness and ownership checkpoint**, without a latency or CPython
performance claim. Source is c57f9752 plus [the exact runtime patch](source.patch.gz),
2026-11-01, Windows, .NET 11 RC3 / 10.0.8. [summary.json](summary.json) records
the loaded identities and accounting control. Core/test/probe/benchmark copies
agree after a nonincremental build.

## Demonstrated defect and replacement

The verified original runtime ae22392d (unchanged at 8d099a1f) prints
`2 True 1 2` after inserting a custom key that hashes/compares equal to `'x'`,
then assigning `data['x'] = 2`. CPython 3.13.2 prints `1 True 2 2`.
[Original probe](native-protocol-write.py), [raw result](native-protocol-write-before.json)
and [initial 34-case matrix](before-matrix.jsonl) preserve the failure; 25 of those
34 cases mismatch. This is separate from the rejected B37 optimization.

Native misses with custom keys now use the existing reentrant resolution loop.
An unmatched native key returns to the unchanged native insertion body, without
acquiring protocol side bookkeeping. Mixed-key setdefault shares that loop.
Staged union application and Counter merges carry comparison provenance.
Existing key identity/order, callback mutations/errors, cancellation and terminal
admission refusal remain observable.

Defaultdict construction shares the general dictionary pair parser. Two separate
population routines and their tuple-only pair checks disappear. Native mapping
sources use the existing copy constructor, preserving stored hashes without
re-entering guest hashing. Storage copies no longer materialize an entry array
or reserve obsolete array scratch. Known capacity is admitted before allocation;
unknown sources admit growth, with rollback for unpublished copies. Copy loops
use the existing cooperative checks; no new resource mechanism or public API.

## Semantic and lifetime acceptance

[45 independent CPython goldens](../../../tests/Lokad.Lython.PublicApi.Tests/Fixtures/DictionaryWrites/cases.json)
exercise native/custom keys in both orders, identity/order, assignment, update,
union, constructor, setdefault, callback mutation/errors, derived mappings,
native-copy hash caching, list/generator pairs and invalid pairs. Each runs
through both APIs with a shared budget returning to baseline. Four additional
public cases cover cancellation inside equality, terminal refusal before callback
effects, failure origin and subsequent budget reuse. Explicit guest MemoryError
remains catchable. Write-key hashing/equality retain their synchronous-completion
restrictions; existing asynchronous lookup equality remains supported. This work
does not introduce new asynchronous key-protocol infrastructure.

Broad focused checks pass 355 native / 880 public cases per actual target. Two
native tests separately verify progressive admission before unknown input drains,
cancellation, iterator disposal and failed-copy refunds. Full local ordinary
suites pass 3,954 native / 15,382 public per target before the final scope fix.

Review then catches an introduced thread-static ambient scope spanning an await
in the async defaultdict constructor. Two genuine-suspension cases added to the
existing regression [fail before correction](ambient-before-fix.log). Restricting
the scope to synchronous construction and resumed keyword application fixes them.
The final nonincremental build has zero warnings/errors; 14 native / 51 public
focused cases pass per target. Full platform/stress/package delivery is recorded
in PLAN.md, rather than implied by these earlier full local results.

## Short affected-workload control

[The program](copy-working-set.py) constructs 256 native entries and copies them
into defaultdict through the synchronous public probe. Both the original and
changed runtime print `256 32640`, matching isolated CPython 3.13.2. The reported
governed execution peak is **197,885 -> 164,669 bytes**; projection is 52 bytes
in both. This is a conservative **accounting estimate**, not physical heap/RSS,
CLR allocation, elapsed time or a speedup. Compilation is outside that reported
execution estimate; no timing campaign is conducted.

[Before](copy-working-set-before.json), [final after](copy-working-set-after.json)
and the [earlier after](copy-working-set-after-before-ambient-fix.json) remain.
The final after is repeated once to qualify the scope correction and has the
same estimate; no favorable observation is selected. Retained source/destination
funding and replacement overlap remain. Tighter allowances can reject at different
thresholds; exact tiny-budget compatibility is not promised.
