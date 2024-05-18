using System.Data;
using System.Diagnostics;

namespace Gml.Avalonia.Builder.Extensions;

public static class InstallerExtensions
{
    public static async Task BuildProject(this BuildProject project, string[] architectures, BuildOptions buildOptions)
    {
        var binariesDirectory = await project.BuildBinaries(architectures).ConfigureAwait(false);
        await project.CreateInstallers(binariesDirectory, buildOptions);
    }

    private static async Task<string> CreateInstallers(this BuildProject project, string binariesDirectory,
        BuildOptions buildOptions)
    {
        var binariesDirectoryInfo = new DirectoryInfo(binariesDirectory);

        var windowsBinaries = binariesDirectoryInfo.GetFiles("*.exe", SearchOption.AllDirectories);

        foreach (var windowsBinary in windowsBinaries)
        {
            var wxsConfigPath = await project.CreateWxsConfig(windowsBinary, buildOptions);
            await BuildWxsMsiFile(wxsConfigPath);

        }

        return binariesDirectoryInfo.FullName;
    }

    private static async Task BuildWxsMsiFile(string wxsConfigPath)
    {
        var command = $@"wix build ""{wxsConfigPath}""";

        var processStartInfo = new ProcessStartInfo("cmd", "/c " + command)
        {
            WorkingDirectory = new FileInfo(wxsConfigPath).Directory!.FullName
        };

        var process = new Process
        {
            StartInfo = processStartInfo
        };

        process.Start();

        await process.WaitForExitAsync();
    }

    private static async Task<string> CreateWxsConfig(
        this BuildProject project,
        FileInfo executablePath,
        BuildOptions buildOptions)
    {
        var architecture = executablePath.Directory!.Name;
        
        var dictionaryReplace = new Dictionary<string, string>
        {
            { "{{ApplicationName}}", project.Name },
            { "{{ExecutableName}}", executablePath.Name },
            { "{{ExecutablePath}}", executablePath.Name },
            { "{{Manufacturer}}", buildOptions.Manufacturer },
            { "{{ApplicationVersion}}", buildOptions.Version.ToString() },
            { "{{ApplicationGuid}}", Guid.NewGuid().ToString().ToUpper() }
        };

        var templateFile = Path.Combine(Environment.CurrentDirectory, "FileTemplates", "ProductTemplate.xml");
        var customAction = Path.Combine(Environment.CurrentDirectory, "Gml.Custom.Action.dll");
        File.Copy(customAction, Path.Combine(executablePath.Directory.FullName, Path.GetFileName(customAction)));
        var content = await File.ReadAllTextAsync(templateFile);

        content = dictionaryReplace.Aggregate(content,
            (current, replacer) =>
                current.Replace(replacer.Key, replacer.Value));

        var wxsConfigPath = Path.Combine(executablePath.Directory!.FullName, $"{project.Name}-{architecture}.wxs");

        await File.WriteAllTextAsync(wxsConfigPath, content);

        return wxsConfigPath;
    }

    private static async Task<string> BuildBinaries(
        this BuildProject project,
        string[] allowedVersions)
    {
        foreach (var version in allowedVersions)
        {
            var command =
                $@"dotnet publish ./src/Gml.Launcher/ -r {version} -p:PublishSingleFile=true --self-contained false -p:IncludeNativeLibrariesForSelfExtract=true";

            var processStartInfo = new ProcessStartInfo("cmd", "/c " + command)
            {
                WorkingDirectory = project.SolutionDirectory.FullName
            };

            var process = new Process
            {
                StartInfo = processStartInfo
            };

            // process.Start();
            //
            // await process.WaitForExitAsync();
        }

        var publishDirectory = project.ProjectDirectory.GetDirectories("publish", SearchOption.AllDirectories);

        var buildsFolder = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "builds",
            $"build-{DateTime.Now:dd-MM-yyyy HH-mm-ss}"));

        if (!buildsFolder.Exists)
        {
            buildsFolder.Create();
        }

        foreach (DirectoryInfo dir in publishDirectory)
        {
            var newFolder = new DirectoryInfo(Path.Combine(buildsFolder.FullName, dir.Parent!.Name));
            if (!newFolder.Exists)
            {
                newFolder.Create();
            }

            CopyDirectory(dir, newFolder);
        }

        return buildsFolder.FullName;
    }

    private static void CopyDirectory(DirectoryInfo source, DirectoryInfo destination)
    {
        if (!destination.Exists)
        {
            destination.Create();
        }

        foreach (FileInfo file in source.GetFiles())
        {
            file.CopyTo(Path.Combine(destination.FullName, file.Name), true);
        }

        foreach (DirectoryInfo subDir in source.GetDirectories())
        {
            CopyDirectory(subDir, new DirectoryInfo(Path.Combine(destination.FullName, subDir.Name)));
        }
    }
}