# Deferred capture coordinates

Both named-group reports read group text, so eager scalar projection gives
them no value. Two private core variants were built against `4b17d8fa2`.
Neither changes the provider contract or adapter. Each passes 437 focused
Release regex tests on Windows .NET 11 and Ubuntu .NET 10. Both verify
the same six whole-script CPython goldens. All shared allowances return
to their baselines.

| Variant | Unicode groups CLR allocation | ASCII groups CLR allocation | Wide offset control |
| --- | ---: | ---: | --- |
| Compact byte ranges, independent scalar reads (`057a3b488`) | −1.51% | −1.66% | About 14.61 MB of UTF-8 counting across 32 matches, versus 0.66 MB in the reference. |
| One deferred scalar sweep (`9af007ab0`) | +2.26% | +2.49% | One ordered sweep per match. Admitted peak rises 768 bytes in the control. |

The byte counts come from applying both source algorithms to the wide
control's CPython capture ranges. It asks for `regs` and two individual
offsets over 80 groups in a 20,800-byte Unicode subject. It has no
prebuilt index. The first variant removes 49 KiB of admitted peak from
each named-group report, but independent reads revisit the same text
in the wide control. The second restores the ordered work and retains
24 extra bytes per ordinary match. That increases allocation and
admitted peak even on the ASCII and group-text-only scripts.

Keep both variants private. Reuse existing match storage for any
deferred state and preserve a single ordered projection for offset
consumers. Verify original-window and repeated-access cases before
timing. These are local allocations and source-level work counts.

[Allocations and image hashes](local-allocation.json) ·
[Wide scan counts](wide-scan-attribution.json) ·
[Source patches, fixtures and checks](procedures.tar.xz).
