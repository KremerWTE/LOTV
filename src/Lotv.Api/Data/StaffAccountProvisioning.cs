using Lotv.Core.Models;
using Microsoft.AspNetCore.Identity;

namespace Lotv.Api.Data;

/// <summary>
/// Makes sure the named staff accounts exist, in every environment. Unlike the dev seed this creates accounts in
/// production, so no password is ever written in the code. By default the account starts with a random password that
/// is thrown away and the person chooses their own through "Forgot password". Alternatively a starting password can be
/// supplied through deployment configuration (StaffAccounts:InitialPasswords:{username with dots as underscores}, fed from
/// a secret). That starting password is only applied while the person has never signed in, so it can never override
/// a password they chose themselves. An account that already exists is otherwise left alone
/// (CoreAdminAccountRepair keeps its role correct).
/// </summary>
public static class StaffAccountProvisioning
{
    public record StaffAccount(string UserName, string Email, string FirstName, string LastName);

    public static readonly StaffAccount[] Accounts =
    [
        new("susan.harper", "susan@wte.net", "Susan", "Harper"),
    ];

    public static string InitialPasswordKey(string userName) => $"StaffAccounts:InitialPasswords:{userName.Replace('.', '_')}";

    public static async Task<int> EnsureAsync(UserManager<LotvIdentityUser> userMgr, ILogger logger, IConfiguration? config = null)
    {
        var created = 0;
        foreach (var a in Accounts)
        {
            var initial = config?[InitialPasswordKey(a.UserName)];
            if (string.IsNullOrWhiteSpace(initial)) initial = null;

            var existing = await userMgr.FindByNameAsync(a.UserName);
            if (existing is not null)
            {
                // Hand over the starting password, but only to someone who has never signed in.
                if (initial is not null && existing.LastLoginAt is null && !await userMgr.CheckPasswordAsync(existing, initial))
                {
                    var token = await userMgr.GeneratePasswordResetTokenAsync(existing);
                    var reset = await userMgr.ResetPasswordAsync(existing, token, initial);
                    if (reset.Succeeded) logger.LogInformation("Set the starting password for staff account {UserName}.", a.UserName);
                    else logger.LogWarning("Could not set the starting password for {UserName}: {Errors}", a.UserName, string.Join("; ", reset.Errors.Select(e => e.Description)));
                }
                continue;
            }
            if (await FindByEmailSafeAsync(userMgr, a.Email) is not null) continue;

            var user = new LotvIdentityUser
            {
                UserName = a.UserName, Email = a.Email, EmailConfirmed = true,
                FirstName = a.FirstName, LastName = a.LastName,
                Role = UserRole.HQAdmin, ChapterId = null, IsActive = true,
            };
            // Either the starting password supplied by configuration, or a long random one that is never shown or stored
            // (then the only way in is the emailed reset link).
            var password = initial ?? $"Aa1!{Guid.NewGuid():N}{Guid.NewGuid():N}";
            var result = await userMgr.CreateAsync(user, password);
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
