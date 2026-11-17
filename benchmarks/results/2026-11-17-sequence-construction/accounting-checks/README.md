# Capacity-accounting assertion corrections

Initial delivery CI43987268690 found two white-box failures. Both reproduce
locally (eight other cases pass). Localtime expected953B but now retains985B;
iterable tuple construction now commits admitted capacity during consumption,
so asserting its funding stays entirely reserved is obsolete.

The corrected assertions retain the old payload floors, bounded linear capacity
headroom, exact total/owner balances, scalar identity, funding before advancing
the source and zero allowance after explicit cleanup. They accept reserved or
committed funding rather than prescribing the old transaction layout. Existing
positive tight factory, large magnitude, first refusal and preallocation-denial
regressions remain. No runtime, timeout or containment behavior changed.

All31affected ownership/accounting cases pass on actual Windows Release.NET11
and.NET10. Original failures and new TRX/logs are in[checks](checks.tgz).
Full delivery must be rerun on the corrected tests; no fully-qualified claim yet.
