# Owned transfer dispatch source gate

Private R501/corebfef519a on ea8e9f91 versus committedea8production/corecf847936.
Two files move eight ordinary transfer cases into the existing primary opcode
loop, delete its secondary transfer switch and retain a direct cold member helper.
No new frame/operand view/opcode/policy; native borrowed helper initialization
stays separate after R495's adverse shared-runner designs. Net source lines flat.

B926 six jobs229.76s,2026-11-17 quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, normalGC/
tiering, six-second warmups/five rotated rounds/two processes per source:

| Complete script/phase | Elapsed change | Process CPU change | Reference/candidate spread |
| --- | --- | --- | --- |
| Text normalization warm | −0.84..+0.18% | −1.93..+1.56% |0.87/0.15%|
| Text compile-to-result | −3.22..−1.56% | −1.89..+1.31% |1.64/0.07%|
| Floating report warm | −6.79..+1.22% | −7.58..+0.68% |0.40/8.16%|
| Floating compile-to-result | +0.09..+2.09% | −1.62..+0.69% |1.69/0.30%|
| Record transform warm | −3.63..+3.57% | −2.94..+4.55% |4.14/3.20%|
| Positional calls warm | −0.28..+1.85% | +0.79..+2.02% |0.14/2.00%|

Allocation and accounted peaks are effectively flat; six budget baselines restore.
Both actual11/10 each407internal/566public focused checks and six actual-image
goldens pass. These preserve contexts/spans/PCs, member/protocol suspension,
publication/aliases, cancellation/terminal refusal and cleanup. No new tests mirror
the deleted dispatcher. The unchanged cf reference diagnostics are reused only
after all32image hashes/catalog compare. Auxiliary worker/adapter are reference
copies; source archives/patches identify the two changed production files.

Decision: leave isolated and unpromoted. A secondary switch is removed, but no
storage or substantive operand work disappears. The measured CPU benefit is
unproven, with small adverse call CPU and unresolved source-control movement.
This is uncertainty, not a failed architecture or permanent rejection. Bounded
confirmation/refinement remains possible for a named cause or changed design.
Current evidence does not justify retaining this rearrangement as a substantial
performance improvement. No unchanged full qualification is requested.

Warm includes mediated RAM I/O/goldens/prior-result cleanup; fresh adds compilation/
compiled-owner disposal. Startup/reset/protocol/final cache teardown excluded;
process CPU is wider. All workers joined, quiet gates passed, persistent archive
SHA verified and fixtures removed. Exact committed old5dcore aliases/profile archive
only retired; current cf/prototype images/catalog/toolchains preserved. Original
254/252MiB floors unchanged. No CPython ratio inferred or publication.

[Summary](summary.json),[decision](decision.json),[allocation](allocation-summary.json),
[raw](observations.tgz),[identity](identity.json),[source](candidate-sources.tar.xz),
[procedures/checks](procedures-and-checks.tar.xz).
