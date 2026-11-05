# Bulk whitespace ranges and cased capacity

Retain the final D53 construction contract, source6c776d0d plus archived patch
(later documentation commits do not change that baseline). Ordinary split/rsplit
keep their original scalar work. The existing unbounded lower/upper whitespace
split has a collector that derives conservative capacity from widths already
decoded. It removes per-word capacity rescans and unused casing/maxsplit coupling.
Final ranges/output admission, Unicode writers and one representation guard remain
shared. A small amount of scan structure is duplicated between the actual bounded
ordinary and unbounded cased contracts; no framework, guest fast path, mode, cache,
owner, shared-fragment representation or per-string field is added.

## Preserved results and bounded corrections

All blocks use reference source6c776d0d/core66263582, workerb94, CPython3.13.16,
ordinary GC/tiering, quiet exclusive VM and independent identical-current roles.
Each block has seven rotated~150ms samples after5s warmup/engine/job. All identity,
full output/file golden, Closed/final-zero funding, joined-worker and removed-fixture
gates pass. Initial and refined observations remain; they do not qualify final code.

| Whole script | Initial, two current/reference changes | Refined | Final |
| --- | ---: | ---: | ---: |
| Standard text helpers |+1.29% /−2.36%|−0.84% /+4.38%|−3.98% /−2.61%|
| Mixed-Unicode text helpers |−1.27% /−0.03%|−4.74% /−6.50%|−4.03% /+1.69%|
| Unfused split control |—|+3.69% /+2.76%|−3.19% /−1.64%|
| Ordinary CSV |−0.03% /−2.28%|+2.15% /+0.71%|—|
| JSON |−2.49% /−0.88%|—|—|
| Report rendering |−1.23% /−2.88%|—|—|

Negative means lower elapsed. Initial140.32s/five jobs, original180s bound; standard
text identical-current spread3.60%, so inconclusive. Refinement113.00s/four jobs,
150s bound: moving accumulation inside the non-whitespace branch removed a redundant
predicate but exposed the unfused slowdown, beyond its.90% current spread. Preserve
that actual adverse result, rather than blaming all differences on noise.

Final86.42s/three jobs,120s bound separates the real collection contracts to remove
new per-scalar work from ordinary splitting. Standard text current spread1.43%,
unfused1.60%; both current roles are lower than reference. MixedUnicode5.96% current
spread and conflicting signs remain **inconclusive**. Earlier Unicode gains cannot
be promoted to the final code. Final text medians2.3467/2.3801ms vs2.4439 reference;
unfused2.1845/2.2195 vs2.2564; Unicode9.3397/9.8960 vs9.7315.

The code eliminates repeated work with the same conservative estimate; the final
diagnostic does not establish a general, portable or causal CPU gain. Do not add
the favorable changes or attribute the unfused control improvement to removed
casing work. No allocation, startup, RSS, embedding-latency or actual in-flight
cancellation-latency measurement. No further isolated retiming is scheduled;
materially new evidence can justify a later complete milestone.

Boundaries include invocation, actual mediated /dev/shm filesystem I/O, full checks
and prior-result cleanup. Compilation/startup, staging/reset, transport and final
owner/cache/process/fixture teardown are outside timing. Unicode input196608B/
4096lines includes expansions, final sigma, combining/ignorable characters and
astral text. Its full output/report and the unfused control were independently
verified with isolated local CPython3.13.2 and by both VM workers before timing.

## Validation and delivery status

Final Release build14.18s, zero warnings/errors. Focused233internal/193public per
actual.NET11/10 pass, including all29 whitespace scalars, casing expansions/final
sigma, maxsplit/rsplit, cancellation/refusal, two retained results, aliases,
admission cleanup and budget reuse. Existing semantic tests are retained; no
implementation-mirroring tests or test-only checkpoint counters were introduced.

Baseline6c776d0d is fully qualified by CI42044419339/all8. **Final D53 has focused
and diagnostic qualification only**; batch full platform/original stress/installed
consumers at the next coherent retained-runtime checkpoint. Publication postponed;
the broader performance objective remains open.

[Initial contract](source-contract.md), [final contract](final-contract.md),
[original precollection condition](precollection-failure.md),
[verified duplicate-upload cleanup](vm-upload-cleanup.json).
The existing disk guard stopped setup before measurements; only byte-verified
duplicate uploads with retained local copies were removed. Raw evidence and frozen
libraries remain. One local final build failed on a C# pattern-variable scope
collision; the name was corrected before the successful build/tests/freezing.
All raw source snapshots, identities, observations, adverse controls and logs are
preserved in initial/, refined/ and final/.
