using Xunit.Abstractions;

namespace Lokad.Lython.Tests;

public sealed class DirectStringUpperTests(ITestOutputHelper output) : DirectStringLowerTests(output)
{
    protected override bool Uppercase => true;
}
