using Microsoft.CodeAnalysis;

namespace Trivium.Generators;

/// <summary>
/// Entry point for the Trivium route generator. The generator is intentionally empty in the foundation
/// milestone; route, invoker, DI and callback-data generation land in M2.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TriviumGenerator : IIncrementalGenerator
{
    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}
