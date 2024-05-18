namespace Gml.Avalonia.Builder;

public class AvaloniaBuilder(string solutionFolder)
{
    public BuildProject GetProject(string projectName)
    {
        var directoryInfo = new DirectoryInfo(solutionFolder);
        var slnFile = directoryInfo.GetFiles("*.sln").FirstOrDefault();
        if (slnFile is null)
        {
            throw new Exception("sln file not found");
        }
        
        var lines = File.ReadAllLines(slnFile.FullName);
        var projectLines = lines.FirstOrDefault(line => line.StartsWith("Project(") && line.Split('"')[3].Equals(projectName));

        if (string.IsNullOrEmpty(projectLines))
        {
            throw new Exception("Project not found");
        }
        
        var projectInfo = projectLines.Split('"');

        return new BuildProject
        {
            Name = projectInfo[3],
            SolutionFile = slnFile,
            ProjectFile = new FileInfo(Path.Combine(solutionFolder, projectInfo[5]))
        };

        // foreach (var line in projectLines)
        // {
        //     var split = line.Split('"');
        //     var projectName = split[3];
        //     var projectPath = split[5];
        //     Console.WriteLine($"Название проекта: {projectName}\nПуть к проекту: {projectPath}\n");
        // }
    }
}