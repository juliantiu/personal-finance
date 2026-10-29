namespace PersonalFinance.Api.Accounts;

public static class AccountEndpointAvailability
{
    public static bool IsEnabled(bool identityDatabaseConfigured, bool emailSenderConfigured)
        => identityDatabaseConfigured && emailSenderConfigured;
}
