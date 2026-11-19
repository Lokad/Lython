# B1117 annotated control repeat

The original B1117 annotated lane had an 8.52% elapsed spread between reference
workers. This repeat uses the same frozen images and unchanged workload catalog.
Four workers per block warm for at least 14 seconds. The second block reverses
the initial worker order. Negative changes indicate reduced time.

| Block | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | ---: | ---: | ---: |
| First | −0.26…+4.38% | −1.96…+5.08% | 1.25 / 3.35% |
| Reversed | −2.08…+1.79% | −4.20…+1.34% | 1.23 / 2.69% |

Both blocks span zero. Three of four pairings in the first block take longer
and use more CPU. The reversed block includes elapsed and CPU improvements.
The captured and ordinary gains support retention. Annotated elapsed and CPU
remain open.

All 40 timed batches verified in 144.35 seconds. The workers joined and fixture
storage was removed. Both quiet gates passed and the archive was verified.

[Initial pair](../README.md) · [Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz).
