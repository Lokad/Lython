# Publish JSON prefixes before default handling (B1621)

Incremental encoding yields the generated list separator or dictionary
key prefix before entering the default path. This gives the writer a
chance to observe and consume the prefix before the callback runs or a
missing handler raises. The existing root-value slot holds the selected
value until the next advance. Its depth comes from the current container.
It remains the selected object if a writer replaces that item in the
original container. Eager encoding still consumes the same traversal.

Against isolated CPython 3.13.2, the immediate reference matched 9 of 30
probe outputs in each execution mode. The correction matches all 30 in
both modes. The probes compare the joined prefix, callback observations
and error types for `dump` and `iterencode`. The case set includes lists,
tuples, mappings, nesting, sorted and indented output, custom separators,
and unsupported values with the default handler. Complete chunk grouping
remains implementation-specific.

Windows and Ubuntu each pass 1,344 focused .NET 11 Release checks.
Native-file tests verify the offsets and partial bytes when a callback
raises. A custom writer replaces a dictionary value before a genuinely
suspending default callback sees the original object. The cancellation
regression holds the selected value while the host borrows its large
prefix and verifies that the callback has yet to execute. The sorted
ownership test verifies the active charge on the new prefix boundary
and the refund after failure. The initial failing ownership test
expected the exception one advance earlier. Its log is preserved.

## File-script checkpoint

The quiet pair compares `e353ba446` with the prefix correction, which
includes the private traversal refund from `fd2cbfd58`. Both 32-file
images verify the same five CPython goldens. The Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. Two workers
per source warmed for five seconds and supplied seven rotated batches
each. All 140 batches, quiet gates, worker joins and fixture cleanup
passed. Elapsed includes warm execution, mediated RAM I/O, verification
and prior-result disposal. Wider CPU also includes fixture reset and
request framing. Ranges span all pairings of worker medians.

| Script | Warm elapsed change | Wider CPU change |
| --- | ---: | ---: |
| Configuration manifest | −0.54 to +0.89% | −1.30 to +1.24% |
| JSON summary helpers | −0.39 to +1.27% | −3.74 to +4.32% |
| Expanded JSON Lines | −1.88 to −0.07% | −2.08 to −1.82% |
| Default JSON Lines | −2.41 to −0.10% | −1.54 to +3.57% |
| Quoted CSV invoice | −1.84 to +3.29% | −1.14 to +3.57% |

Manifest warm allocation stays at 96,536 B and admission falls from
56,623 to 56,399 B. Other admitted peaks match. The default-options
elapsed penalty from the earlier abandoned designs does not recur.
The expanded control improves with lower CPU. The ranges leave the
manifest, summary and quoted invoice inconclusive.

## Closed pair construction

A separate untimed run uses the candidate core and the defaulted-rule
golden. Its host allowance observes 8,200 positive growth requests.
Under closed pair construction, 4,096 requests reserve 520 B for
default storage, 4,096 reserve 160 B for the callable and one reserves
32 B for shared retention. The script output and final budget balance
verify. This census identifies where repeated admission occurs. Earlier
CPU samples locate part of the constructor cost in reservations.
[Call census](call-admission.json) · [B1605](../2026-11-24-defaulted-report-cpu/README.md).

[Quiet summary](quiet-summary.json) · [Allocation data](allocation-summary.json) ·
[Quiet observations](quiet-observations.tar.xz) · [Terminal receipt](quiet-terminal-status.json) ·
[Sources, probes and logs](procedures.tar.xz).
