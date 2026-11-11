from pathlib import Path
repo=Path.cwd().resolve();r=Path(__file__).parent.resolve()
old=(r/'refined/b589').resolve();new=(r/'refined/b589-four-job-inventory-refusal').resolve()
assert old.is_relative_to(repo) and new.is_relative_to(repo) and old.is_dir() and not new.exists()
assert not(old/'observe-not-before.json').exists()
old.rename(new)
print('Original four-job inventory refusal preserved. No VM mutation occurred; generating five actual jobs with matching archive assertions.')
