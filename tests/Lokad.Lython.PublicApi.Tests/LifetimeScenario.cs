namespace Lokad.Lython.PublicApi.Tests;

// A 32-byte stranded charge per iteration exhausts 64 KiB after 2,048
// iterations. This keeps the leak oracle while avoiding repeated long scans.
// Stress subclasses override both methods to retain the original fixture.
[Collection("RuntimeStress")]
public abstract class LifetimeScenario
{
    protected virtual int Iterations(int stressIterations) => Math.Min(stressIterations, 2048);

    protected virtual long Budget(long stressBudget) => Math.Min(stressBudget, 65536);
}
