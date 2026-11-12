using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;

namespace PersonalFinance.Infrastructure.Profiles;

internal static class EfProfileCategoryWriteLock
{
    internal static async Task AcquireAsync(AccountIdentityDbContext database, Guid categoryId,
        CancellationToken cancellationToken)
    {
        var lockName = $"profile-category:{categoryId:N}";
        var returnCode = new SqlParameter("@returnCode", SqlDbType.Int) { Direction = ParameterDirection.Output };
        await database.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC {returnCode} = sys.sp_getapplock @Resource = {lockName}, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000;",
            cancellationToken);
        if (returnCode.Value is int result && result < 0)
            throw new TimeoutException("The category is currently being updated. Retry the request.");
    }
}
