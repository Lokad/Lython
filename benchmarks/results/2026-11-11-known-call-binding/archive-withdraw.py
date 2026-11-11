from pathlib import Path
import gzip, hashlib, io, json, shutil, subprocess, tarfile

repo = Path.cwd()
root = Path(__file__).parent
out = repo / 'benchmarks/results/2026-11-11-known-call-binding'
assert not out.exists()
out.mkdir()
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()

# Freeze the refined source before removing any implementation or internal test.
meta = json.loads((root/'refined/diagnostic-identity.json').read_text(encoding='utf-8'))
for role in ['before', 'candidate']:
    hashes = json.loads((root/'before.json').read_text(encoding='utf-8'))['sourceHashes'] if role == 'before' else meta['sourceHashes']
    with tarfile.open(root/'refined'/('sources-'+role+'.tar.xz'), 'w:xz') as archive:
        for name, expected in hashes.items():
            original = root/'originals'/name
            source = original if role == 'before' and original.exists() else repo/name
            assert sha(source) == expected, name
            archive.add(source, arcname=name)

for label, base, quiet in [('initial', root, 'b586'), ('refined', root/'refined', 'b587')]:
    dest = out/label
    dest.mkdir()
    for name in ['before.json', 'diagnostic-identity.json', 'catalog.json', 'comparison.log', 'focused.log', 'sources-before.tar.xz', 'sources-candidate.tar.xz']:
        shutil.copyfile(base/name, dest/name)
    for phase in ['warm-sync', 'warm-async', 'compile']:
        shutil.copytree(base/phase, dest/phase)
    for p in base.glob('*.log'):
        shutil.copyfile(p, dest/p.name)
    for role in ['before', 'candidate']:
        core = base/'images'/role/'Lokad.Lython.dll'
        assert sha(core) == json.loads((base/'diagnostic-identity.json').read_text(encoding='utf-8'))['images'][role]['Lokad.Lython.dll']
        (dest/(role+'-net11.0.dll.gz')).write_bytes(gzip.compress(core.read_bytes(), mtime=0))
        core10 = base/(role+'-net10.dll')
        (dest/(role+'-net10.0.dll.gz')).write_bytes(gzip.compress(core10.read_bytes(), mtime=0))
    q = base/quiet
    qout = dest/'quiet'
    qout.mkdir()
    for p in q.iterdir():
        if p.is_file() and p.suffix in {'.json', '.log', '.py', '.tgz', '.stderr'}:
            shutil.copyfile(p, qout/p.name)
    receipt = json.loads((q/'archive-receipt.json').read_text(encoding='utf-8'))
    assert sha(q/'observations.tgz') == receipt['archiveSHA256']
    assert receipt['allWorkersJoined'] and receipt['guestFixturesRemoved'] and receipt['archivedAndVerifiedUnderLease']

for p in root.glob('*.py'):
    shutil.copyfile(p, out/p.name)
for p in root.glob('*goldens.json'):
    shutil.copyfile(p, out/p.name)
with tarfile.open(out/'prototype-tests.tar.xz', 'w:xz') as archive:
    for name in ['tests/Lokad.Lython.PublicApi.Tests/PreparedFunctionBindingTests.cs', 'tests/Lokad.Lython.Tests/PreparedFunctionBindingBoundaryTests.cs']:
        archive.add(repo/name, arcname=name)
with (out/'prototype.patch').open('wb') as f:
    subprocess.run(['git','diff','--binary','--','src/Lokad.Lython'],stdout=f,check=True)

decision = {
 'decision': 'Withdraw both forms; preserve useful public semantic/lifetime tests. No new production mapping layer or compiler metadata remains.',
 'reason': 'No whole-script benefit sufficient to justify the added metadata/admission/identity branches. Refined quiet CSV improves slightly, synchronous search remains adverse, async search inconclusive. Allocation flat; compilation allocation +0.38..0.54%. Not a permanent veto on coherent call redesign.',
 'initialQuiet': '5 jobs / 142.949972690 s. SearchAsync -7.46..-4.47%, search sync +0.34..+7.86%, CSV near flat. Wider CPU and identical-control spreads preserved.',
 'refinedQuiet': '5 jobs / 143.083904067 s. Search sync +1.83..+4.93%; CSV sync -2.95..-0.61%; search async -3.97..+0.94%; CSV async -1.41..-0.41%; ordinary text control -1.79..+3.57%. Wider CPU does not establish search CPU regression or broad gain.',
 'checks': 'Initial 5920 focused actual11/10 plus overlapping final cases; refined 5936 focused actual11/10; 15 independent CPython3.13.2 correctness goldens. Full 690 qualification is cached GREEN; no later prototype qualification claim.',
 'limitations': 'Desktop target/control clocks conflict; all adversity retained. No RSS, embedding tail latency, pending-I/O latency, large-file throughput or new external CPython ratio claim.'
}
(out/'decision.json').write_text(json.dumps(decision,indent=2)+'\n',encoding='utf-8')
(out/'archive-identity.json').write_text(json.dumps({str(p.relative_to(out)).replace('\\','/'):sha(p) for p in sorted(out.rglob('*')) if p.is_file()},indent=2)+'\n',encoding='utf-8')

# Restore exact qualified source, removing the new architecture rather than shelving it.
for name in meta['changedProductionFiles']:
    source = root/'originals'/name
    assert sha(source) == json.loads((root/'before.json').read_text(encoding='utf-8'))['sourceHashes'][name]
    shutil.copyfile(source,repo/name)
(repo/'tests/Lokad.Lython.Tests/PreparedFunctionBindingBoundaryTests.cs').unlink()
old = repo/'tests/Lokad.Lython.PublicApi.Tests/PreparedFunctionBindingTests.cs'
text = old.read_text(encoding='utf-8').replace('PreparedFunctionBindingTests','FunctionBindingLifetimeTests').replace('SignatureFactsDoNotFreezeTargetsDefaultsOrArgumentEffects','TargetsDefaultsAndArgumentEffectsRemainLive').replace('RefusalInMappedBodyRemainsTerminalThroughFinallyReturn','RefusalInFunctionBodyRemainsTerminalThroughFinallyReturn')
(old.parent/'FunctionBindingLifetimeTests.cs').write_text(text,encoding='utf-8')
old.unlink()
print('Both exact source pairs, cores, raw observations and failed conditions preserved. Seven production files restored; architecture-only tests removed; public semantics/ownership tests retained.')
