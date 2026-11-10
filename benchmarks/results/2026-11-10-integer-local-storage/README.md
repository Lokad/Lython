# Deferred integer-local storage — R188

Both isolated variants are withdrawn; production runtime remains qualified
`3a413c59`. [Decision and final ranges](decision.md). There is no general CPU improvement claim.
Publication remains postponed.

The existing executable engine can keep uncaptured signed-64-bit local arithmetic
unboxed, materializing and caching an ordinary immutable BigInteger on observation.
A compile-owned candidate fact and invocation-owned, preadmitted long backing replace
eligible per-update boxes/general numeric transport. No wrapper value type, object
ledger, owner/pool/policy, alternative engine or new resource limit is introduced.
Overflow and dynamic values retain existing operations and allocation preflights.
Aliases, captures/mirrors, cleanup, cancellation and shared ownership remain required.
[Prospective contract](milestone.md).

## Original variant

Frozen candidate `76c646d9`, reference `26d43e66` (qualified `3a413c59` equivalent).
Existing workers, optional adapter0.4, dependencies, scripts and fixtures are equal.
Desktop five-script CLR allocation: CSV−2.96%, text−13.81%, report−3.07%,
keyword-default search−17.96..17.97%, config−1.18%. Warm elapsed is mixed/adverse:
CSV+0.75..5.89%, text+0.91..3.81%, report−0.93..+2.76%, search−0.06..+4.45%,
config+1.44..13.12%. Compile allocation+0.008..0.064%; elapsed diagnostic.
Separate phases include the existing complete worker output/I/O/cleanup boundary.

Quiet F4as_v7, Ubuntu24.04.4, actual .NET11RC3, five-second warmup per role,
seven rotated400ms rounds, four independent processes, three complete scripts:
B535 finishes112.766seconds with all identity/output/ownership/quiet/join/archive
checks. All pairing ranges, not confidence intervals:

| Workload | Warm elapsed | Wider process CPU | Control uncertainty |
| --- | --- | --- | --- |
| CSV validation | −1.74..+2.12% | −0.06..+2.44% | Reference spread3.48% |
| Keyword-default search | −1.13..+5.50% | −1.16..+3.40% | Candidate spread6.14% |
| Text summary | −1.25..+0.53% | −2.02..0.00% | Ranges near zero |

Wider process CPU includes reset/protocol work and coarse100Hz ticks. Allocation
savings are distinct from these inconclusive CPU results. Original raw/control
observations and elapsed adversity remain in [the archive](original/observations.tgz)
and [summary](original/summary.json). No unchanged CPython refresh was repeated.

Corrected focused315public/189internal pass on each actual.NET11/10. Five Python
3.13.2 goldens are independent; source/build/test logs and complete frozen source
are archived. Initial partial-struct build failure and unsupported definite-assignment
test shape/correction remain visible. Wider4300public barriers per target and
11final new tests per target passed separately after the original freeze; later
supplemental evidence will identify their exact source.

The first VM launch refused before staging for disk headroom. Recovery verifies
canonical tracked copies and every hard-link alias of two earlier runtime images;
only those duplicate bytes are retired. Current baseline/candidate and raw evidence
survive; [receipt](original/space-recovery.json). No active worker/RAMfixture remains.

## Bounded refinement

An actual local Tier1 disassembly of the same complete text fixture shows three
ReadLocalValue calls in the inlined input handler, while the read helper includes
BigInteger construction. A cold materialization split keeps the normal read small.
This is code-generation evidence, not CPU attribution. JIT diagnostics deliberately
set runtime overrides and are not performance samples. Their first launch used an
invalid fixture-directory prefix, failed before Ready and was joined; corrected
launch uses the existing supervisor prefix, completes/validates1000invocations and
joins. Original errors/trace/receipt remain in original/jit-diagnostic.tar.xz.

Focused150public/189internal checks pass per actual target for the refinement.
The second quiet collection completes four jobs in148.225seconds with all gates;
[raw archive](refined/observations.tgz) and [summary](refined/summary.json) retain
CSV/report adversity, slight search gains and text uncertainty. Separate refined
five-script desktop compile/warm measurements remain under refined/desktop/.
Both variants are withdrawn for the [explicit architectural/regression decision](decision.md).
Only useful semantic/cancellation tests remain;98focused checks per target pass on
the unchanged qualified main runtime after forced builds. The broader performance
roadmap stays active. No unchanged CPython/full-platform qualification is repeated.
