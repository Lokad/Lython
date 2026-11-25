# B1741 Share ordered assignment bindings with syntax validation

The prototype carries ordered assignment names from the existing syntax
validation pass into lexical preparation. Ordinary statements and lambda
bodies skip the second recursive expression walk. A lambda's defaults
contribute to the enclosing scope. Match guards and exception headers keep
their relative binding order. Lazy type expressions, generator scopes and
static comprehension flow still use the recursive collector.

| Workload | Reference compile allocation | Candidate compile allocation | Retained admission, both |
| --- | ---: | ---: | ---: |
| 64 generated record rules, without assignment expressions | 1,630,344 B | 1,617,216 B | 760,066 B |
| 64 generated record rules, with assignment expressions | 1,908,680 B | 1,899,136 B | 987,394 B |
| File-backed configuration manifest | 72,328 B | 72,328 B | 82,304 B |
| Ordinary definitions | 1,437,832 B | 1,437,920 B | 883,617 B |
| Shipment callback report | 149,992 B | 149,512 B | 164,576 B |
| Sparse configuration guards | 2,136,312 B | 2,136,072 B | 2,294,048 B |

The reference is `8fb548c75`. Both 32-file images pass six independent
CPython 3.13 output and file goldens. The local Windows .NET 11 allocation
diagnostic measures compile and compiled-owner disposal with a cancellable
token. Each source has six warmups and eleven samples. Allowance always
returns to baseline. Windows .NET 11 and Ubuntu .NET 10 each pass 1,293
public and 388 internal focused Release checks, including nested defaults,
annotation diagnostics, cancellation and admission refusal.

The Windows elapsed probe showed large variation, including in the unchanged
manifest. The bench VM had 4.3 MiB free, below the storage required for the
two new core images. A bounded confirmation ran on local WSL Ubuntu 24.04
and .NET 11 with one pinned logical processor, four independently warmed
workers and seven rotated rounds. The same-source replicas measure control
variation. The compile boundary includes owner disposal. Compile-to-result
also executes the script and disposes results. CPU uses process ticks across
the worker request. Elapsed uses the worker's invocation clock.

| Workload and phase | Paired CPU change, median and observed range | Paired elapsed change, median and observed range |
| --- | ---: | ---: |
| Rules without assignments, compile | −1.1% (−5.9% to +13.0%) | −1.7% (−5.4% to +17.5%) |
| Rules with assignments, compile | −2.4% (−16.9% to +28.4%) | −2.2% (−18.3% to +22.0%) |
| Rules with assignments, compile-to-result | −5.1% (−18.0% to +4.4%) | −8.8% (−33.2% to +9.1%) |
| Manifest, compile | −8.6% (−54.0% to +17.7%) | −5.0% (−53.6% to +9.2%) |

All ranges span zero, including the same-source controls. These observations
leave the timing effect uncertain. The 0.5–0.8% allocation savings on generated
rules do not justify adding a temporary root map, per-lambda state and visit
actions while other callers still need the recursive collector. Keep the
prototype archived. Revisit if one binding representation can serve the
remaining consumers as well, or if attribution finds a dominant duplicate walk.

[Identity](identity.json) · [Summary](summary.json) ·
[Pinned observations](pinned-observations.tar.xz) · [Procedures and original Windows runs](procedures.tar.xz).
