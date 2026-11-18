using Microsoft.EntityFrameworkCore;
using PersonalFinance.Application.Profiles;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Profiles;

public sealed class EfProfileParticipantsReader(AccountIdentityDbContext database) : IProfileParticipantsReader
{
    public async Task<IReadOnlyList<ProfileParticipant>?> ListAsync(Guid accountId, Guid profileId, CancellationToken cancellationToken)
    {
        var isOwner = await database.BudgetProfiles.AsNoTracking()
            .AnyAsync(profile => profile.Id == profileId && profile.OwnerAccountId == accountId, cancellationToken);
        if (!isOwner) return null;
        return await database.ProfileAccess.AsNoTracking()
            .Where(access => access.ProfileId == profileId)
            .Join(database.Users.AsNoTracking(), access => access.AccountId, user => user.Id,
                (access, user) => new { user.Email, access.Role })
            .OrderBy(participant => participant.Email)
            .Select(participant => new ProfileParticipant(participant.Email!, participant.Role))
            .ToListAsync(cancellationToken);
    }
}
