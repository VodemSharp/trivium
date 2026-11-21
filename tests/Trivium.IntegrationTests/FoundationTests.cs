namespace Trivium.IntegrationTests;

public sealed class FoundationTests
{
    [Fact]
    public void TestAssembly_IsNamedForTheProject()
    {
        var name = typeof(FoundationTests).Assembly.GetName().Name;

        Assert.Equal("Trivium.IntegrationTests", name);
    }
}
