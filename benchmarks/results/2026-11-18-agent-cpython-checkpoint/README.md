# Targeted complete-agent-script CPython checkpoint

B929,2026-11-18 UTC, qualified62786960 production/frozen5ae019db versus stock
CPython3.13.16(PGO/LTO,GIL,no JIT). Quiet AzureF4as_v7/4vCPU AMD EPYC9V45/16GiB,
Ubuntu24.04.4/.NET11RC3, normal GC/tiering. Six jobs226.58s; six-second full-request
warmups/five rotated rounds/two processes per engine. Existing workers/catalog.

| Complete script | Phase | Lython | CPython | Lython / CPython | Status |
| --- | --- | ---: | ---: | ---: | --- |
| Configuration edit | Compilation only | 0.036–0.037 ms | 0.035–0.035 ms | 1.05–1.08× | Diagnostic627/5ae; controls L/C 3.34/0.05%; IQR≤0.70% |
| Configuration edit | Warm execution | 0.077–0.078 ms | 0.061–0.062 ms | 1.25–1.26× | Diagnostic627/5ae; controls L/C 0.78/0.75%; IQR≤0.99% |
| Configuration edit | Compile-to-result | 0.145–0.145 ms | 0.134–0.135 ms | 1.08–1.08× | Diagnostic627/5ae; controls L/C 0.09/0.32%; IQR≤0.85% |
| Formatted report | Warm execution | 2.934–2.942 ms | 2.671–2.737 ms | 1.07–1.10× | Diagnostic627/5ae; controls L/C 0.27/2.47%; IQR≤5.34% |
| Floating JSON numeric report | Warm execution | 1.450–1.530 ms | 0.872–0.874 ms | 1.66–1.75× | Diagnostic627/5ae; controls L/C 5.49/0.29%; IQR≤0.90% |
| Floating JSON numeric report | Compile-to-result | 1.518–1.645 ms | 1.027–1.030 ms | 1.47–1.60× | Diagnostic627/5ae; controls L/C 8.33/0.28%; IQR≤1.08% |

Configuration edits181B/16paths; report3072records/189441B; floating4096pairs/56495B.
Config/report source text equals the maintained scripts and archived R469 catalog.
All32image hashes, actual-worker catalog verification, goldens and shared-budget
cleanup pass. Local process-wide allocation diagnostics are separate from VM timing;
their first compile/run clocks are cold, uncontrolled observations.

Warm guest clocks include Run, mediated RAM-backed I/O, output/file goldens and
previous-result cleanup. Fresh adds compilation/compiled-owner disposal. Compilation
includes owner disposal. Startup/reset/request framing/final cache teardown excluded;
wider process CPU includes reset/protocol. Separately warmed phases are not additive.
CPython's native file APIs do not provide equivalent containment/ownership checks.

Config warm controls<0.8%/fresh<0.4% support a modest remaining latency gap, not
setup attribution. Report CPython control2.47%/maxIQR5.34%limits precision.
Floating identical Lython processes differ5.49%warm/8.33%fresh despite small IQRs:
keep this source/process uncertainty explicit; do not infer a causal R503 gain from
older collections or select one favorable process. No broad unchanged retiming.
The large floating gap persists; profile-guided redesign remains active.

Persistent archive verified under owned lease before RAM teardown; all users joined,
quiet gates passed, local SHA verified. Only exact committed supersededcf core aliases
retired after fresh identity/link/path proof; current5ae/catalog/toolchains retained,
254/252MiB guards unchanged. No publication or performance-universality claim.

[Summary](summary.json),[raw](observations.tgz),[identity](identity.json),
[catalog](catalog.json),[actual-worker verification](preflight.json),
[allocation/reuse](allocation.json),[procedures](procedures.tar.xz).
