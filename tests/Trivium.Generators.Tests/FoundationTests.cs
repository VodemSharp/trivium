namespace Trivium.Generators.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void Generator_TypeIsAvailable()
    {
        var type = typeof(TriviumGenerator);

        Assert.Equal("TriviumGenerator", type.Name);
    }
}
