from pathlib import Path
import hashlib,json,shutil,subprocess

root=Path(__file__).resolve().parent
repo=Path('D:/code/Lython')
baseline=root.parent/'regex-dev67-r215'
assert not (root/'feed').exists()
for name in ['Directory.Build.props','Directory.Packages.props','NuGet.Config','CursorAdmissionTests.cs','workflow-fixture-manifest.json','qualify.py']:
    source=(baseline/name).read_text(encoding='utf-8-sig').replace('regex-dev67-r215','regex-dev67-trusted-r215')
    (root/name).write_text(source,encoding='utf-8',newline='\n')
provider=(baseline/'Utf8RegexProvider.cs').read_text(encoding='utf-8-sig')
old='Timeout.InfiniteTimeSpan, cancellationToken), cancellationToken, utf8Pattern: pattern);'
new='Timeout.InfiniteTimeSpan, cancellationToken, inputValidation: Utf8InputValidation.AssumeValid), cancellationToken, utf8Pattern: pattern);'
assert provider.count(old)==1
(root/'Utf8RegexProvider.cs').write_text(provider.replace(old,new),encoding='utf-8',newline='\n')
shutil.copytree(baseline/'feed',root/'feed')
identity=json.loads((baseline/'identity.json').read_text(encoding='utf-8-sig'))
identity['consumerRevision']=subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip()
identity['consumerRuntime']='69644713 strict fsdecode/valid stable UTF-8 contract; normal 0.4 unchanged; delivery CI independent.'
identity['bridge']='Dev67 bridge with sole compilation inputValidation change to AssumeValid, using explicit Lython valid/stable subject precondition. Direct callers uphold the same contract.'
identity['providerSourceSHA256']=hashlib.sha256((root/'Utf8RegexProvider.cs').read_bytes()).hexdigest()
identity['cursorTestSourceSHA256']=hashlib.sha256((root/'CursorAdmissionTests.cs').read_bytes()).hexdigest()
for name,item in identity['packages'].items():
    assert hashlib.sha256((root/'feed'/name).read_bytes()).hexdigest()==item['sha256']
(root/'identity.json').write_text(json.dumps(identity,indent=2)+'\n',encoding='utf-8')
paths=json.loads((baseline/'normal-state-before.json').read_text(encoding='utf-8-sig'))
(root/'normal-state-before.json').write_text(json.dumps({p:hashlib.sha256((repo/p).read_bytes()).hexdigest()for p in paths},indent=2)+'\n',encoding='utf-8')
proposal=dict(hypothesis='Eliminate repeated whole-subject validation where immutable guest storage already guarantees valid UTF-8; preserve native needed matching scans.',
    sourceChanges='Only development adapter compilation inputValidation; core contract/strict fsdecode separately committed.',
    acceptance='Existing focused semantic/current-token/refusal/ownership/cursor tests on both real TFMs, new construction/rebinding tests included; normal source/pin/locks/output identity unchanged by qualification.',
    comparison='One short existing complete-script driver comparison against Validate using identical dev67/core. Include unchanged non-regex and necessary-scan controls. Desktop clocks diagnostic; no general backend or whole-script gain claim.',
    limits='No released pin promotion, producer safety certification, missing-capability claim or new core buffer/owner/switch. Preserve original Validate qualification.')
(root/'proposal.json').write_text(json.dumps(proposal,indent=2)+'\n',encoding='utf-8')
print('Prepared isolated trusted development bridge; baseline evidence unchanged.',flush=True)
