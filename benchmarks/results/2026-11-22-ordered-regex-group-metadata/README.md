# B1342 Share ordered regex name definitions and defer groupindex

Private `72a458bc1` uses an ordered UTF-8 name table and integer slot
indices through frontend and runtime metadata construction. It removes
eager Python dictionary creation and index boxing. The first observed
`groupindex` property builds a funded Python mapping and its proxies
share the mapping. Group names keep their identity across match results
and mapping APIs. Last-group lookup uses sorted capture order. The
provider API and optional adapter pin are unchanged.

Compared `2d4fb517b` and `72a458bc1` on November 22, 2026 UTC on the quiet
Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Two workers per
revision warmed for at least twelve seconds apiece and supplied seven
rotated batches each. Ranges span the four process-median pairings.
Warm includes verification and prior-result disposal, and time to
result adds compilation and code-owner disposal. Process CPU covers
the wider worker request. Startup and final teardown are separate.
All cases use the pinned 0.4.0 adapter.

| Complete script | Phase | Elapsed change | Process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| 64 named rules | Warm | −1.19 to +3.48% | −1.57 to +4.64% | 2.34 / 2.33% |
| 64 names with groupindex observed | Warm | +0.79 to +4.30% | +1.54 to +3.78% | 0.89 / 2.57% |
| Unicode group report | Warm | +0.42 to +1.36% | −0.18 to +0.18% | 0.21 / 0.72% |
| ASCII group report | Warm | +0.31 to +3.76% | 0.00 to +4.55% | 0.72 / 2.70% |
| 64 unnamed rules | Warm | −0.93 to +1.32% | −0.13 to +0.99% | 0.78 / 1.48% |
| 64 named rules | Time to result | −2.77 to +2.03% | −1.37 to +0.51% | 3.85 / 1.04% |

Keep the variant private. Unobserved named rules allocate 1.36% less
with 24,576 fewer admitted peak bytes. The groupindex observer
allocates 3.72% more, needs 16,384 more admitted peak bytes and uses
1.54–3.78% more warm CPU. In that case the Python mapping and the
ordered map both remain live. Named-rule time to result spans zero.
Unicode elapsed increases with flat CPU. ASCII process CPU ranges from
unchanged to 4.55% higher. The local diagnostic also records adverse
elapsed observations. Revisit if the mapping proxy can lend the existing
ordered storage without adding a second retained table or repeating
index boxing. Preserve stable key and index identities and cancellation
at comparison and materialization.

Windows .NET 11 and Ubuntu .NET 10 each pass 91 white-box and 608 public
focused Release checks. They include cooperative cancellation, refusal
before publishing group metadata, independently owned names, mapping
order and identity, and adapter semantics. Every measured allocation
run returns its allowance to baseline. Both complete images match five
CPython goldens. All 168 timed batches and both machine gates pass.
Workers join and the archive verifies under the lease after fixture
cleanup. The first test build found a white-box constructor still
using the former Python dictionary type. The corrected build preserves
that test's cache admission assertions.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Desktop diagnostic](local-timing.json) · [Observations](observations.tgz) ·
[Source and checks](checks-and-procedures.tar.xz).
