using Lotv.Core.Models;
using Microsoft.AspNetCore.Identity;

namespace Lotv.Api.Data;

/// <summary>
/// Self-healing startup step for the small set of named HQ staff accounts.
/// Unlike DevSeedData (Development-only, full mock dataset), this runs on
/// EVERY startup in EVERY environment: it only touches the Role of accounts
/// that already exist and whose username is on this known-admin list — it
/// never creates, deletes, or touches passwords/emails of any account.
///
/// Exists because the live production Role column was found reset to its
/// CLR default (UserRole.PublicUser — see LotvIdentityUser.Role) for both
/// mary.roberts and chris.kremer on 2026-09-22, locking every account out of
/// /admin/users (PUT /api/v1/users/{id}/role requires an already-HQAdmin/
/// ChapterAdmin/Director caller — a closed loop with no admin account left).
/// This repair breaks that loop on the next deploy without needing direct
/// database access.
/// </summary>
public static class CoreAdminAccountRepair
{
    // Matches the "Real staff accounts" list in DevSeedData.SeedLoginAccountsAsync.
    private static readonly string[] KnownHqAdminUsernames =
    [
        "mary.roberts",
        "whitney.whitmore",
        "cynthia.destefano",
        "chris.kremer",
        "susan.harper",
        "admin",
        "tech",
    ];

    public static async Task RepairAsync(UserManager<LotvIdentityUser> userMgr, ILogger logger)
    {
        foreach (var username in KnownHqAdminUsernames)
        {
            var user = await userMgr.FindByNameAsync(username);
            if (user is null) continue;

            if (user.Role != UserRole.HQAdmin || user.ChapterId is not null)
            {
                logger.LogWarning(
                    "CoreAdminAccountRepair: {Username} had Role={OldRole} ChapterId={OldChapterId} — correcting to HQAdmin/null.",
                    username, user.Role, user.ChapterId);
                user.Role = UserRole.HQAdmin;
                user.ChapterId = null;
                await userMgr.UpdateAsync(user);
            }
        }
    }
}
