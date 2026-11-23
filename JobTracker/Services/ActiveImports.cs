using System.Collections.Concurrent;

namespace JobTracker.Services;

public sealed class ActiveImports
{
    private readonly ConcurrentDictionary<string, byte> users = new();

    // TryAdd checks and adds in one step, so two requests can't both get in
    public bool TryStart(string userId) => users.TryAdd(userId, 0);

    public void Finish(string userId) => users.TryRemove(userId, out _);
}
