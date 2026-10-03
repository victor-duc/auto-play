using System.Reflection;
using AutoPlay.Adapters.Persistence;
using AutoPlay.Adapters.Vision;
using AutoPlay.Application.UseCases;
using AutoPlay.Domain.Model;

namespace AutoPlay.ArchitectureTests;

/// <summary>Checks the dependencies actually used by the compiled cross-platform assemblies.</summary>
public class AssemblyDependencyTests
{
    private static readonly Assembly Domain = typeof(Profile).Assembly;
    private static readonly Assembly Application = typeof(ProfileService).Assembly;
    private static readonly Assembly Persistence = typeof(FileProfileStore).Assembly;
    private static readonly Assembly Vision = typeof(OpenCvTemplateMatcher).Assembly;

    [Fact]
    public void Domain_uses_only_the_base_library()
    {
        Assert.All(ReferencedNames(Domain), name => Assert.True(IsBaseLibrary(name), $"The domain uses {name}."));
    }

    [Fact]
    public void Application_uses_only_the_domain_and_the_base_library()
    {
        Assert.All(
            ReferencedNames(Application),
            name => Assert.True(IsBaseLibrary(name) || name == "AutoPlay.Domain", $"The application uses {name}."));
    }

    [Theory]
    [InlineData("Persistence")]
    [InlineData("Vision")]
    public void Adapters_do_not_use_other_adapters(string adapter)
    {
        var assembly = adapter == "Persistence" ? Persistence : Vision;

        Assert.DoesNotContain(
            ReferencedNames(assembly),
            name => name.StartsWith("AutoPlay.Adapters.", StringComparison.Ordinal) || name == "AutoPlay");
    }

    private static List<string> ReferencedNames(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();

    private static bool IsBaseLibrary(string name) =>
        name is "System.Runtime" or "netstandard" or "mscorlib"
        || name.StartsWith("System.", StringComparison.Ordinal)
        || name.StartsWith("Microsoft.Win32.", StringComparison.Ordinal);
}
