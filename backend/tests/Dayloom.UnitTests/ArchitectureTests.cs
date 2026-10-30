using System.Reflection;
using System.Xml.Linq;
using Dayloom.Application.Common.Interfaces;
using Dayloom.Domain.Auth;

namespace Dayloom.UnitTests;

/// <summary>
/// Enforces the Clean Architecture dependency rule (docs/ARCHITECTURE.md): dependencies only point inwards
/// (Api → Infrastructure → Application → Domain). Rules are allowlists, not blocklists, so a dependency nobody
/// thought of (logging, a new package) is rejected by default. Checked at two levels:
/// what each .csproj declares, and what each compiled assembly actually references.
/// </summary>
public class ArchitectureTests
{
    private const string PlainSdk = "Microsoft.NET.Sdk";

    // Project → (allowed project references, allowed packages; null = any package). Keep in sync with ARCHITECTURE.md §1.
    private static readonly Dictionary<string, (string[] Projects, string[]? Packages)> Layers = new()
    {
        ["Dayloom.Domain"] = ([], []),
        ["Dayloom.Application"] = (["Dayloom.Domain"], ["Microsoft.EntityFrameworkCore"]),
        ["Dayloom.Infrastructure"] = (["Dayloom.Application"], null),
        ["Dayloom.Api"] = (["Dayloom.Application", "Dayloom.Infrastructure"], null),
    };

    [Theory]
    [InlineData("Dayloom.Domain")]
    [InlineData("Dayloom.Application")]
    [InlineData("Dayloom.Infrastructure")]
    [InlineData("Dayloom.Api")]
    public void Project_references_point_only_inwards(string project)
    {
        var declared = ReadProject(project).Descendants("ProjectReference")
            .Select(reference => ProjectName(reference.Attribute("Include")!.Value))
            .Order();

        Assert.Equal(Layers[project].Projects.Order(), declared);
    }

    [Theory]
    [InlineData("Dayloom.Domain")]
    [InlineData("Dayloom.Application")]
    public void Inner_layers_declare_no_framework_and_only_allowed_packages(string project)
    {
        var csproj = ReadProject(project);

        // Microsoft.NET.Sdk.Web or a FrameworkReference would pull in all of ASP.NET Core.
        Assert.Equal(PlainSdk, csproj.Root!.Attribute("Sdk")?.Value);
        Assert.Empty(csproj.Descendants("FrameworkReference"));

        var packages = csproj.Descendants("PackageReference").Select(package => package.Attribute("Include")!.Value);
        Assert.Empty(packages.Except(Layers[project].Packages!));
    }

    [Fact]
    public void Domain_assembly_references_only_the_base_class_library()
    {
        Assert.Empty(FindViolations(typeof(Session).Assembly, IsAllowedInDomain));
    }

    [Fact]
    public void Application_assembly_references_only_domain_ef_core_abstractions_and_dependency_injection()
    {
        Assert.Empty(FindViolations(typeof(IAppDbContext).Assembly, IsAllowedInApplication));
    }

    [Fact]
    public void Checker_rejects_dependencies_that_are_not_explicitly_allowed()
    {
        // Control test: proves the checker is not silently passing everything.
        string[] referenced = ["System.Runtime", "Microsoft.Extensions.Logging.Abstractions", "Microsoft.EntityFrameworkCore", "Npgsql"];

        Assert.Equal(
            ["Microsoft.Extensions.Logging.Abstractions", "Microsoft.EntityFrameworkCore", "Npgsql"],
            FindViolations(referenced, IsAllowedInDomain));
        Assert.Equal(
            ["Microsoft.Extensions.Logging.Abstractions", "Npgsql"],
            FindViolations(referenced, IsAllowedInApplication));
    }

    [Theory]
    [InlineData(@"..\Dayloom.Domain\Dayloom.Domain.csproj")] // how dotnet writes it on Windows
    [InlineData("../Dayloom.Domain/Dayloom.Domain.csproj")]
    public void Project_name_is_read_the_same_on_every_operating_system(string include)
    {
        Assert.Equal("Dayloom.Domain", ProjectName(include));
    }

    /// <summary>
    /// .csproj files store Windows-style paths ("..\Dayloom.Domain\Dayloom.Domain.csproj"). On Linux (CI),
    /// backslash is not a directory separator, so normalize it before taking the file name.
    /// </summary>
    private static string ProjectName(string include) =>
        Path.GetFileNameWithoutExtension(include.Replace('\\', '/'));

    private static bool IsBaseClassLibrary(string name) =>
        name is "netstandard" or "mscorlib" or "System" || name.StartsWith("System.", StringComparison.Ordinal);

    private static bool IsAllowedInDomain(string name) => IsBaseClassLibrary(name);

    private static bool IsAllowedInApplication(string name) =>
        IsBaseClassLibrary(name)
        || name == "Dayloom.Domain"
        || name == "Microsoft.EntityFrameworkCore" || name.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal)
        || name == "Microsoft.Extensions.DependencyInjection.Abstractions";

    private static List<string> FindViolations(Assembly assembly, Func<string, bool> isAllowed) =>
        FindViolations(assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty), isAllowed);

    private static List<string> FindViolations(IEnumerable<string> referencedNames, Func<string, bool> isAllowed) =>
        referencedNames.Where(name => !isAllowed(name)).ToList();

    private static XDocument ReadProject(string project)
    {
        var path = Path.Combine(FindBackendRoot(), "src", project, $"{project}.csproj");
        return XDocument.Load(path);
    }

    private static string FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Dayloom.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find Dayloom.slnx above the test output folder.");
    }
}
