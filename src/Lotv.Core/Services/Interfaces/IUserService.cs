using Lotv.Core.Common;
using Lotv.Core.Models;

namespace Lotv.Core.Services.Interfaces;

public interface IUserService
{
    Task<ApplicationUser?> GetUserAsync(string userId);
    Task<List<ApplicationUser>> GetUsersAsync(int? chapterId = null);
    Task<Result> UpdateProfileAsync(string userId, string firstName, string lastName, string email);
    Task<Result> AssignRoleAsync(string userId, UserRole role, int? chapterId);
    Task<Result> DeactivateAsync(string userId);
}
