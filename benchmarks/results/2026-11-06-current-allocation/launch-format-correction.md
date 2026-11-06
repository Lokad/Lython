# B344 terminal setup failure / B345 correction

B344 service is terminal failed/MainPID0, shell exit2: set -eu received CRLF from
Windows Path.write_text default newline translation. It failed before Python,
workers, fixtures, collector or observations. Original source/payload/terminal log
retained under failed-b344/. This is a launch-format defect, not workload/design
or measurement evidence. Write LF explicitly, assert no CR bytes in run.sh, syntax-
check before launch, and use distinct B345 root/unit. Same source/binary/workloads,
counts, capture/cleanup/headroom policy; no runtime change or favorable resampling.
