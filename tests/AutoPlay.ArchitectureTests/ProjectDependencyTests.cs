using System.Xml.Linq;

namespace AutoPlay.ArchitectureTests;

/// <summary>
/// Checks the dependency rules of the hexagonal architecture on the project files, which covers the
/// Windows-only projects that cannot be loaded on every OS:
/// <list type="bullet">
/// <item>the Domain depends on nothing;</item>
/// <item>the Application depends only on the Domain and uses no package;</item>
/// <item>each adapter depends on the Application, never on another adapter or on the UI;</item>
/// <item>only the App (composition root) references the adapters.</item>
/// </list>
/// </summary>
public class ProjectDependencyTests
{
    private static readonly string SourceDirectory = Path.Combine(FindRepositoryRoot(), "src");

    public static TheoryData<string> AdapterProjects => new(
        Directory.GetDirectories(SourceDirectory, "AutoPlay.Adapters.*").Select(Path.GetFileName).OfType<string>());

    [Fact]
    public void Domain_has_no_dependency()
    {
        Assert.Empty(ProjectReferences("AutoPlay.Domain"));
        Assert.Empty(PackageReferences("AutoPlay.Domain"));
    }

    [Fact]
    public void Application_depends_only_on_the_domain()
    {
        Assert.Equal(["AutoPlay.Domain"], ProjectReferences("AutoPlay.Application"));
        Assert.Empty(PackageReferences("AutoPlay.Application"));
    }

    [Theory]
    [MemberData(nameof(AdapterProjects))]
    public void Adapters_depend_only_on_the_application(string adapter)
    {
        Assert.Equal(["AutoPlay.Application"], ProjectReferences(adapter));
    }

    [Fact]
    public void There_are_adapters_to_check()
    {
        Assert.True(AdapterProjects.Count >= 3);
    }

    [Fact]
    public void Only_the_app_references_the_adapters()
    {
        var referencingAdapters = Directory.GetDirectories(SourceDirectory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(project => ProjectReferences(project).Any(r => r.StartsWith("AutoPlay.Adapters.", StringComparison.Ordinal)));

        Assert.Equal(["AutoPlay.App"], referencingAdapters);
    }

    private static List<string> ProjectReferences(string project) =>
        Items(project, "ProjectReference")
            .Select(path => Path.GetFileNameWithoutExtension(path.Replace('\\', '/')))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static List<string> PackageReferences(string project) => Items(project, "PackageReference");

    private static List<string> Items(string project, string itemType)
    {
        var document = XDocument.Load(Path.Combine(SourceDirectory, project, $"{project}.csproj"));
        return document.Descendants(itemType).Select(e => (string?)e.Attribute("Include") ?? string.Empty).ToList();
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AutoPlay.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("The repository root (AutoPlay.slnx) was not found.");
    }
}
