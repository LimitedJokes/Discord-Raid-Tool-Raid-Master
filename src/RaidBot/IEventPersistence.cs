
namespace RaidBot;

public interface IEventPersistence
{
    Task<T?> LoadAsync<T>(ulong id) where T : class;
    Task SaveAsync(ulong id, object content);
}
