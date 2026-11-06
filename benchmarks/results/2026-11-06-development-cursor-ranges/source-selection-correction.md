# Private source-selection correction

The first build compiled both normal and substituted provider sources: Compile
Remove in early Directory.Build.props ran before SDK default item inclusion.
Original44 duplicate-definition errors are retained. Exclude the normal provider
from that one project's default glob, and explicitly include the private source.
No production source, pin, lock or core dependency was changed. Retry qualification
with this corrected private harness; this is not a producer semantic result.
