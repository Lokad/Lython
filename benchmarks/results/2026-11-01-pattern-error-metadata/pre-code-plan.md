### B60 concrete replacement and acceptance, before code

Reference: local CPython3.13 re/_constants.py at24d048d3; isolated Python3.13.2
will capture independent constructor/error goldens. PatternError stores ordinary
mutable attributes in __dict__, and one formatted argument. Changing fields does
not rewrite args/string. Negative/past-end positions follow Python slice bounds;
text uses scalar indices, bytes uses byte indices. Custom message/pattern/index
protocols retain order and actual asynchronous suspension.

- Replace message-only provider/replacement error construction with one small
  Lython error factory using EXISTING governed PyDict/CustomDict and PyTuple args.
  Keep original pattern aliases; do not copy full subjects or add a new exception,
  owner, registry, parser or tracing framework. Add the real three-argument/keyword
  PatternError constructor to the existing exception callable, including async
  protocols. Every retained payload/summary is admitted before allocation.
- Forward available original patterns into provider failure translation. Preserve
  raw message and optional scalar position; calculate line/column with cancellable
  native UTF-8/byte scans. Missing positions remain None. Keep mutable dict fields
  separate from args so assignment/deletion/__dict__ aliases behave like Python.
- Correct the OPTIONAL adapter: released0.4 XML documents UTF-16 error positions,
  with-1 meaning unknown. Convert on failure over borrowed UTF-8 or the backend's
  already-retained original string; no extra pattern copy/success-path conversion.
  Reflection confirms its Message is unformatted, so core never parses backend
  message text. Clarify that provider error messages exclude location suffixes.
  This bridge requires no new producer. Keep final released-package gate separate.
- Before translating a provider failure, preserve an existing first admission
  refusal and the current cancellation token. Neither may become PatternError.
  Cleanup stays awaited/available; no fallback, retry or background operation.
- Managed strptime has no synthetic regex pattern/position; retain its message
  and publish None for unavailable fields, without recreating CPython's regex.

Semantic acceptance: independent ordinary/keyword/multiline/supplementary/bytes/
negative/past-end/None constructor and provider/template-error goldens; original
pattern/field aliases, mutable/deleted fields and stable args; actual custom
message/pattern/index callbacks and ordered failures. A cooperating provider
qualifies scalar positions, metadata construction refusal before guest effects,
held users/cancellation/first-origin memory failure and budget reuse. Real adapter
ASCII/supplementary/multiline/unknown-position checks stay distinct. Use focused
Release both actual runtimes, then batch full Windows/Ubuntu ordinary/original
stress and explicit package consumers with B59. No publication, no old CI polling.

One<=45s same-process diagnostic: baselineB59 frozen corec19c2c68 plus unchanged
auxiliary bits versus fresh candidate and identical original. Two complete warm
jobs:128 ordinary PatternError constructions/raises/catches (same count output;
metadata fields deliberately unobserved so original has the same golden), and
unaffected maintained CSV helpers.128 interleaved warmups/image,2s settle/case,
seven rotating32-job batches. Share CoreLib/actual hashes, normal GC/tiering; full
outputs/ownership/cleanup/quiet gates, batches>100ms,<=10% IQR and<=3% original drift.
Keep all adverse/inconclusive data; no retiming or phase subtraction/CPython ratio.
Error-path storage/cost can increase because the previously missing metadata is
now real; report it explicitly. Correctness/ownership are retention grounds.
A valid>5% unaffected CSV regression requires an explanation before delivery.
Stop this correction after these checks; no regex framework/general exception
rewrite or automatic enum-compatibility expansion.
