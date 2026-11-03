# B169: staged native JSON object construction — withdrawn

2026-11-03. **Withdraw the prototype; restore retained B166 source 2323cceb.**
No public contract change and no full qualification for the discarded prototype.
This is not a permanently closed design: another proposal needs materially new
evidence about eliminated construction, rather than an unchanged retime.

The prototype staged native key/value pairs under an existing temporary reservation
through callbacks. At object completion it allocated the existing dictionary
storage once using the actual member count, inserted values, published the key
snapshot and relinquished scratch before the object hook could suspend. It removed
incremental final-dictionary growth while introducing a second pair representation.
Scratch growth admitted old/new overlap; values and escaping aliases kept their
independent batch funding. No new parser, owner layer, backend, cache cap or replay.

| Complete warm job | Elapsed % / unchanged control, sync | Elapsed % / control, async | Allocation | Accounted peak change |
| --- | ---: | ---: | ---: | ---: |
| Report | +3.45 / −0.77 | +1.43 / −3.83 | +5.02% | unchanged |
| Unique names | +0.93 / +3.78 | +1.03 / −2.30 | −2.20% | unchanged |
| Unknown repeated names | +1.56 / +5.07 | −0.67 / +3.70 | −2.92% | unchanged |
| Four duplicate members / record | +11.06 / +0.98 | +10.09 / −1.72 | +18.98% | +655,360 B |

Duplicate-heavy CPU +11.97% sync /+9.04% async, controls +0.90%/−1.52%.
Report CPU +3.34%/+1.19%, controls −0.68%/−3.58%. Unique and unknown CPU are
inconclusive against mixed controls; allocation reductions alone do not justify
retention. Member-count capacity also wastes storage when keys repeat and could
reject earlier. That tradeoff was explicit before implementation, never promoted.

Decision: the added representation has demonstrated representative costs and does
not eliminate enough work. Remove all prototype runtime code; retain the earlier
immutable key snapshot. No buffer-size, uniqueness-index, new ownership layer or
timing variant is selected. This is a design/complexity decision informed by all
controls, not an automatic fixed-percentage veto. Whole-script performance stays
open; return to complete activation and operand storage attribution.

Baseline core **3bed411efcd6e008fc04bca8634d32be64bafc71742509ff0192e8eee86ab8bc**,
retained B166 source 2323ccebc8d526e5a5e0214d85d2de9feba5bff2. Prototype
**a0cf86434a9cf3406c6d84093a66bb0ebacd90ad36c9302305b60135975dd01a**.
Release .NET 11 767 public/181 white-box PASS before freezing; no .NET 10 prototype
qualification claimed. Restored source is nonincrementally rebuilt and its affected
token/ownership controls rechecked on both frameworks. No timeouts/assertions relaxed.

Same existing complete-job driver as B166, quiet Standard_F4as_v7/Ubuntu 24.04.4/
.NET 11 RC3, ordinary GC/tiering. Both APIs; before/candidate/identical-before
control share CoreLib/hash state. Existing report/unique/unknown fixtures plus
1,024 four-member objects with a repeated key. Full duplicate output independently
confirmed under isolated CPython 3.13.2; no external speed ratio derived.

256 warmups/image, nine rotated 64-job batches, two-second settle/case. Includes
actual mediated RAM-file I/O, fixture reset, public invocation, full stdout/file
checks and result disposal. Excludes compilation/startup/final cached teardown.
All goldens, zero balances, identities, quiet gates, fixture removal and joins PASS.
One owned service **79.037 s**, exit zero; lease released. No extra timing replay.

[All observations](comparison-tables.md), [assessment](assessment.json),
[raw observations](observations.tgz), [identity](identity.json),
[withdrawn patch](source.patch), [source snapshot](prototype-Deserialize.cs.gz),
[reference output](duplicate-reference.json). Snapshot is immutable evidence,
not a dormant execution route. Publication remains postponed.
