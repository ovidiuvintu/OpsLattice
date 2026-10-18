
using System.Xml.Linq;
using Xunit;

namespace OpsLattice.Architecture.Tests;

public sealed class DependencyTests
{
    private static readonly string _solutionRoot =
        FindSolutionRoot();

    [Theory]
    [InlineData("OpsLattice.Core", "OpsLattice.Simulator")]
    [InlineData("OpsLattice.Core", "OpsLattice.Scenarios.Airport")]
    [InlineData("OpsLattice.Core", "OpsLattice.Api")]
    [InlineData("OpsLattice.Simulator", "OpsLattice.Scenarios.Airport")]
    [InlineData("OpsLattice.Simulator", "OpsLattice.Api")]
    [InlineData("OpsLattice.Scenarios.Airport", "OpsLattice.Api")]
    public void Project_ShouldNotReferenceForbiddenProject(
        string sourceProject,
        string forbiddenProject)
    {
        var projectPath = Path.Combine(
            _solutionRoot,
            "src",
            sourceProject,
            $"{sourceProject}.csproj");

        var document = XDocument.Load(projectPath);

        var references = document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.DoesNotContain(
            references,
            reference => Path.GetFileNameWithoutExtension(
                reference!.Replace('\\', '/')) == forbiddenProject);
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(
            AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                directory.FullName,
                "OpsLattice.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate OpsLattice.slnx.");
    }
}
