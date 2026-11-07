# Native JSON construction source gate — R61
+
+2026-11-07, restored production e66b8f72 / receipt8937cf00. No runtime prototype,
+timing, new benchmark fixture or repeated qualification. Count actual maintained
+input bytes and JSON string tokens in the existing comparison catalogs:
+
+| Script | Input bytes | String tokens | Escaped tokens |
+| --- | ---: | ---: | ---: |
+| JSON-lines helpers |110,592|12,288|0|
+| JSON-to-Markdown |189,441|15,360|0|
+| Configuration read/edit/write |181|25|0|
+
+Valid escaped tokens already use UTF-8 CopyString, staging output before an exact
+final copy. None of these current inputs reaches that path; changing it cannot
+explain their gap. Direct final capacity could retain substantially more spare
+storage and needs affected-workload evidence before selection. No second parser,
+unescaping algorithm, new memory layer or special benchmark is added to promote it.
+
+ReadJsonContainerRemainder already keeps completed work in a synchronous loop,
+deferring only genuinely pending child/callback continuations. Repeated objects
+already use shared RecordKeys and admitted initial capacity. PyDict.SetRecordItem
+fills ordered known-prefix slots directly; duplicate/mismatched keys preserve
+ordinary mutation. Do not rediscover direct slot construction as absent.
+
+No simple complete admission stage can be deleted on this source proof without
+new envelope/ownership/hook machinery; that larger design is not automatically
+started. Source gate ends with no justified candidate, not a permanent rejection
+or a forecast of saved CPU. Continue the existing whole-script roadmap. Future
+escaped-input work needs representative affected scripts and explicit retained-
+capacity/peak/semantic/cancellation/refusal checks before timing.
+
+[Existing fixture census](existing-fixture-census.json). The census uses actual
+base64-decoded fixture inputs and disjoint escaped-character JSON token matching;
+it measures payload eligibility, not parser cost or performance.
+