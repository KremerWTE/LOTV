using Lotv.Core.Models;
using Microsoft.AspNetCore.Identity;

namespace Lotv.Api.Data;

/// <summary>
/// Makes sure the named staff accounts exist, in every environment. Unlike the dev seed this creates accounts in
/// production, so it never sets a password anyone knows: the account starts with a random one that is thrown away,
/// and the person chooses their own through "Forgot password", which emails a reset link to their address.
/// An account that already exists is left alone (CoreAdminAccountRepair keeps its role correct).
/// </summary>
public static class StaffAccountProvisioning
{
    public record StaffAccount(string UserName, string Email, string FirstName, string LastName);

    public static readonly StaffAccount[] Accounts =
    [
        new("susan.harper", "susan@wte.net", "Susan", "Harper"),
    ];

    public static async Task<int> EnsureAsync(UserManager<LotvIdentityUser> userMgr, ILogger logger)
    {
        var created = 0;
        foreach (var a in Accounts)
        {
            if (await userMgr.FindByNameAsync(a.UserName) is not null) continue;
            if (await FindByEmailSafeAsync(userMgr, a.Email) is not null) continue;

            var user = new LotvIdentityUser
            {
                UserName = a.UserName, Email = a.Email, EmailConfirmed = true,
                FirstName = a.FirstName, LastName = a.LastName,
                Role = UserRole.HQAdmin, ChapterId = null, IsActive = true,
            };
            // Long, random and never shown or stored: the only way in is the emailed reset link.
            var throwAway = $"Aa1!{Guid.NewGuid():N}{Guid.NewGuid():N}";
            var result = await userMgr.CreateAsync(user, throwAway);
            if (result.Succeeded)
            {
                created++;
                logger.LogInformation("Created staff account {UserName} ({Email}) as HQAdmin; they set their password with Forgot password.", a.UserName, a.Email);
            }
            else
            {
                logger.LogWarning("Could not create staff account {UserName}: {Errors}", a.UserName, string.Join("; ", result.Errors.Select(e => e.Description)));
            }
        }
        return created;
    }

    public static async Task<LotvIdentityUser?> FindByEmailSafeAsync(UserManager<LotvIdentityUser> userMgr, string email)
    {
        try { return await userMgr.FindByEmailAsync(email); }
        catch (InvalidOperationException) { return null; }   // more than one account shares the address
    }
}
