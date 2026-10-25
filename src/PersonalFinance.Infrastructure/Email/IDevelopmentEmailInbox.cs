namespace PersonalFinance.Infrastructure.Email;

public interface IDevelopmentEmailInbox
{
    IReadOnlyList<DevelopmentEmailMessage> ReadAll();

    void Add(DevelopmentEmailMessage message);
}
