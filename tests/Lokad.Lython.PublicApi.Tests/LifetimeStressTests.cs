namespace Lokad.Lython.PublicApi.Tests;

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class RetainedSetShellStressTests
{
    // Keep the original count and set-refusal cap. The None control's measured
    // replacement peak is 3,540,064 bytes; 4MiB funds that successful growth.
    [Theory]
    [InlineData(true, 3145728)]
    [InlineData(false, 4194304)]
    public async Task OriginalRetainedCountRemainsCovered(bool sets, long budget)
        => await SetShellAccountingScenarioTests.AssertRetainedCells(100000, budget, sets);
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class ZipWriterOpenMemberCancellationStressTests
{
    [Fact]
    public async Task MillionWritesStillPropagateOpenMemberCancellation()
        => await ZipReadSurfaceTests.AssertWriterCancellationWithOpenMember(1000000);
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class IntegerParseAccountingStressTests : IntegerParseAccountingScenarioTests
{
    protected override int Digits => 30000;
    protected override int Count => 100;
    protected override long MemoryLimit => 1048576;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class IntegerBuiltinAccountingStressTests : IntegerBuiltinAccountingScenarioTests
{
    protected override int Argument => 20000;
    protected override int Count => 100;
    protected override long MemoryLimit => 2097152;
}

// Original scales and caps, selected separately at containment checkpoints.

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class InstanceLifetimeStressTests : InstanceLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class ExceptionLifetimeStressTests : ExceptionLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class DiffRenderLifetimeStressTests : DiffRenderLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class SequenceMatcherLifetimeStressTests : SequenceMatcherLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class FactoryLifetimeStressTests : FactoryLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class PathLifetimeStressTests : PathLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class WrapperLifetimeStressTests : WrapperLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class RangeLifetimeStressTests : RangeLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class RegexLifetimeStressTests : RegexLifetimeScenarioTests
{
    protected override int BatchSize => 256;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class ClassLifetimeStressTests : ClassLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class ItertoolsLifetimeStressTests : ItertoolsLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class CsvBoundedScanStressTests : CsvBoundedScanScenarioTests
{
    protected override long MemoryLimit => 3145728;
    protected override int SmallRows => 20000;
    protected override int LargeRows => 200000;
    protected override int BatchRows => 512;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class GrowthStorageScenarioStressTests : GrowthStorageScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override long AppendBudget => 3145728;
    protected override int AppendIterations => 10000000;
    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class OsIteratorAccountingScenarioStressTests : OsIteratorAccountingScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class IntConversionAccountingStressTests : IntConversionAccountingTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class BytesLifetimeStressTests : BytesLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class ChainMapLifetimeStressTests : ChainMapLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class CounterArithmeticLifetimeStressTests : CounterArithmeticLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class DatetimeLifetimeStressTests : DatetimeLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class DecimalLifetimeStressTests : DecimalLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class DictUpdateLifetimeStressTests : DictUpdateLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class DifflibShellLifetimeStressTests : DifflibShellLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class FormatLifetimeStressTests : FormatLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class HashlibLifetimeStressTests : HashlibLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class LambdaLifetimeStressTests : LambdaLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class NamedTupleLifetimeStressTests : NamedTupleLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class NestedFunctionLifetimeStressTests : NestedFunctionLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class PartialLifetimeStressTests : PartialLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class RandomLifetimeStressTests : RandomLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class RenderJoinLifetimeStressTests : RenderJoinLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class SliceLifetimeStressTests : SliceLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int SliceLength => 1000;
    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class SortedLifetimeStressTests : SortedLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class SplitLifetimeStressTests : SplitLifetimeScenarioTests
{
    protected override int BatchSize => 256;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class StatisticsModeLifetimeStressTests : StatisticsModeLifetimeScenarioTests
{
    protected override int BatchSize => 32;
    protected override int BatchRepetitions(int iterations) => iterations == 0 ? 8 : Math.Max(8, (iterations + BatchSize - 1) / BatchSize);

    protected override int Iterations(int stressIterations) => stressIterations;

    protected override long Budget(long stressBudget) => stressBudget;
}

[Collection("RuntimeStress")]
[Trait("Category", "Stress")]
public sealed class ImportedCodeRetentionStressTests : ImportedCodeRetentionScenarioTests
{
    protected override int LargeStatements => 20000;
    protected override int NestedStatements => 10000;
    protected override int SmallStatements => 500;
    protected override int SharedStatements => 4000;
    // One coarse compilation fits; duplicate preparation of this body would not.
    protected override long SharedImportBudget => 4194304;
    protected override int LoopDefinitions => 3000;
    protected override long RetentionBudget => 1048576;
    // Three large modules now admit their conservative compilation capacity,
    // rather than retaining only a precise deferred-body node inventory.
    protected override long FundedBudget => 67108864;
    protected override long LoopRetainBudget => 1572864;
}
