using Agora.Web.Data.Entities;
using Agora.Web.Repositories;

namespace Agora.Web.Services;

public interface IRuleService
{
    Task<IReadOnlyList<Rule>> GetOrderedAsync(CancellationToken cancellationToken = default);
    Task<Rule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Rule> CreateAsync(string title, string content, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, string title, string content, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<RuleMoveResult> MoveAsync(int id, int direction, CancellationToken cancellationToken = default);
}
