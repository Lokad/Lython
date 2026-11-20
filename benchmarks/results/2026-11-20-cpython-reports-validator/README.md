# B1173 report and validator comparison against CPython

The retained `c1c15c008` source uses the exact `8643ebfd8` core from B1172.
The quiet Azure F4as_v7 ran Ubuntu 24.04.4, .NET 11 RC3 and stock CPython
3.13.16 on 2026-11-20 UTC. All 32 image files and four independent goldens
verified before collection. Ratios span every pairing of two process medians
per engine. The summary retains duplicate spread, IQR and wider process CPU.

| Workload | Boundary | Lython | CPython | Lython / CPython |
| --- | --- | ---: | ---: | ---: |
| Shipment report with format selectors | Warm execution | 3.812–3.827 ms | 3.067–3.124 ms | 1.22–1.25× |
| Shipment report with format selectors | Compile-to-result | 3.825–3.852 ms | 3.240–3.245 ms | 1.18–1.19× |
| Shipment report with format selectors | Compilation only | 0.02478 ms | 0.03226–0.03231 ms | 0.77× |
| Expanded forwarding report | Warm execution | 1.775–1.822 ms | 1.154–1.169 ms | 1.52–1.58× |
| Expanded forwarding report | Compile-to-result | 1.869–1.942 ms | 1.303–1.307 ms | 1.43–1.49× |
| Expanded forwarding report | Compilation only | 0.04374 ms | 0.06121–0.06165 ms | 0.71× |
| Generated validator | Warm execution | 0.081 ms | 0.043–0.044 ms | 1.83–1.87× |
| Generated validator | Compile-to-result | 2.110–2.148 ms | 1.949–1.974 ms | 1.07–1.10× |
| Generated validator | Compilation only | 2.04870–2.05688 ms | 1.82118–1.83917 ms | 1.11–1.13× |
| CSV invoice | Warm execution | 6.435–6.455 ms | 6.079–6.091 ms | 1.06× |

Warm includes mediated RAM-backed I/O, output and file goldens, and previous
result disposal. Compile-to-result adds compilation and code-owner disposal.
Compilation only includes the compiler and compiled-owner disposal.
Fixture reset, request setup, startup and final teardown are outside guest elapsed.

Validator compile-only controls spread 0.40% on Lython and 0.99% on CPython.
Its 1.11–1.13× gap is clearer than B1136, which had 7.56% CPython duplicate
spread. Compilation accounts for most of its compile-to-result time.
Forwarding compile-to-result has 3.94% duplicate Lython spread.
Shipment warm has up to 4.91% IQR. These rows describe the measured revision.

Both machine gates and all 200 batches passed. Workers joined and the RAM-backed
guest fixture mount was removed. The archive was written to persistent storage
before the service failed its final free-space assertion. Staging used the
168 MiB reserve from B1172 while the archiver still checked 174 MiB. The
original exit and journal are preserved. A subsequent idle lease matched the
original archive to the persistent observations, goldens and complete image.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Original terminal status](terminal-status.json) ·
[Archive recovery](archive-recovery.json) · [Procedures](procedures.tar.xz) ·
[Exact measured core](../2026-11-20-utf8-format-specifiers/measured-core.dll.gz).
