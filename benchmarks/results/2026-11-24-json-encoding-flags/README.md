# B1553 Reuse JSON flags and keep the compact iterator

The measured `eabbf02ed` source is retained at `f93970141`.
The matched reference is `b3c447420`. Eager encoding reuses the
runtime's existing Boolean boxes for omitted and resolved flags.
One indent helper covers openings and closings. A pending nonfinite
float keeps only the flag it needs. Streaming retains its raw guest
options and resolves truth at the original points. Both versions
have 160-byte CLR iterator shells with 224-byte admission.

| Warm script | CLR allocation change | Elapsed change | Wider process CPU change |
| --- | ---: | ---: | ---: |
| Expanded JSON Lines export | −9.65% | −3.53% to −3.21% | −1.19% to +0.75% |
| Default-options JSON Lines export | 0.00% | −2.72% to +0.32% | −2.79% to −1.77% |
| Configuration edit | −0.11% | −0.92% to +0.48% | −1.58% to −1.27% |
| Unchanged positional calls | 0.00% | −2.62% to +1.75% | −2.58% to +1.58% |

Expanded encoding allocates 589,768 fewer bytes per 3,072-record
invocation. Admitted peaks match on all four scripts and each run
returns the allowance to baseline. This is a measured elapsed and
allocation improvement on the expanded script. Its CPU range spans zero.
Default timing also spans zero on elapsed, with unchanged allocation.
The earlier larger layout stays archived with its default and positional
penalties. [B1552](../2026-11-24-json-encoding-settings/README.md).

This November 24, 2026 UTC comparison used an Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3 for timing, and six complete local
Windows invocations for allocation. Warm elapsed includes RAM-backed
I/O, golden checks and prior-result disposal. Process CPU includes
reset and protocol. Two independent workers per source warm for eight
seconds and contribute seven batches each. All 112 timed batches,
both quiet gates, joined cleanup and persistent archive verification pass.
All four outputs match stock CPython 3.13.16. Windows and Ubuntu each
pass 909 public and 221 white-box focused Release checks covering
binding, option order, cancellation and terminal refusal.

[Allocation summary](allocation-summary.json) · [Timing](summary.json) ·
[Image hashes](identity.json) · [Terminal status](terminal-status.json) ·
[Raw observations](observations.tar.xz) · [Procedures and patch](procedures.tar.xz).
