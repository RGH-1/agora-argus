using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Repositories;

public class ForumRepository(AgoraDbContext dbContext) : IForumRepository
{
    public Task<Forum?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Forums.Include(forum => forum.Creator).FirstOrDefaultAsync(forum => forum.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Forum>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Forums.AsNoTracking().OrderBy(forum => forum.Name).ToListAsync(cancellationToken);

    public async Task AddAsync(Forum forum, CancellationToken cancellationToken = default)
    {
        await dbContext.Forums.AddAsync(forum, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Forum forum, CancellationToken cancellationToken = default)
    {
        forum.UpdatedAtUtc = DateTime.UtcNow;
        dbContext.Forums.Update(forum);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
