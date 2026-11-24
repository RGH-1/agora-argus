using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Web.Tests;

public class AgoraRulesTests
{
    [Fact]
    public async Task Create_validates_trims_and_places_new_rules_at_the_end()
    {
        await using var fixture = await RuleFixture.CreateAsync();
        await fixture.Service.CreateAsync(" First ", " First content ");
        var second = await fixture.Service.CreateAsync("Second", "Second content");

        var rules = await fixture.Service.GetOrderedAsync();

        Assert.Equal(["First", "Second"], rules.Select(rule => rule.Title));
        Assert.Equal(2, second.DisplayOrder);
        await Assert.ThrowsAsync<RuleBusinessRuleException>(() => fixture.Service.CreateAsync(" ", "Content"));
        await Assert.ThrowsAsync<RuleBusinessRuleException>(() => fixture.Service.CreateAsync(new string('x', 151), "Content"));
        await Assert.ThrowsAsync<RuleBusinessRuleException>(() => fixture.Service.CreateAsync("Title", " "));
        await Assert.ThrowsAsync<RuleBusinessRuleException>(() => fixture.Service.CreateAsync("Title", new string('x', 2001)));
    }

    [Fact]
    public async Task Maximum_lengths_are_accepted_but_invalid_create_and_edit_never_persist()
    {
        await using var fixture = await RuleFixture.CreateAsync();
        var rule = await fixture.Service.CreateAsync(new string('t', 150), new string('c', 2000));
        Assert.Equal(1, await fixture.Context.Rules.CountAsync());

        await Assert.ThrowsAsync<RuleBusinessRuleException>(() => fixture.Service.CreateAsync(new string('t', 151), "Content"));
        Assert.Equal(1, await fixture.Context.Rules.CountAsync());

        await Assert.ThrowsAsync<RuleBusinessRuleException>(() => fixture.Service.UpdateAsync(rule.Id, " ", new string('c', 2001)));
        var persisted = await fixture.Service.GetByIdAsync(rule.Id);
        Assert.Equal(new string('t', 150), persisted!.Title);
        Assert.Equal(new string('c', 2000), persisted.Content);
    }

    [Fact]
    public void Rule_forms_expose_counter_fields_without_browser_length_truncation()
    {
        var form = ReadWorkspaceFile("src", "Agora.Web", "Areas", "Moderation", "Views", "Rules", "_RuleForm.cshtml");
        var publicIndex = ReadWorkspaceFile("src", "Agora.Web", "Views", "Rules", "Index.cshtml");
        var moderationIndex = ReadWorkspaceFile("src", "Agora.Web", "Areas", "Moderation", "Views", "Rules", "Index.cshtml");

        Assert.Contains("data-length-form", form);
        Assert.Contains("data-limit=\"150\"", form);
        Assert.Contains("data-limit=\"2000\"", form);
        Assert.DoesNotContain("maxlength", form.ToLowerInvariant());
        Assert.Contains("rule-content", publicIndex);
        Assert.DoesNotContain("Html.Raw", publicIndex);
        Assert.Contains("rule-preview", moderationIndex);
    }

    [Fact]
    public async Task Edit_preserves_creation_and_display_order_and_updates_changed_content()
    {
        await using var fixture = await RuleFixture.CreateAsync();
        var rule = await fixture.Service.CreateAsync("Original", "Original content");
        var created = rule.CreatedAtUtc; var order = rule.DisplayOrder;

        await fixture.Service.UpdateAsync(rule.Id, " Updated ", " Updated content ");
        var updated = await fixture.Service.GetByIdAsync(rule.Id);

        Assert.NotNull(updated);
        Assert.Equal("Updated", updated!.Title); Assert.Equal("Updated content", updated.Content);
        Assert.Equal(created, updated.CreatedAtUtc); Assert.Equal(order, updated.DisplayOrder);
        Assert.True(updated.UpdatedAtUtc >= created);
    }

