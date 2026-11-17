# Floating transport: first condition and control regression

R471/B878–B879, 2026-11-17 UTC. Qualified ccf20ba3/frozen7a599347 versus
private cb7472e7 plus four production files/frozen cf1b0555. **Prototype under
refinement, not delivered.** Publication held. Exact sources and immutable core
are preserved; no claim that these timings describe a later revision.

The complete 4,096-record squared-error JSON/report script is unchanged; input
numbers are divided by8 into exactly representable binary fractions. Independent
CPython3.13.2 stdout/files match actual frozen workers. Existing numeric/CSV
fixtures were integer-only. Fresh double arithmetic can use the existing 16-byte
managed slot through fused locals/helper returns/generator iteration into existing
compensated sum. Observable aliases publish canonical objects; admitted scalar
capacity, mixed/protocol operations, tokens and owners remain. No new backend,
arena, cache, value framework, unsafe code or slot-size increase.

| Complete script | Phase | Elapsed change | Wider CPU change | Reference / candidate control spread |
| --- | --- | ---: | ---: | ---: |
| Floating JSON numeric report | Warm | −26.38..−14.85% | −24.20..−15.70% | 4.07 / 11.14% |
| Same | Compile and execute once | −22.53..−20.00% | −20.67..−20.03% | 1.22 / 2.02% |
| Integer report control | Warm | −4.18..+9.76% | −4.05..+9.19% | 4.59 / 9.52% |
| Helper text control | Warm | −0.51..+0.34% | +0.10..+0.89% | 0.30 / 0.56% |
| Integer report, bounded unchanged-source confirmation | Warm | **+1.67..+4.77%** | **+1.43..+5.56%** | 0.47 / 2.57% |

B878 four jobs187.94s; B879 one job51.05s. Same fixed three-case catalog,
four independent processes, rotated warmups accumulating8s full-request time per
process; five rotated roughly0.5s samples. Ranges are all four process-median
pairings, not confidence intervals. Warm magnitude remains uncertain; fresh
floating gains do not erase the confirmed integer regression. No further unchanged
retiming: refinement restores integer-first sum dispatch, avoiding the newly added
float-phase test for each integer item. Its outcome belongs to a separate record.

Natural-GC local allocation: floating689,344→492,760B/run, **−28.52%**; integer
flat, text+56B/+0.0034%. Accounted peaks unchanged (numeric906,718B). All shared
budgets return to baseline. Local allocation driver includes fixture reset and
output checks; its compilation/first-use clocks are diagnostics, not CPU claims.
Focused Release each actual Windows .NET11/10:93public/79internal; six public
semantic scripts independently pass CPython. Public checks include aliases,
signed zero/nonfinite values, compensated/general sums, mixed integer/errors,
real suspended I/O/cancellation, retained results, terminal refusal and cleanup.
Filters overlap existing coverage; full qualification has not been replayed.

Nonincremental build,32-file images differ only in core; actual candidate13case
goldens pass. Original12 reference cases pass; added case passes after correcting
only Windows-captured CRLF stdout metadata to the workers' logical LF. Preserve
the original13case mismatch and initial compiler diagnostics in procedures.

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, workstation natural GC/normal tiering.
Warm guest timer includes synchronous Run, mediated RAM I/O, full stdout/file
checks and prior-result cleanup; excludes compile/startup/reset/protocol/final
cache teardown. Fresh adds compile and compiled-owner disposal; external CPU
covers a wider request. No async/CPython-speed/RSS/embedding-tail claim.

Both machine gates, all identities/goldens/Closed/exits/joins and RAM cleanup pass.
B878's original serviceexit1 comes solely from an inherited archival assertion
expecting5jobs after the intentionally bounded collection became4. All work and
cleanup finished first. Fresh idle-lease recovery verifies all images/gates/users,
corrects only the offline receipt count, and archives original script/failure.
No timing was repeated or overwritten. B879 succeeds without recovery. SHA-verified
raw archives and terminals cached. Retirement removes only exact committed old
R470core aliases and superseded working catalog,8,441,068B; fresh ownership,
path/hash/link/all-inode-alias proofs preserve canonicals/current images/toolchains
and254/252MiB staging/final floors. See raw retirement receipts.

[Source-pair summary](summary.json), [confirmed regression](confirmation-summary.json),
[allocation](allocation-summary.json), [raw first condition](source-pair/observations.tgz),
[raw confirmation](integer-confirmation/observations.tgz),
[sources](candidate-sources.tar.xz), [identity](frozen-identity.json),
[core](candidate-core.dll.gz), [procedures/checks](procedures-and-checks.tar.xz).
