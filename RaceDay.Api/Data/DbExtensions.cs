using Microsoft.EntityFrameworkCore;

namespace RaceDay.Api.Data;

public static class DbExtensions
{
    public static Task<int?> OrganiserOfEventAsync(this RaceDayDbContext db, int eventId) =>
        db.Events.Where(e => e.EventId == eventId).Select(e => (int?)e.OrganiserId).FirstOrDefaultAsync();
}
