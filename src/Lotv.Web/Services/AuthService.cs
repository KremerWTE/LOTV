namespace Lotv.Web.Services;

public class AuthService
{
    // Demo credentials — in production these come from Identity/JWT
    private static readonly (string Email, string Password, string Name, string Role)[] _users =
    [
        ("mary.roberts@lotvministry.org",   "lotv2026!", "Mary Roberts",   "Administrator"),
        ("anne.collins@lotvministry.org",   "lotv2026!", "Anne Collins",   "Case Manager"),
        ("david.kim@lotvministry.org",      "lotv2026!", "David Kim",      "Finance"),
        ("sara.mitchell@lotvministry.org",  "lotv2026!", "Sara Mitchell",  "Volunteer Coordinator"),
    ];

    public bool IsAuthenticated { get; private set; }
    public string UserName      { get; private set; } = "";
    public string UserRole      { get; private set; } = "";
    public string UserInitials  => UserName.Length > 0
        ? string.Concat(UserName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 0).Take(2).Select(p => p[0]))
        : "?";

    public event Action? OnChange;

    public bool Login(string email, string password)
    {
        var match = _users.FirstOrDefault(u =>
            u.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase) &&
            u.Password == password);

        if (match == default) return false;

        IsAuthenticated = true;
        UserName        = match.Name;
        UserRole        = match.Role;
        OnChange?.Invoke();
        return true;
    }

    public void Logout()
    {
        IsAuthenticated = false;
        UserName        = "";
        UserRole        = "";
        OnChange?.Invoke();
    }
}
