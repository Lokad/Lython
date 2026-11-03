
## Ordered observations

| API / workload | Elapsed % / control | CPU % / control | Allocation % / control |
| --- | ---: | ---: | ---: |
| sync/json-summary-helpers | -1.87 / +0.78 | -3.66 / -1.16 | -0.006 / +0.000 |
| sync/text-summary-helpers | -0.39 / +0.33 | -0.00 / +0.96 | +0.000 / +0.000 |
| sync/report-render | -3.49 / -4.20 | -2.59 / -3.25 | -0.003 / -0.002 |
| sync/config-edit | +3.19 / -13.64 | +1.48 / -15.76 | -0.015 / -0.049 |
| sync/json-unique-keys | -31.96 / -3.04 | -34.58 / -3.18 | -14.642 / +0.001 |
| sync/json-unknown-repeated | +15.65 / +0.89 | +20.32 / +1.87 | -0.003 / +0.004 |
| sync/json-reordered | +40.20 / -0.47 | +45.66 / -0.36 | +46.667 / +0.000 |
| async/json-summary-helpers | -2.40 / -1.01 | -2.94 / -0.47 | -0.006 / +0.000 |
| async/text-summary-helpers | +4.50 / +4.24 | +4.60 / +4.40 | +0.000 / +0.000 |
| async/report-render | +1.31 / -0.84 | +1.36 / -0.82 | -0.003 / -0.001 |
| async/config-edit | +0.82 / -9.97 | -0.19 / -12.73 | -0.009 / +0.005 |
| async/json-unique-keys | -36.54 / -5.22 | -37.27 / -6.85 | -14.638 / -0.009 |
| async/json-unknown-repeated | +10.25 / +0.16 | +14.12 / +1.13 | -0.014 / -0.003 |
| async/json-reordered | +36.97 / -2.90 | +39.81 / -2.91 | +46.642 / -0.001 |

## Refined observations

| API / workload | Elapsed % / control | CPU % / control | Allocation % / control |
| --- | ---: | ---: | ---: |
| sync/json-summary-helpers | -1.77 / +3.42 | +1.90 / +3.91 | +0.015 / +0.000 |
| sync/text-summary-helpers | +0.02 / +0.85 | +0.05 / +0.87 | +0.000 / -0.000 |
| sync/report-render | +0.13 / -0.29 | +0.32 / -0.29 | -0.002 / +0.001 |
| sync/config-edit | +2.15 / +1.94 | +2.21 / +1.75 | -0.015 / +0.000 |
| sync/json-unique-keys | -35.15 / -2.44 | -37.05 / -1.75 | -14.642 / +0.002 |
| sync/json-unknown-repeated | -0.91 / -0.50 | -1.66 / -1.17 | -0.016 / +0.001 |
| sync/json-reordered | -0.51 / -0.33 | -0.92 / +0.58 | -0.013 / -0.002 |
| async/json-summary-helpers | -2.27 / +0.17 | -3.17 / -0.77 | -0.027 / -0.021 |
| async/text-summary-helpers | -2.41 / -1.40 | -2.50 / -1.36 | +0.000 / +0.000 |
| async/report-render | -1.45 / -0.80 | -1.26 / -1.02 | -0.003 / -0.000 |
| async/config-edit | +0.46 / +1.81 | +1.05 / +4.31 | -0.015 / +0.000 |
| async/json-unique-keys | -31.72 / +0.27 | -31.99 / +1.34 | -14.636 / -0.001 |
| async/json-unknown-repeated | +0.60 / +0.49 | +0.34 / +0.82 | -0.013 / +0.000 |
| async/json-reordered | +0.04 / +2.50 | +0.03 / +2.78 | -0.010 / +0.004 |
