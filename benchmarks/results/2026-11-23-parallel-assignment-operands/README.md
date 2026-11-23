# B1517 exact-width tuple display assignment

The explicit-float CSV report evaluates three conversions on each of 4,096
records. Retained reference `09fc2c0a5` makes a tuple and unpacks it. The
measured tapes replace those two instructions with one in-place reversal
of the existing operand slots. The following stores still execute left to
right. The source separates right-hand evaluation from assignment effects
and retains actual tuple values for other uses.

| Same-workload elapsed and process CPU versus reference | Initial `e67ca4033` | Confirmation `e67ca4033` | Refined `c70356956` |
| --- | --- | --- | --- |
| Explicit-float warm elapsed | 5.71–9.48% less | 7.86–11.32% less | 8.99–11.50% less |
| Explicit-float warm CPU | 5.45–9.73% less | 5.76–10.48% less | 9.07–11.53% less |
| Explicit-float time to result | 4.78–9.09% less | — | — |
| Typed ASCII warm elapsed | 0.44–4.45% more | 1.10–3.16% more | 1.55–5.00% less |
| Typed ASCII warm CPU | 0.39–2.96% more | 1.38–2.78% more | 2.16–4.26% less |
| Quote-heavy invoice warm CPU | 1.85–2.44% less | 1.05–6.16% more | 0.00–2.44% less |

Time-to-result CPU on the initial prototype falls 5.05–8.82%. The initial
fully supplied helper control has mixed CPU and 7.90% reference elapsed
spread. The refined invoice elapsed spans zero. Identical control tapes
rule out a guest-instruction change in those cases. Windows JIT diagnostics
with tiering disabled show ordinary dispatch at 12,566→12,698→12,698 bytes
and native dispatch at 4,995→5,082→5,074 bytes. The three collections do
not establish which host-level effect moved the controls.

The refined source `fb3894153` keeps the wide reversal out of both
dispatch loops and restores the original collection arity accessor. The
repeated typed-control slowdown is absent in the matched refined run.
The original form supplies the first-use comparison and the retained form
supplies the refined warm comparison.

Windows allocation diagnostics on the retained shape give **2,556,766
→ 1,966,768 B** per explicit-float invocation, down **23.08%**. Admitted
peak goes **6,285,897 → 5,171,785 B**, down **17.72%**.
The 1,114,112 B difference is 272 B per record. Both measured forms
restore shared-budget baselines. Control peaks match.

The quiet Azure F4as_v7 uses Ubuntu 24.04.4 and .NET 11 RC3. Each lane
pairs two workers per image, eight seconds of warmup and seven complete
timed batches each. Elapsed includes guest work, applicable mediated
RAM-backed file I/O, result verification and prior-result disposal.
The time-to-result lane includes compilation. Both quiet gates, complete
image hashes, CPython goldens, worker joins and fixture cleanup verify
in all three collections. The archive retains 140 initial, 84 confirmation
and 84 refined batches, plus the locally rejected staging estimates.

The retained shape passes 915 public and 334 white-box focused Release
checks per platform, on Windows .NET 11 and Ubuntu .NET 10. The hosted
regression checks failure before targets, suspended right-hand and target
work, alias and local snapshots, repeated targets and partial setter
failure in both execution APIs.

[Initial timing](summary.json) · [Confirmation](repeat-summary.json) ·
[Refined timing](refined-summary.json) · [Tapes](tape-summary.json) ·
[Allocation and procedures](procedures.tar.xz) ·
[Reference image](../2026-11-23-direct-closed-defaults/identity.json) ·
[Retained identity](refined-identity.json).
