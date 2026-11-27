# B1834 Short commands, shipment, JSON Lines and compilation against CPython

On November 27, 2026 UTC, the quiet Azure F4as_v7 ran Lython `4a6000316`
on Ubuntu 24.04.4 with .NET 11 RC3. The comparison host uses the path
metadata changes from `05c1059de`. The reference is stock CPython 3.13.16.
Both machines used for verification match the seven independent output and
file goldens. The VM passes both quiet gates and all 224 timed batches.

| Workload | Input | Phase | Lython | CPython | Lython / CPython |
| --- | --- | --- | ---: | ---: | ---: |
| File search, match at line 1 | 42,010 B file | Warm sync | 0.06591–0.06623 ms | 0.05077–0.05093 ms | 1.294–1.304× |
| File search, match at line 1,025 | 42,010 B file | Warm sync | 0.07531–0.07592 ms | 0.13725–0.13909 ms | 0.541–0.553× |
| Configuration edit | 181 B input | Warm sync | 0.06925–0.06940 ms | 0.06144–0.06186 ms | 1.120–1.130× |
| Five-item `starmap` shipment report | 2,048 records / 27,350 B | Warm async | 2.41579–2.53935 ms | 1.80178–1.81128 ms | 1.334–1.409× |
| Flat JSON Lines summary | 4,096 records / 110,592 B | Warm async | 3.24048–3.32348 ms | 4.15220–4.16260 ms | 0.778–0.800× |
| Nested-array JSON Lines summary | 4,096 records / 131,826 B | Warm async | 4.04103–4.12341 ms | 4.54020–4.60559 ms | 0.877–0.908× |
| Generated ordinary definitions | 96 definitions / 8,641 B source | Compilation only | 0.56532–0.57200 ms | 0.93722–0.93900 ms | 0.602–0.610× |
| Generated ordinary definitions | 96 definitions / 8,641 B source | Compile-to-result | 0.75919–0.76152 ms | 1.12423–1.13454 ms | 0.669–0.677× |

Each lane uses two workers per engine, five seconds of warmup per worker
and seven rotated batches per worker. The ratios divide Lython elapsed by
CPython elapsed. Ranges span all four pairs of worker medians. The wider
process CPU clock includes fixture reset and request framing. Elapsed warm
execution includes mediated RAM-backed I/O, output and file verification
and cleanup. The async rows run the same synchronous Python source in both
hosts. Compile-only includes compiled-owner disposal. Compile-to-result
includes compilation, one verified run and cleanup. Process startup sits
outside these lanes.

The first-line search gap is about 15–16 µs at the complete command
boundary, compared with about 23 µs in the historical B1815 checkpoint.
The runtime and comparison host both changed across checkpoints. The
wide async shipment remains slower, and its Lython worker medians vary
by 5.11%. Both Lines scripts run faster than CPython on these inputs.
The ordinary factory takes 0.602–0.610× for compilation and 0.669–0.677×
through result. That external comparison leaves earlier same-source
compiler timing uncertainty open.

Both JSON feeds contain 5,120 lines, including 1,024 blank lines. The
input byte counts and 4,096 data records are taken from the frozen fixture.
The ordinary script declares 96 functions per build and calls the builder
16 times. A compiler census on the frozen core finds 93 deferred sites
and 63 separate keyword slots in its 933-instruction build function.
This observation counts emitted work and storage. It does not estimate
CPU cost.

The launcher streams 6,813,065 bytes, including the changed core and host
DLLs. Thirty immutable image files are reused under their recorded hashes.
Two pairs of identical frozen core files in completed runs share storage
after staging. Both image paths and both sets of historical archives
verify. Preflight attempts selected older duplicate files with unavailable
VM archives, different manifest formats and existing aliases. They stopped
before staging or changing those copies. The final run verifies the
32-file image and catalog. Workers join, RAM-backed fixtures are removed
and the persistent archive verifies before the lease ends.

[Image](identity.json) · [Ratios and worker spreads](summary.json) ·
[Fixture census](fixture-census.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Staging](stage-custody.json) ·
[Procedures](procedures.tar.xz).
