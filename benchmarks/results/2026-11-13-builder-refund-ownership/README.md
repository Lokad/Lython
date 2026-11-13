# Byte-builder refund ownership — R295

The complete report CPU source gate exposes a shared storage transaction whose
ownership fields were published **after** external refund callbacks. Four small
regressions fail on917e01d8: reentrant/throwing release double-refunds a retained
128-byte input's charge (256→248 accounted bytes), while a refusing/throwing growth
refund leaves replacement storage reserved after cleanup (314 instead of256).
These are actual retained funding/cleanup failures, not exact admission thresholds.

The fix commits and publishes the replacement before refunding old backing;
release publishes an empty owner first. Failed allocation rolls back only its
unpublished reservation. Cleanup after a callback failure therefore releases its
own surviving storage, preserving the independent input and the first structured
admission-refusal origin. Construction still admits the full replacement overlap.
No new owner, reservation object, ledger, cache, retry, policy or option is added.

First-form core305fa32f /570-file source inventory is preserved with the
reference source, exact patch, four original failures and final checks. Actual
.NET11/.NET10 focused suites pass **134 internal /704 public per target**, including
formatting, output, pending protocol cleanup, cancellation and admitted storage.
All seven maintained scripts pass both APIs, complete stdout/files, zero final
balances and owned cleanup against the same fixed worker and qualified9e53 image.
Allocation/desktop clocks are diagnostic; no CPU gain or equivalence is claimed.
The desktop runs overlap separate test processes and are not timing qualification.

This correction follows the completed917e01d8 platform checkpoint; that earlier
GREEN result does not qualify this new source. Batch full Windows/Ubuntu, both
targets, original stress and installed consumers at the next delivery boundary.
Publication remains held; complete formatting operand/construction work remains
an independent performance milestone. Do not withdraw the ownership correction
because a future CPU comparison is uncertain.

[Validation](validation.json), [source, raw failures and diagnostics](source-checks.tar.xz),
[report CPU gate](../2026-11-13-report-cpu-source-gate/README.md).


## Bounded refinement

Final candidate **ed398473** separates the existing cold Grow transaction from
the capacity fast path. The first-form UTF16 sync report had a diagnostic elapsed
+9.84..+10.20% result with0.33% reference spread, while async improved and other
controls drifted. An exception-handler/inlining effect is a hypothesis, not a
measured explanation. Preserve that original adverse condition.

The refinement passes the same134internal/704public per actual target. Seven full
sync scripts/goldens/balances/cleanup pass; first-form async diagnostics remain
source-labelled separately. Allocation remains effectively flat. This is one
bounded short desktop confirmation; its clocks remain diagnostic, so neither CPU
equivalence nor a speedup is claimed. Full qualification of the final correction
is pending at the next delivery boundary, independently of917e01d8's GREEN result.

[Final source/checks and all seven raw sync rows](refined/source-checks.tar.xz),
[exact validation state](refined/validation.json).
