$ErrorActionPreference='Stop'
$env:DOTNET_ROOT='C:/Users/JoannesVermorel/.dotnet11'
$env:DOTNET_HOST_PATH='C:/Users/JoannesVermorel/.dotnet11/dotnet.exe'
$env:PATH=$env:DOTNET_ROOT+';'+$env:PATH
$notes='D:/code/Lython/.git/agent-notes/review-20261031/operand-use-map-b231'
$internalFilter='Category!=Stress&(FullyQualifiedName~Executable|FullyQualifiedName~NativeReturn|FullyQualifiedName~Augmented|FullyQualifiedName~Function|FullyQualifiedName~Binding|FullyQualifiedName~Governed|FullyQualifiedName~Cancellation|FullyQualifiedName~DrainLease|FullyQualifiedName~Slice|FullyQualifiedName~Operand|FullyQualifiedName~ExecutionPathOwnership)'
$publicFilter='Category!=Stress&(FullyQualifiedName~Function|FullyQualifiedName~Closure|FullyQualifiedName~Generator|FullyQualifiedName~Augmented|FullyQualifiedName~Subscript|FullyQualifiedName~Slice|FullyQualifiedName~GovernedMemory|FullyQualifiedName~CooperativeCancellation|FullyQualifiedName~NativeItemMutation|FullyQualifiedName~BoundLoop|FullyQualifiedName~Assignment|FullyQualifiedName~MutationAsync|FullyQualifiedName~Provenance|FullyQualifiedName~Executable|FullyQualifiedName~Operand|FullyQualifiedName~ControlFlow|FullyQualifiedName~Protocol)'
foreach($framework in 'net11.0','net10.0') {
    foreach($suite in 'internal','public') {
        $project=if($suite -eq 'internal') {'tests/Lokad.Lython.Tests'} else {'tests/Lokad.Lython.PublicApi.Tests'}
        $filter=if($suite -eq 'internal') {$internalFilter} else {$publicFilter}
        $logPath=Join-Path $notes ('final-'+$suite+'-'+$framework+'.log')
        dotnet test $project -c Release -f $framework --no-build --filter $filter *> $logPath
        $testExit=$LASTEXITCODE
        Get-Content -LiteralPath $logPath -Tail 25
        if($testExit -ne 0){exit $testExit}
    }
}
