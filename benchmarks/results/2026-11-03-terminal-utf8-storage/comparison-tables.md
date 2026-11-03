# Initial B174 source-pair observations

Percentages compare with the before image. Negative is lower. Control uses identical before source. These are source-pair observations, not CPython ratios.

## sync

| Case | Elapsed % | CPU % | Allocation % | Control elapsed % | Control CPU % | Elapsed IQR before/new/control % | Accounted execution peak delta B |
| --- | ---: | ---: | ---: | ---: | ---: | --- | ---: |
| scripts.csv-summary-helpers.large | +0.077 | +0.138 | +0.020 | -3.554 | -3.196 | 1.37/1.22/1.69 | +10 |
| scripts.text-summary-helpers.large | -3.652 | -3.640 | +0.000 | +1.834 | +2.139 | 3.97/4.11/1.20 | +0 |
| scripts.json-summary-helpers.large | +2.621 | +2.586 | +0.017 | +0.520 | +0.459 | 0.17/0.38/0.33 | +50 |
| scripts.report-render.large | -6.624 | -6.572 | -5.680 | -1.718 | -2.424 | 1.51/0.74/0.54 | +150,059 |
| scripts.config-edit.large | +1.147 | +1.049 | +0.000 | -0.482 | -0.721 | 1.13/1.52/1.09 | +0 |
| scripts.string-transfer-short.large | +2.873 | +2.836 | -9.778 | +10.067 | +10.062 | 0.77/0.99/0.74 | +4,096 |
| scripts.string-transfer-long.large | -1.720 | -1.767 | -22.079 | -0.319 | -0.286 | 2.07/0.82/1.56 | +1,548,154 |
| scripts.string-transfer-unicode.large | -5.604 | -5.691 | -19.053 | -8.311 | -8.411 | 1.69/1.09/2.73 | +189,440 |

## async

| Case | Elapsed % | CPU % | Allocation % | Control elapsed % | Control CPU % | Elapsed IQR before/new/control % | Accounted execution peak delta B |
| --- | ---: | ---: | ---: | ---: | ---: | --- | ---: |
| scripts.csv-summary-helpers.large | +2.152 | +2.060 | -0.022 | -1.988 | -1.942 | 0.44/0.52/0.65 | +10 |
| scripts.text-summary-helpers.large | -1.438 | -1.164 | -0.000 | +1.714 | +1.938 | 0.51/0.82/0.52 | +0 |
| scripts.json-summary-helpers.large | -2.298 | -2.163 | +0.017 | -1.675 | -1.663 | 1.30/0.52/0.55 | +50 |
| scripts.report-render.large | -4.129 | -3.881 | -5.677 | +1.169 | +0.770 | 2.18/1.52/0.97 | +150,059 |
| scripts.config-edit.large | +0.549 | +1.076 | +0.000 | +0.437 | +0.866 | 3.52/2.87/2.75 | +0 |
| scripts.string-transfer-short.large | -2.696 | -2.005 | -9.764 | +0.178 | +0.267 | 2.10/2.85/5.28 | +4,096 |
| scripts.string-transfer-long.large | -9.858 | -9.481 | -22.077 | -2.929 | -2.809 | 1.65/0.71/1.55 | +1,548,154 |
| scripts.string-transfer-unicode.large | -2.791 | -2.415 | -19.049 | +3.747 | +3.540 | 2.90/1.35/2.19 | +189,440 |

