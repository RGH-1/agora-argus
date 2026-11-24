using Agora.Web.Data.Entities;
using Agora.Web.Repositories;

namespace Agora.Web.Services;

public sealed class RuleService(IRuleRepository ruleRepository) : IRuleService
{
    public Task<IReadOnlyList<Rule>> GetOrderedAsync(CancellationToken cancellationToken = default) => ruleRepository.GetOrderedAsync(cancellationToken);
    public Task<Rule?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => ruleRepository.GetByIdAsync(id, cancellationToken);
    public Task<Rule> CreateAsync(string title, string content, CancellationToken cancellationToken = default) { Validate(title, content); return ruleRepository.CreateAsync(title.Trim(), content.Trim(), cancellationToken); }
    public async Task UpdateAsync(int id, string title, string content, CancellationToken cancellationToken = default) { Validate(title, content); if (!await ruleRepository.UpdateAsync(id, title.Trim(), content.Trim(), cancellationToken)) throw new RuleNotFoundException(); }
    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default) { if (!await ruleRepository.DeleteAsync(id, cancellationToken)) throw new RuleNotFoundException(); }
    public Task<RuleMoveResult> MoveAsync(int id, int direction, CancellationToken cancellationToken = default) => ruleRepository.MoveAsync(id, direction, cancellationToken);
    private static void Validate(string title, string content)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new RuleBusinessRuleException("A rule title is required.");
        if (title.Trim().Length > 150) throw new RuleBusinessRuleException("Rule titles cannot exceed 150 characters.");
        if (string.IsNullOrWhiteSpace(content)) throw new RuleBusinessRuleException("Rule content is required.");
        if (content.Trim().Length > 2000) throw new RuleBusinessRuleException("Rule content cannot exceed 2000 characters.");
    }
}
public sealed class RuleBusinessRuleException(string message) : Exception(message);
public sealed class RuleNotFoundException : Exception;
