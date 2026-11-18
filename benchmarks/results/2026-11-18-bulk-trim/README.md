# Shared bulk whitespace scanner for default trim

R506 private65fcf7d0 on9bf3a63d versus qualified62786960/5ae019db.
Default strip/lstrip/rstrip reuse split's existing managed bulk ASCII scanner.
Remove Rune decoding and structural traversal per ASCII character; Unicode and
explicit-character scalar matching stay. No new opcode/cache/owner/policy.
Chunks≤64B use the existing cooperative structural check; a full check occurs
within4096ASCII bytes, while Unicode/custom scans retain scalar checkpoints.
The old256character cancellation fixture now scans8192characters so it tests
actual long work rather than an exact former checkpoint location. Boundary cases
cover0/1/63/64/65/129padding, C0/Unicode whitespace, multibyte/nonwhitespace edges,
all-whitespace output, explicit characters and unchanged identity.

Actual11/10 each429internal/29public focused checks, plus78public text/ownership/
asynchronous cancellation regressions per target pass; six exact-worker goldens,
allocation and shared-budget cleanup pass. Storage/admission/alias behavior is
unchanged. Full platform/originalstress/installed Release consumers are batched
for the next coherent retained delivery boundary, not inherited from62786960.

B930,2026-11-18 quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, normalGC/tiering:
two jobs80.34s, six-second full-request warmups/five rotated rounds/two processes
per source. Complete text normalization warm elapsed1.38–2.19%lower, processCPU
1.16–3.81%lower, controls0.27/0.56%, maxIQR0.61%; allocation/peaks unchanged.
Floating control elapsed+0.52..+5.25%/CPU0..+5.42%, reference spread4.04% remains
uncertain/adverse. Do not claim universal neutrality or a substantial total gain.
Retain the shared kernel and named modest benefit; broader redesign remains active.

Warm clocks include Run, mediated RAM I/O, goldens and prior-result cleanup;
startup/reset/protocol/final cache teardown excluded, processCPU wider. All32image
hashes/catalog/quiet/join/persistent archive/localSHA/fixture teardown pass.
Preparation failures and original conditions are retained separately; no failed
collection or retiming. Only proved exact committed replicas retired, both current
catalogs/5ae images/toolchains/unmatched evidence preserved;254/252MiBguards unchanged.

[Decision](decision.json),[summary](summary.json),[allocation](allocation-summary.json),
[raw](observations.tgz),[identity/source hashes](identity.json),
[source](candidate-sources.tar.xz),[procedures/checks](procedures-and-checks.tar.xz),
[original preparation conditions](preparation-history.json).
Publication postponed. This milestone does not close the whole-script objective.
