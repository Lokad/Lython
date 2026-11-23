# B1448 Borrow decoded windows for native CSV rows

`c778f0e36` retains the runtime measured at `57654f61a`.
The native text reader consumes a complete buffered CSV line under
its active read. The parser borrows the decoded bytes through row
admission and materialization and gives escaping fields their own
funded payload. One native operation replaces the separate read and
feed/unpack handoffs. The parser shares its borrowed-input grammar
with immutable input lines. Cross-window input and general-grammar
continuations still use admitted scratch.

Compared `9b35ab6b8` and `57654f61a` on November 23, 2026 UTC on
the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. Two
fresh workers per revision supplied seven rotated batches each after
eight seconds of warmup. Ranges cover every pairing of worker medians.
Warm elapsed includes mediated RAM-backed input, output verification
and previous-result cleanup. Wider process CPU includes fixture resets
and request handling. Compilation, startup and final teardown sit
outside those clocks. CPython supplies semantic goldens.

| Complete script | Warm elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | ---: | ---: | ---: |
| Native typed ASCII CSV | −7.50 to −0.12% | −7.55 to −2.08% | 6.00 / 1.87% |
| Native typed Unicode CSV | −1.35 to +0.50% | −1.75 to +0.59% | 1.35 / 0.52% |
| Borrowed typed ASCII CSV | −1.15 to +3.79% | +0.49 to +2.15% | 2.39 / 2.54% |
| Quote-heavy native invoice | +0.68 to +1.51% | 0.00 to +2.17% | 0.08 / 0.75% |
| Nonlocal Unicode text control | −7.30 to +1.28% | −7.43 to +0.45% | 4.97 / 4.08% |

The native ASCII CPU direction holds across worker pairings. Its
reference spread limits the elapsed estimate. Unicode and unrelated
text timing span zero. The invoice and borrowed-input CPU costs remain
visible. Retain the common native improvement and its staging removal
with these measured tradeoffs.

Separate instrumented runs count 138,972 fewer staged bytes out of
139,299 for the native ASCII meter and 222,529 fewer out of 223,267
for Unicode. The quoted invoice's staged bytes and fallback-prefix
copies together fall from 174,147 to 42,382. Counts exclude final
payload construction. Local CLR allocations are effectively flat and
admitted peaks match.

Windows .NET 11 and Ubuntu .NET 10 each pass 172 white-box and 447
public focused Release checks. The new test exercises cleanup
requested during final admission, retained fields, first refusal,
cancellation and direct unpacking. Both exact 32-file images match
five representative CPython goldens and five additional native
specimens. Those specimens cover 4,158 grammar outcomes and multi-window
retention after refill and close. All 140 timed batches verify. Workers
join, the archive verifies under the lease and the guest fixtures are
removed. The procedures retain the initial missing benchmark test
sources, the fixture-count rejection and the VM staging refusal before
the older archive receipts were supplied.

[Timings](summary.json) · [Window copy counts](window-copy-counts.json) ·
[Allocation and allowance](local-allocation.json) · [Exact images](source.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](procedures.tar.xz).
