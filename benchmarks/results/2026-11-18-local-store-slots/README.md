# Transfer consumed slots into local bindings

B979 compared `93739eed` with `c14c7c92b` / `12643cec` on the quiet Azure
F4as_v7 VM on 2026-11-18. The verified 32-file images differ only in the
core DLL. All three complete scripts matched the CPython output and file
goldens. The .NET 11 RC3 workers used ordinary GC and tiering.

The ordinary and native drivers now take a consumed temporary's slot
directly into a local. Mirroring and captured-cell stores still publish
through their existing policy. Reads of surviving bindings still publish
before making aliases. The compiler inspection confirmed all six `price_line`
call-result stores were already fused, and identified four remaining stores
after the module's unpack operation. This change removes a publication at
that boundary and ordinary assignments with a consumed temporary.

| Script and phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| Invoice, warm | −2.02 to +0.72% | −2.67 to +3.42% | 0.06 / 2.73% |
| Invoice, compile-to-result | −4.67 to −3.08% | −4.65 to −2.19% | 1.53 / 0.13% |
| Validated records, warm | +0.67 to +2.30% | −0.33 to +2.84% | 0.69 / 0.91% |
| Positional calls, warm control | −2.01 to −0.51% | −2.77 to −0.07% | 0.85 / 0.67% |

Ranges compare all pairings of two process medians per source. Negative
favors the candidate. Each process used eight-second warmup and five
samples. Warm includes mediated RAM I/O, output checks and previous-result
cleanup. Compile-to-result adds compilation and compiled-owner disposal.
Process CPU also includes reset and protocol. Startup is separate.
All four jobs finished in 186.91 seconds with verified quiet endpoints,
joined workers and removed guest fixtures.

Local invoice allocation fell 3.45%. The other cases were effectively
flat. Accounted peaks and restored allowance balances matched. Windows
passed 263 white-box and 373 public Release checks per actual target,
including alias identity, unpacking target order, failed admissions, actual
asynchronous transfers and cancellation cleanup.

Retain the narrower publication boundary and measured invoice compile-to-result
improvement. The validated-record elapsed regression and mixed warm invoice
results remain explicit. Recheck validated records alongside the next related
runtime redesign. This comparison used two Lython sources.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Source and validation](checks-and-procedures.tar.xz)
