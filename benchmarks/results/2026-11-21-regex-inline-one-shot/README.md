# B1299 inline scratch for single regex matches

Private candidate `168cdf7a4` uses a bounded managed capture buffer
for single matches with at most eight slots, including the whole
match. It avoids the heap capture buffer and prepared-operation shell.
The provider still owns and closes its preparation under the active
token. Lython validates the same byte ranges and admits the final
match before projecting scalar positions. Wide matches and cursors
retain their existing funded working storage.

The local Windows .NET 11 diagnostic compares `d51891009` and
`168cdf7a4` using the same optional 0.4.0 adapter. Both complete
worker images match three CPython 3.13.2 goldens. Four independently
warmed workers contribute five rotated batches each per workload.
Ranges cover all pairings of their medians. Elapsed includes warm
execution, golden verification and previous-result disposal.

| Complete script | Input | CLR allocation change | Local warm elapsed change |
| --- | --- | ---: | ---: |
| Parse one record at a time with fullmatch | 960 log records | −3.61% | −4.96% to −2.06% |
| Parse the same records with a cursor | 960 log records | −0.014% | −0.21% to +0.96% |
| Wide fullmatch control, 13 capture slots | 960 strings | 0.00% | −4.70% to +35.51% |

Accounted peaks match and all executions return their allowance to
baseline. The per-record allocation result supports the hypothesis.
The wide control has large duplicate-worker spreads, and the
developer-machine elapsed data leave its effect unresolved. Keep the
prototype private pending a controlled comparison and a decision on
whether the current narrow scratch change justifies a separate route.
[B1300 quiet comparison and decision](confirmation/README.md).

Windows .NET 11 and Ubuntu .NET 10 each pass 67 white-box and 422
public Release checks. These cover provider results, original-subject
bounds, admission, cancellation and cleanup. All 60 local timing
batches and both 32-file images verify. Adapter-release qualification
is separate.

[Diagnostic summary](summary.json) · [Raw observations and procedures](checks-and-procedures.tar.xz).
