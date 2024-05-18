namespace Gml.Avalonia.Builder;

public class BuildProject
{
    public required string Name { get; init; }
    public required FileInfo ProjectFile { get; init; }
    public required FileInfo SolutionFile { get; init; }
    public DirectoryInfo ProjectDirectory => ProjectFile.Directory!;
    public DirectoryInfo SolutionDirectory => SolutionFile.Directory!;
}