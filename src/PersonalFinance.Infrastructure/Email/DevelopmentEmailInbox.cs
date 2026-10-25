using System.Collections.Concurrent;

namespace PersonalFinance.Infrastructure.Email;

public sealed class DevelopmentEmailInbox : IDevelopmentEmailInbox
{
    private readonly ConcurrentQueue<DevelopmentEmailMessage> messages = new();

    public IReadOnlyList<DevelopmentEmailMessage> ReadAll() => messages.ToArray();

    public void Add(DevelopmentEmailMessage message) => messages.Enqueue(message);
}
