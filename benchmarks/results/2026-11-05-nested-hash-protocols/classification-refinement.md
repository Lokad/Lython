# Managed classification refinement — before code

B29986.16s/three jobs/all gates is preserved, rawSHAfe01d1a1. Native compound
grouping+8.19/+7.54% elapsed versus correct805, current spread0.60%, roleIQR
0.71/2.81%, reference0.37%. Native CSV small/variable (referenceIQR3.74%),
text-4.20/-3.32% diagnostic. No allocation/CPU/RSS/embedding claim.

Added TupleNeedsProtocol/SliceNeedsProtocol currently enter the full Single guard,
registering/removing identities just to classify immutable key structure. That
classification needs bounded managed depth/stack, not a visited identity set.
Reuse the existing structural state and Scope (a depth-only constructor), and
consolidate duplicate pair/single admission checks into one unchanged-limit check.
No new fields, policy, public setting, native probe or ownership infrastructure.
Actual tuple/slice/dataclass hashing and validation keep their Single cycle guards.
Classification cycles still terminate at the existing depth bound; actual key
hash cycles retain their earlier explicit detection. Preserve exact error kinds,
reasons, limits and finally unwind. This removes mechanisms rather than dropping
stack protection or accepting the measured extra bookkeeping silently.

Add meaningful depth/recovery and native-callback sticky-failure regressions.
Focused actual11/10 first. One <=120s two-job confirmation, target~60s: same
native grouping and complete text control, frozen805 reference/current control/
CPython3.13.16, same normalGC/source/golden/quiet/Closed/finalzero/joins. Original
adverse B299 stays. Decide remaining debt explicitly; no further broad campaign.
