# UTF-8 keys through guest argument binding

B997 compared `0f48fcbbf` (frozen `bde27322`) with the isolated native-key
prototype (frozen `e70bbfa0`) on the quiet Azure F4as_v7 VM, 2026-11-18 UTC.
The prototype keeps expanded keys in UTF-8 for calls to guest functions and
bound guest methods. It uses a definition-owned native parameter index and
reuses its keyword-default names. Other callable consumers receive their
established admitted CLR name. This prototype is awaiting a retention
decision.

| Workload and phase | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Expanded forwarding report, 1,024 records, warm | −7.67% to −6.00% | −7.60% to −4.38% |
| Expanded forwarding report, compile | −2.31% to −1.43% | −0.70% to −0.53% |
| Expanded forwarding report, compile-to-result | −6.69% to −4.48% | −7.90% to −4.32% |
| Ordinary named calls, warm | −0.28% to +0.69% | −2.56% to +1.50% |
| Ordinary named calls, compile | +0.58% to +1.70% | −0.46% to +3.04% |
| Positional calls, warm | −2.10% to +0.21% | −0.60% to +0.84% |
| Native formatting, warm | +0.94% to +4.77% | 0.00% to +5.86% |

Warm execution includes mediated RAM-backed I/O, golden checks and
prior-result cleanup. Compile-to-result additionally includes compiled-owner
disposal. Startup, fixture reset and final cache teardown are outside elapsed.
Process CPU covers the wider request. The native-formatting control had an
elapsed IQR up to 5.62%, with same-source spread up to 2.21%. Its unfavorable
direction warrants a check on the changed positional packet discriminator.

Local report allocation fell from 5,023,286 to 4,613,574 B (8.16%), and its
accounted peak fell from 5,896,862 to 5,632,670 B (4.48%). Allowance returned to
baseline. Control allocations and peaks were effectively unchanged. Windows
elapsed and cold compilation-allocation screens include wide dispersion and
adverse observations. Use the quiet pair for elapsed and keep the diagnostic
screens for follow-up. This source pair does not refresh the CPython ratios.

Windows passed 704 focused public tests per framework. Ubuntu on WSL passed
44 white-box cases per framework. The checks include native refusal before
custom item lookup, binding a long guest key with less allowance than a
decoded copy requires, and original key identity. The independent CPython
probe confirms exact Unicode and positional-only overflow semantics.

Both complete worker images verified against exact output and file goldens.
The seven jobs finished in 323 seconds, passed both quiet gates and joined
every worker. The raw archive matched the copy made under the VM lease.
Only previously verified archived replicas were retired. The initial
local summarizer expected two jobs. Correcting its check used the same
seven-job archive.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Source, checks and procedures](checks-and-procedures.tar.xz)
