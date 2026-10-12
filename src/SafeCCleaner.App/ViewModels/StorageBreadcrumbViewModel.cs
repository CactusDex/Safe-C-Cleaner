namespace SafeCCleaner.App.ViewModels;

public sealed class StorageBreadcrumbViewModel
{
    public StorageBreadcrumbViewModel(string name, string path)
    {
        Name = name;
        Path = path;
    }

    public string Name { get; }
    public string Path { get; }
}
