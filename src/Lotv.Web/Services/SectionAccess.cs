namespace Lotv.Web.Services;

public enum Section { Prayer, Cases, Admin, Analytics }

/// <summary>
/// Which staff-portal sections each role can see. Kept in one table so a policy change is a one-line edit.
///
/// Today: chris.kremer sees everything. Everyone else sees only the sections below —
///   Volunteer  → Prayer
///   Staff      → Prayer, Cases, Admin (the Admin items they have access to)
///   Admin      → Prayer, Cases, Admin
///   Board      → Prayer (view only) and Cases (read-only); their own Board Portal is separate
///
/// Planned, not switched on yet:
///   Admin → also Analytics
///   Board → also Analytics
/// (Analytics is hidden from everyone but chris.kremer until then.)
/// </summary>
public static class SectionAccess
{
    private static readonly string[] Admins = ["HQAdmin", "ChapterAdmin", "Director"];
    private static readonly string[] Staff  = ["HQAdmin", "ChapterAdmin", "Director", "ChapterStaff"];

    public static bool CanSee(Section section, string role, bool isChrisKremer)
    {
        if (isChrisKremer) return true;
        return section switch
        {
            Section.Prayer    => Staff.Contains(role) || role is "Volunteer" or "Board",
            Section.Cases     => Staff.Contains(role) || role == "Board",
            Section.Admin     => Admins.Contains(role),                         // the Admin pages themselves still check role
            Section.Analytics => false,                                         // later: Admins.Contains(role) || role == "Board"
            _ => false,
        };
    }
}
