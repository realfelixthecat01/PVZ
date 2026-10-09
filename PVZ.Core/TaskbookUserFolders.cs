namespace PVZ.Core;

public static class TaskbookUserFolders
{
    public static string RootPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "PVZ");

    public static string DownloadsPath => Path.Combine(RootPath, "downloads");

    public static string ProjectsPath => Path.Combine(RootPath, "projects");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DownloadsPath);
        Directory.CreateDirectory(ProjectsPath);
    }
}
