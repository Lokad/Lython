# Private workflow fixture layout

The selected public.NET11 run passed416 and failed5 because the relocated output
is no longer below tests/Fixtures. Original failure log retained; all five are
DirectoryNotFoundException before guest execution. Copy those maintained workflow
fixtures (hash manifest recorded) into both private output fixture trees. No
production/test assertion change or rebuild. Rerun only the five failed.NET11
cases, then run the still-unexecuted.NET10 selections. Do not replay unchanged
416 public/84 internal.NET11 successes. The exact producer/cursor source remains.
