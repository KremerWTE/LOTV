using Lotv.Core.Common;
using Lotv.Core.Models;
using Lotv.Core.Reporting;

namespace Lotv.Core.Services.Interfaces;

public interface IVolunteerService
{
    Task<List<Volunteer>> GetVolunteersAsync(int chapterId);
    Task<Volunteer?> GetVolunteerAsync(int id);
    Task<List<Volunteer>> GetAvailableAsync(int chapterId, int requestId);
    Task<Result<Volunteer>> RegisterAsync(Volunteer volunteer);
    Task<Result> UpdateAsync(Volunteer volunteer);
    Task<List<VolunteerScoreResult>> ScoreVolunteersAsync(int requestId);
}
