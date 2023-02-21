using System.Text.Json;

namespace RaidBot;

internal class LocalPersistence : IEventPersistence
{
    private readonly DirectoryInfo _directory;

    public LocalPersistence(string folder)
    {
        _directory = Directory.CreateDirectory(folder);
    }

    public async Task SaveAsync(ulong id, object content)
    {
        await using var fs = File.Create(Path.Combine(_directory.FullName, $"{content.GetType().Name}-{id}.json"));
        await JsonSerializer.SerializeAsync(fs, content);
    }

    public async Task<T?> LoadAsync<T>(ulong id) where T : class
    {
        var path = Path.Combine(_directory.FullName, $"{typeof(T).Name}-{id}.json");
        if (File.Exists(path))
        {
            try
            {
                await using var fs = File.OpenRead(path);
                return await JsonSerializer.DeserializeAsync<T>(fs);
            }
            catch
            {
            }
        }
        return null;
    }
}
