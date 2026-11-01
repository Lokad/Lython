# Identical-worker latency control fails stability (B54)

2026-11-01. **Same core/host bits, two independent workers:9.78% median elapsed
difference.** The source is unchanged; this is not a speedup. The predeclared5%
median bound fails even though both within-worker IQR bounds pass. No retiming.

## One maintained short control

[Criteria](pre-collection-plan.md) reuse the existing ComparisonMicroCommand,
without framework/runtime changes. Old and new worker paths are IDENTICAL. Frozen
core6e51564b, original source95b5955c/informational20e208c0, byte-identical library/
host/CoreLib hashes and normal GC/tiering overrides verified from worker identities.
Same CSV-helper case/fixtures/full stdout/file goldens. CPython3.13.16 is the normal
third semantic/timing reference, not another Lython control or new qualified ratio.

Dedicated Ubuntu24.04.4 Azure F4as_v7/four EPYC9V45 vCPUs/16GiB/.NET11RC3.
Existing1s bounded warmup,25ms calibration target,2s settle and seven rotating/
interleaved rounds. Supervisor alone disables tiering; every worker removes that
override.15s tool deadline/30s external owned service. **9.020s service**, exit0,
all before/after quiet/full-golden/input-identity/round/worker-close gates pass.
MainPID0, empty control group, inactive/dead/success and free VM lease verified.

| Worker | Median elapsed us/job | Relative IQR |
| --- | ---: | ---: |
| Lython baseline | 8,027.332 | 2.07% |
| Lython identical candidate | 7,242.631 | 4.69% |
| CPython context | 3,491.256 | 1.02% |

[Summary](summary.json) preserves exact values and failed gate. Low within-worker
variation does not prove cross-process comparability. Complete warm public runs
include fixture I/O, validation and declared prior-result cleanup; final results
remain through response and cached programs end on exit. This existing boundary
differs from the private complete-job driver: do not mix absolute numbers or
subtract phases. No startup/compile/cleanup attribution or new CPython multiplier.

## Source decision and limits

Possible process-specific costs include hashing, JIT/PGO, scheduling, caches and
I/O. PyString.GetHashCode caches System.HashCode.AddBytes output; PyValueComparer
uses that hash for native dictionary keys. Microsoft's [HashCode documentation](https://learn.microsoft.com/en-us/dotnet/api/system.hashcode?view=net-10.0#remarks)
describes process-local random seeding. Thus dictionary hash distributions can
vary across workers. This is a source-supported hypothesis, **not proof that it
caused9.78%**; closed workers did not record their hash values. Do not change
runtime hashing/seeding, introduce benchmark-only runtime switches, disable worker
GC/tiering or use unsafe/native techniques to make a comparison pass.

Stop this control. Before another performance-selected runtime trial, one bounded
method decision must address cross-process uncertainty. A narrowly scoped
same-process private diagnostic may share CoreLib hashing while isolating Lython
images; any such method needs its own identical-image/hash/golden/ownership control
and explicit limits. It is not a new benchmark framework or an external-baseline
replacement. No rerun of B53 or this failed control, and no arbitrary longer lane.
Independent correctness work and final released-provider qualification continue.

[All attempts, identities, inputs and machine gates](raw-results.tar.gz), SHA256
`8e87b706800c7a6b6ab239d789394129c9cda944bfa50b2ce120da560377d8a9`,
collector/analyzer and original console output preserve the complete observation.
