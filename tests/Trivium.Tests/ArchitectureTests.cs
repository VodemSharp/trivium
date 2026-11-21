using System.Xml.Linq;

namespace Trivium.Tests;

public sealed class ArchitectureTests
{
    private static readonly string[] _databasePackagePrefixes =
    [
        "Dapper",
        "dbup-",
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "StackExchange.Redis"
    ];

    [Fact]
    public void Core_HasNoTriviumOrHostingDependencies()
    {
        const string project = "src/Trivium/Trivium.csproj";

        Assert.Empty(ProjectReferences(project));
        Assert.Equal(["Telegram.Bot"], PackageReferences(project));
        Assert.DoesNotContain("Microsoft.Extensions.Hosting", PackageReferences(project));
        Assert.DoesNotContain("Microsoft.AspNetCore.App", FrameworkReferences(project));
    }

    [Fact]
    public void Hosting_DependsOnCoreOnly()
    {
        var references = ProjectReferences("src/Trivium.Hosting/Trivium.Hosting.csproj");

        Assert.Equal(["Trivium"], references);
    }

    [Fact]
    public void AspNetCore_DependsOnHostingOnly()
    {
        const string project = "src/Trivium.AspNetCore/Trivium.AspNetCore.csproj";

        Assert.Equal(["Trivium.Hosting"], ProjectReferences(project));
        Assert.DoesNotContain("Npgsql", PackageReferences(project));
        Assert.DoesNotContain("Dapper", PackageReferences(project));
        Assert.DoesNotContain("StackExchange.Redis", PackageReferences(project));
    }

    [Fact]
    public void FrameworkProjects_ReferenceNoDatabaseDriver()
    {
        var violations = FrameworkProjects()
            .SelectMany(project => PackageReferences(project)
                .Where(package => _databasePackagePrefixes
                    .Any(prefix => package.StartsWith(prefix, StringComparison.Ordinal)))
                .Select(package => $"{project} -> {package}"));

        Assert.Empty(violations);
    }

    [Fact]
    public void Generators_DependOnRoslynOnly()
    {
        const string project = "src/Trivium.Generators/Trivium.Generators.csproj";

        Assert.Empty(ProjectReferences(project));
        Assert.Equal(["Microsoft.CodeAnalysis.CSharp"], PackageReferences(project));
    }

    /// <summary>
    /// Every project under <c>src</c> ships to consumers. Samples orchestrate containers and may reference hosting
    /// integrations; the framework itself never touches a database.
    /// </summary>
    private static IEnumerable<string> FrameworkProjects()
    {
        var root = RepositoryRoot();

        return Directory.EnumerateFiles(Path.Combine(root, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Contains("/obj/", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
    }

    private static string[] ProjectReferences(string project)
    {
        var document = Load(project);

        return [.. document.Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>()
            .Select(path => Path.GetFileNameWithoutExtension(path.Replace('\\', '/')))];
    }

    private static string[] PackageReferences(string project)
    {
        var document = Load(project);

        return [.. document.Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>()];
    }

    private static string[] FrameworkReferences(string project)
    {
        var document = Load(project);

        return [.. document.Descendants("FrameworkReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>()];
    }

    private static XDocument Load(string project)
    {
        var path = Path.Combine(RepositoryRoot(), project.Replace('/', Path.DirectorySeparatorChar));

        return XDocument.Load(path);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Trivium.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("Could not locate the repository root.");
    }
}
