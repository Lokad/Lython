# First JSON byte-input comparison: adverse string control retained

B687, 2026-11-13 UTC, frozenb7306a8f/576inputs against qualified976f9439/frozen97bb,
with the same fixed B685 worker and adapter0.4. Complete ZIP/config uses native
json.loads(bytes) on the candidate and the original explicit UTF8 decode on the
reference. Both intentionally different scripts produce the same complete output;
this is an API/workflow comparison, not a same-script runtime-only attribution.
The unchanged JSON-lines aggregation control passes str to json.loads throughout.
CPython3.13.16 uses the candidate's ordinary bytes/str script, with two processes.

Two jobs complete in87.369s under180s ownership. Each has six rotating process
roles, five-second warmups and five250ms full-request-sized rounds. Warm timing
includes mediated RAM-file I/O, full stdout/file checks and prior-result cleanup;
it excludes compile/startup/staging/reset/protocol/final cache teardown. Wider
process CPU includes reset/protocol. Images/goldens/quietness pass; all users join,
fixtures end, and the persistent archive is SHA-verified. No allocation profile.

| Script | Reference Lython | Candidate Lython | CPython | Candidate / reference |
| --- | ---: | ---: | ---: | --- |
| ZIP/config,1,048,827B archive |59.43–60.34µs|59.66–59.69µs|57.71–59.32µs|−1.13..+0.44% elapsed; wider CPU−4.30..0.00% |
| Unchanged JSON aggregation,4096records |3.387–3.407ms|3.441–3.597ms|4.060–4.129ms|+1.00..+6.20% elapsed; wider CPU+2.56..+6.15% |

Candidate JSON process spread is4.35%, versus reference0.58% and CPython1.71%.
This establishes uncertainty/adversity, not a failed architecture. Source follow-up
finds the previously tiny str input helper enlarged with cold byte-decoding logic
and an extra CheckExecution on every str input. Separate that cold path and remove
the duplicate check before bounded confirmation. The existing caller still owns
execution admission/checks; byte validation/conversion retains its actual checks.

UTF8 decoding previously could already share a full immutable byte array. Do not
claim this change universally removes a physical copy: it removes the required
guest decode call and duplicate payload admission. B730 is a prototype, not fully
qualified and not an elapsed result for the later refinement. Original raw data
and exact source/binary are preserved here; no original condition is discarded.
