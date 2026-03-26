namespace Lotv.Tests.Integration;

// ── Response DTO mirrors the API's LoginResponse private record ───────────────
internal record LoginResponseDto(string AccessToken, string RefreshToken, string Role, int? ChapterId);

// ── Minimal response shapes for resource endpoints ───────────────────────────
internal record FamilyResponseDto(int Id);
internal record RequestResponseDto(int Id, string Status);
internal record DashboardStatsDto(int OpenCases, int Overdue, decimal DonationsThisMonth, decimal DonationsLastMonth, int ActiveVolunteers);
internal record NoteResponseDto(int Id, string Content, bool IsInternal);
internal record VolunteerResponseDto(int Id, string FirstName, string LastName);
internal record DonorResponseDto(int Id, string FirstName, string LastName);
internal record DonationResponseDto(int Id, decimal Amount);
