using IsolationLevel = System.Data.IsolationLevel;
using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Agora.Web.Repositories;

public sealed class RuleRepository(AgoraDbContext dbContext) : IRuleRepository
{
    public async Task<IReadOnlyList<Rule>> GetOrderedAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Rules.AsNoTracking().OrderBy(rule => rule.DisplayOrder).ThenBy(rule => rule.Id).ToListAsync(cancellationToken);

    public Task<Rule?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Rules.AsNoTracking().FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken);

    public async Task<Rule> CreateAsync(string title, string content, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var nextOrder = (await dbContext.Rules.MaxAsync(rule => (int?)rule.DisplayOrder, cancellationToken) ?? 0) + 1;
        var now = DateTime.UtcNow;
        var rule = new Rule { Title = title, Content = content, DisplayOrder = nextOrder, CreatedAtUtc = now, UpdatedAtUtc = now };
        dbContext.Rules.Add(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rule;
    }

    public async Task<bool> UpdateAsync(int id, string title, string content, CancellationToken cancellationToken = default)
    {
        var rule = await dbContext.Rules.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (rule is null) return false;
        if (rule.Title == title && rule.Content == content) return true;
        rule.Title = title;
        rule.Content = content;
        rule.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var rule = await dbContext.Rules.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (rule is null) return false;
        var deletedOrder = rule.DisplayOrder;
        dbContext.Rules.Remove(rule);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Rules.Where(item => item.DisplayOrder > deletedOrder)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DisplayOrder, item => -item.DisplayOrder), cancellationToken);
        await dbContext.Rules.Where(item => item.DisplayOrder < 0)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.DisplayOrder, item => -item.DisplayOrder - 1), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        dbContext.ChangeTracker.Clear();
        return true;
    }

    public async Task<RuleMoveResult> MoveAsync(int id, int direction, CancellationToken cancellationToken = default)
    {
        if (direction is not (-1 or 1)) throw new ArgumentOutOfRangeException(nameof(direction));
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var rules = await dbContext.Rules.OrderBy(rule => rule.DisplayOrder).ThenBy(rule => rule.Id).ToListAsync(cancellationToken);
        var index = rules.FindIndex(rule => rule.Id == id);
        if (index < 0) return RuleMoveResult.NotFound;
        var targetIndex = index + direction;
        if (targetIndex < 0 || targetIndex >= rules.Count) return RuleMoveResult.Boundary;
        var rule = rules[index];
        var neighbor = rules[targetIndex];
        var ruleOrder = rule.DisplayOrder;
        var neighborOrder = neighbor.DisplayOrder;
        var temporaryOrder = int.MinValue + rule.Id;
        rule.DisplayOrder = temporaryOrder;
        await dbContext.SaveChangesAsync(cancellationToken);
        neighbor.DisplayOrder = ruleOrder;
        await dbContext.SaveChangesAsync(cancellationToken);
        rule.DisplayOrder = neighborOrder;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return RuleMoveResult.Moved;
    }
}
