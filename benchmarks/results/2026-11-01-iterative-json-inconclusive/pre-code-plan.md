**B53 — replace recursive JSON container readers with ONE parser (bounded trial).**
Source hypothesis, not a measured removable fraction: per-child recursive value/
container transport and duplicated object-key/comma/value loops impose avoidable
work. B52 is sparse, so it cannot predict magnitude. A coherent replacement can
remove this transport/duplication across defaults AND callbacks, without a second
fast parser or grammar fallback. No value representation, scalar scanner or local
operator/binder/reservation prototype is selected.

Concrete deletion/replacement before code:
- Replace ReadJsonObjectAsync/ReadJsonArrayAsync and recursive child dispatch with
  one iterative reader over the original UTF-8 document. Consolidate dictionary/
  pairs-hook object grammar. Retain existing scalar/string/number scanners, Python
  errors and exact byte/scalar prefix coordinates, options preparation and hooks.
- Private unfinished-container records hold storage, current key and separator
  state. Use an ordinary array, conservatively admitted through the EXISTING
  temporary reservation before allocation; cover geometric capacity and old/new
  replacement overlap. Keep it funded until actual callbacks settle, clear popped
  references and release on completion/unwind. No new owner or memory framework.
- Existing container depth64 and managed callback/number/string stack admission
  stay. Earlier denial from explicit parser workspace is an intentional documented
  tradeoff; no new cap/allowance, retry, abandoned parsing or returned alias refund.
- Delete the original recursive readers; no dormant path/compatibility switch.

Semantic acceptance: nested arrays/objects, duplicates and pairs-hook precedence,
postorder callbacks and arbitrary returned aliases, numeric hooks/constants,
strict/lenient strings, empty/truncated/trailing syntax and error positions/doc
identity, raw prefix decoding/Unicode indices, existing depth/stack policy. Both
APIs and genuinely suspended callbacks must retain current tokens, terminal first
refusal, pending input/workspace/result funding, deterministic cleanup/budget reuse.
Use existing focused JSON/ownership tests and independent nested-prefix/lifetime
cases where needed; both actual TFMs before timing. Preserve allocation preflights.

One <=60s before/candidate/identical-before collection, <=30s/child. Maintained
integer/float numeric reports and JSON helpers, synchronous CSV unaffected control;
128 fixed warmup jobs/7batches64 measured jobs per process, same auxiliary host,
runtime/tiering/inputs and complete outputs. More fixed warmup acknowledges B52's
ongoing JIT; process CPU remains diagnostic, not a guest/causal fraction. No retiming
of closed cases. Include real I/O/validation/disposal, exclude compile/startup.
Quiet/golden/balance/join gates, CPU batches>100ms, <=5% CPU/elapsed and0.2%
allocation control drift per case. Retain only >=10% CPU AND >=5% elapsed gain
against BOTH controls on at least TWO of the three affected scripts, with <=5%
CPU/elapsed regression on every measured script. No allocation-only gain or phase
subtraction. If criteria fail/inconclusive, restore original production source with
fresh timestamps/nonincremental build, retain useful independent tests and evidence,
and stop this prototype without tuning, relaxed gates or a second collection.
Full platform/stress/package checks only for a retained delivery boundary.