    [Fact]
    public async Task Move_and_delete_keep_unique_contiguous_order_without_boundary_changes()
    {
        await using var fixture = await RuleFixture.CreateAsync();
        var first = await fixture.Service.CreateAsync("First", "One");
        var second = await fixture.Service.CreateAsync("Second", "Two");
        var third = await fixture.Service.CreateAsync("Third", "Three");

        Assert.Equal(RuleMoveResult.Boundary, await fixture.Service.MoveAsync(first.Id, -1));
        Assert.Equal(RuleMoveResult.Moved, await fixture.Service.MoveAsync(third.Id, -1));
        Assert.Equal(["First", "Third", "Second"], (await fixture.Service.GetOrderedAsync()).Select(rule => rule.Title));
        Assert.Equal(RuleMoveResult.Boundary, await fixture.Service.MoveAsync(second.Id, 1));
        await fixture.Service.DeleteAsync(third.Id);

        var rules = await fixture.Service.GetOrderedAsync();
        Assert.Equal(["First", "Second"], rules.Select(rule => rule.Title));
        Assert.Equal([1, 2], rules.Select(rule => rule.DisplayOrder));
        Assert.Equal(2, await fixture.Context.Rules.Select(rule => rule.DisplayOrder).Distinct().CountAsync());
    }

    [Fact]
    public async Task Missing_rules_are_reported_without_changing_other_rules()
    {
        await using var fixture = await RuleFixture.CreateAsync();
        var rule = await fixture.Service.CreateAsync("Existing", "Content");

        await Assert.ThrowsAsync<RuleNotFoundException>(() => fixture.Service.UpdateAsync(999, "No", "No"));
        await Assert.ThrowsAsync<RuleNotFoundException>(() => fixture.Service.DeleteAsync(999));
        Assert.Equal(RuleMoveResult.NotFound, await fixture.Service.MoveAsync(999, 1));
        Assert.NotNull(await fixture.Service.GetByIdAsync(rule.Id));
    }

    [Fact]
    public void Public_rules_are_anonymous_but_management_mutations_are_role_authorized_and_post_only()
    {
        Assert.NotEmpty(typeof(Agora.Web.Controllers.RulesController).GetCustomAttributes(typeof(AllowAnonymousAttribute), true));
        var controller = typeof(Agora.Web.Areas.Moderation.Controllers.RulesController);
        var authorization = controller.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        Assert.Equal("Moderator,Admin", authorization.Roles);
        foreach (var name in new[] { "Create", "Edit", "Delete", "MoveUp", "MoveDown" })
            Assert.Contains(controller.GetMethods(), method => (method.Name == name || method.GetCustomAttributes(typeof(ActionNameAttribute), true).Cast<ActionNameAttribute>().Any(attribute => attribute.Name == name)) && method.GetCustomAttributes(typeof(HttpPostAttribute), true).Any() && method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).Any());
    }

    private static string ReadWorkspaceFile(params string[] relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativePath]);
            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }
        }

        throw new FileNotFoundException($"Could not find workspace file: {Path.Combine(relativePath)}");
    }

    private sealed class RuleFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection; private readonly ServiceProvider provider; private readonly IServiceScope scope;
        private RuleFixture(SqliteConnection connection, ServiceProvider provider) { this.connection = connection; this.provider = provider; scope = provider.CreateScope(); Context = scope.ServiceProvider.GetRequiredService<AgoraDbContext>(); Service = scope.ServiceProvider.GetRequiredService<IRuleService>(); }
        public AgoraDbContext Context { get; } public IRuleService Service { get; }
        public static async Task<RuleFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var services = new ServiceCollection(); services.AddDbContext<AgoraDbContext>(options => options.UseSqlite(connection));
            services.AddScoped<IRuleRepository, RuleRepository>(); services.AddScoped<IRuleService, RuleService>();
            var fixture = new RuleFixture(connection, services.BuildServiceProvider()); await fixture.Context.Database.EnsureCreatedAsync(); return fixture;
        }
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); scope.Dispose(); await provider.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
