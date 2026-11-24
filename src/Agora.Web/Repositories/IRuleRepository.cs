using Agora.Web.Data.Entities;

namespace Agora.Web.Repositories;

public interface IRuleRepository
{
    Task<IReadOnlyList<Rule>> GetOrderedAsync(CancellationToken cancellationToken = default);
    Task<Rule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Rule> CreateAsync(string title, string content, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, string title, string content, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<RuleMoveResult> MoveAsync(int id, int direction, CancellationToken cancellationToken = default);
}

public enum RuleMoveResult { Moved, NotFound, Boundary }
