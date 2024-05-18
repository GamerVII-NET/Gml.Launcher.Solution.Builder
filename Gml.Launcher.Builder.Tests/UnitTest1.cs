using Gml.Avalonia.Builder;
using Gml.Avalonia.Builder.Extensions;

namespace Gml.Launcher.Builder.Tests;

public class Tests
{
    private AvaloniaBuilder _avaloniaBuilder;

    [SetUp]
    public void Setup()
    {
        var solutionFolder = @"D:\Projects\RiderProjects\Gml.Launcher";
        
        _avaloniaBuilder = new AvaloniaBuilder(solutionFolder);

    }

    [Test]
    public async Task Test1()
    {
        var project = _avaloniaBuilder.GetProject("Gml.Launcher");

        await project.BuildProject(
        [
            AllowedVersion.WinX64,
            AllowedVersion.WinX64,
            AllowedVersion.LinuxX64
        ], new BuildOptions
        {
            Manufacturer = "GamerVII Company",
            Version = new Version(1, 1, 0, 2)
        });

    }
}