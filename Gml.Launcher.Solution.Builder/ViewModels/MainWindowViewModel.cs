using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Gml.Launcher.Solution.Builder.Models;
using Gml.Launcher.Solution.Builder.Views;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Gml.Launcher.Solution.Builder.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private string _projectName;
    [Reactive] public ObservableCollection<AllowedVersion> Versions { get; set; }
    [Reactive] public string SelectedPath { get; set; }
    [Reactive] public string MessageText { get; set; }
    [Reactive] public string ProjectNameSlug { get; set; }
    
    public string ProjectName
    {
        get => _projectName;
        set
        {
            this.RaiseAndSetIfChanged(ref _projectName, value);
            ProjectNameSlug = value.ToLower().Replace(" ", "-");
        }
    }

    public ICommand OpenFolderCommand { get; set; }
    public ICommand CreateInstallersCommand { get; set; }


    public MainWindowViewModel()
    {
        Versions = new ObservableCollection<AllowedVersion>
        {
            new("win-x86", "Windows x32"),
            new("win-x64", "Windows x64"),
            new("linux-x64", "Linux x64")
        };

        OpenFolderCommand = ReactiveCommand.Create(OpenFolder);
        CreateInstallersCommand = ReactiveCommand.CreateFromTask(CreateInstallers);
    }

    private async Task CreateInstallers()
    {
        var selectedVersions = Versions.Where(c => c.IsSelected);

        var allowedVersions = selectedVersions as AllowedVersion[] ?? selectedVersions.ToArray();

        if (allowedVersions.Length == 0)
        {
            MessageText = "Выберите версии для сбоки";
            return;
        }

        if (string.IsNullOrEmpty(SelectedPath) || Directory.Exists(SelectedPath) == false)
        {
            MessageText = "Указана неверная папка с проектом";
            return;
        }

        var solutions = Directory.GetFiles(SelectedPath, "*.sln");

        if (solutions.Length == 0)
        {
            MessageText = "В выбранной директории отсутствует файл .sln";
            return;
        }

        if (solutions.Length != 1)
        {
            MessageText = "Убедитесь, что в выбранной папке лишь один .sln файл";
            return;
        }

        var solutionDirectory = new FileInfo(solutions[0]);
        var projectDirectory = solutionDirectory.Directory;
        var launcherDirectory = new DirectoryInfo(Path.Combine(projectDirectory!.FullName, "src", "Gml.Launcher"));

        if (!projectDirectory.Exists || !launcherDirectory.Exists)
        {
            MessageText = "Папка с лаунчером не существует";
            return;
        }

        var buildFolder = await CreateBuilds(allowedVersions, projectDirectory, launcherDirectory);

        var outputDirectory = new DirectoryInfo(Path.Combine(buildFolder, "installers"));
        outputDirectory.Create();

        var unixBuilds = allowedVersions.Where(c => c.Code.Contains("linux"));
        var windowsBuilds = allowedVersions.Where(c => c.Code.Contains("windows"));

        await CompileUnixInstallers(outputDirectory, unixBuilds.ToArray(), ProjectName, ProjectNameSlug);
        await CompileWindowsInstallers(outputDirectory, windowsBuilds.ToArray(), ProjectName, ProjectNameSlug);

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = outputDirectory.FullName,
            UseShellExecute = true
        });
    }

    private async Task CompileWindowsInstallers(
        DirectoryInfo outputDirectory, 
        AllowedVersion[] toArray,
        string projectName, 
        string projectNameSlug)
    {
    }

    private async Task CompileUnixInstallers(
        DirectoryInfo outputDirectory, 
        AllowedVersion[] unixBuilds,
        string projectName, 
        string projectNameSlug)
    {
        foreach (var unixVersion in unixBuilds)
        {
            //
            //
            var buildDirectory = new DirectoryInfo(Path.Combine(outputDirectory.Parent!.FullName, unixVersion.Code));
            var installerDirectory = new DirectoryInfo(Path.Combine(outputDirectory.FullName, unixVersion.Code));

            var packageData = new DirectoryInfo(Path.Combine(installerDirectory.FullName, "PackageData"));
            var packageDebianData = new DirectoryInfo(Path.Combine(packageData.FullName, "DEBIAN"));
            var packageUserData = new DirectoryInfo(Path.Combine(packageData.FullName, "usr"));
            var packageUserBinData = new DirectoryInfo(Path.Combine(packageUserData.FullName, "bin"));
            var packageUserShareData = new DirectoryInfo(Path.Combine(packageUserData.FullName, "share"));
            var packageUserShareApplicationData =
                new DirectoryInfo(Path.Combine(packageUserShareData.FullName, "applications"));
            var packageUserShareIconData = new DirectoryInfo(Path.Combine(packageUserShareData.FullName, "icons"));

            packageData.Create();
            packageDebianData.Create();
            packageUserData.Create();
            packageUserBinData.Create();
            packageUserShareData.Create();
            packageUserShareApplicationData.Create();
            packageUserShareIconData.Create();

            await CreateDebianControlFile(packageDebianData, projectName, projectNameSlug);
            await CopyUnixApplication(buildDirectory, packageDebianData, packageUserShareApplicationData, packageUserBinData, projectName, projectNameSlug);
            await CreatePreInstallFile(packageDebianData);

            // Process.Start(new ProcessStartInfo
            // {
            //     FileName = "explorer.exe",
            //     Arguments = installerDirectory.FullName,
            //     UseShellExecute = true
            // });
        }
    }

    private async Task CreatePreInstallFile(DirectoryInfo packageDebianData)
    {
        string text = $@"#!/bin/sh

";
        
        await File.WriteAllTextAsync(Path.Combine(packageDebianData.FullName, "preinst"), text);
    }

    private static async Task CreateDebianPostInstFile(DirectoryInfo packageDebianData, string executableFileName, string projectNameSlug)
    {
        
        string text = $@"#!/bin/sh
chmod +x /usr/bin/{executableFileName}

desktop_file_path=""/usr/share/applications/{projectNameSlug}.desktop""
target_file_path=""$HOME/Desktop/{projectNameSlug}.desktop""

# Создаем ссылку на файл .desktop на рабочем столе пользователя
ln -s $desktop_file_path $target_file_path

# Делаем файл исполняемым
chmod +x $target_file_path
";

        await File.WriteAllTextAsync(Path.Combine(packageDebianData.FullName, "postinst"), text);
    }

    private async Task CopyUnixApplication(
        DirectoryInfo buildDirectory, 
        DirectoryInfo packageDebianData,
        DirectoryInfo packageApplicationData,
        DirectoryInfo packageBinData,
        string projectName, string projectNameSlug)
    {
        var executableFile = buildDirectory.GetFiles().Single(c => !c.Extension.Contains("pdb"));
            
        string text = $@"[Desktop Entry]
Encoding=UTF-8
Version=1.0
Type=Application
Terminal=false
Exec=/usr/bin/{executableFile.Name}
Name={projectName}
Icon=/usr/share/icons/{projectNameSlug}.xpm

";
        await File.WriteAllTextAsync(Path.Combine(packageApplicationData.FullName, $"{projectNameSlug}.desktop"), text);

        executableFile.CopyTo(Path.Combine(packageBinData.FullName, executableFile.Name));
        
        await CreateDebianPostInstFile(packageDebianData, executableFile.Name, projectNameSlug);
    }

    private async static Task CreateDebianControlFile(DirectoryInfo packageDebianData, string projectName, string projectNameSlug)
    {
        string text = $@"Package: {projectNameSlug}-package
Version: 1.0
Architecture: all
Essential: no
Priority: optional
Maintainer: {projectName}
Description: {projectName} launcher
";

        await File.WriteAllTextAsync(Path.Combine(packageDebianData.FullName, "control"), text);
    }

    private static async Task<string> CreateBuilds(
        AllowedVersion[] allowedVersions,
        DirectoryInfo projectDirectory,
        DirectoryInfo launcherDirectory)
    {
        foreach (var version in allowedVersions)
        {
            var command =
                $@"dotnet publish ./src/Gml.Launcher/ -r {version.Code} -p:PublishSingleFile=true --self-contained false -p:IncludeNativeLibrariesForSelfExtract=true";

            var processStartInfo = new ProcessStartInfo("cmd", "/c " + command)
            {
                WorkingDirectory = projectDirectory.FullName
            };

            var process = new Process
            {
                StartInfo = processStartInfo
            };

            process.Start();

            await process.WaitForExitAsync();
        }

        var publishDirectory = launcherDirectory.GetDirectories("publish", SearchOption.AllDirectories);

        var buildsFolder = new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, "builds",
            $"build-{DateTime.Now:dd-MM-yyyy HH-mm-ss}"));

        if (!buildsFolder.Exists)
        {
            buildsFolder.Create();
        }

        foreach (DirectoryInfo dir in publishDirectory)
        {
            var newFolder = new DirectoryInfo(Path.Combine(buildsFolder.FullName, dir.Parent.Name));
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

    private async void OpenFolder()
    {
        if (((IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime)?.MainWindow is MainWindow
            mainWindow)
        {
            var dialog = new OpenFolderDialog();
            var result = await dialog.ShowAsync(mainWindow);
            if (!string.IsNullOrEmpty(result))
            {
                SelectedPath = result;
            }
        }
    }
}