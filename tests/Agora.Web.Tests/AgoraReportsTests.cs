using System.ComponentModel.DataAnnotations;
using Agora.Web.Controllers;
using Agora.Web.Data;
using Agora.Web.Data.Entities;
using Agora.Web.Models.Reports;
using Agora.Web.Repositories;
using Agora.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Agora.Web.Tests;

public class AgoraReportsTests
{
    [Fact]
    public async Task Member_can_report_another_users_visible_post_with_open_status_and_timestamp()
    {
        await using var fixture = await ReportFixture.CreateAsync();
        var author = await fixture.UserAsync("author"); var reporter = await fixture.UserAsync("reporter");
        var post = await fixture.PublicPostAsync(author.Id);

        var report = await fixture.Service.CreateAsync(post.Id, "Spam or misleading content", " More details ", reporter.Id);

        Assert.Equal(post.Id, report.PostId);
        Assert.Equal(reporter.Id, report.ReportingUserId);
        Assert.Equal("Spam or misleading content: More details", report.Reason);
        Assert.Equal(ReportStatus.Open, report.Status);
        Assert.Equal(DateTimeKind.Utc, report.CreatedAtUtc.Kind);
        Assert.Single(await fixture.Context.Reports.ToListAsync());
    }

    [Fact]
    public async Task Valid_reason_with_empty_or_whitespace_details_creates_a_category_only_report()
    {
        await using var fixture = await ReportFixture.CreateAsync();
        var author = await fixture.UserAsync("author"); var reporter = await fixture.UserAsync("reporter");
        var firstPost = await fixture.PublicPostAsync(author.Id);
        var secondPost = await fixture.PublicPostAsync(author.Id);

        var empty = await fixture.Service.CreateAsync(firstPost.Id, "Other", null, reporter.Id);
        var whitespace = await fixture.Service.CreateAsync(secondPost.Id, "Other", "   ", reporter.Id);

        Assert.Equal("Other", empty.Reason);
        Assert.Equal("Other", whitespace.Reason);
        Assert.Equal(ReportStatus.Open, empty.Status);
        Assert.Equal(ReportStatus.Open, whitespace.Status);
    }

    [Fact]
    public async Task Second_open_report_from_the_same_member_is_rejected_without_creating_a_record()
    {
        await using var fixture = await ReportFixture.CreateAsync();
        var author = await fixture.UserAsync("author"); var reporter = await fixture.UserAsync("reporter");
        var post = await fixture.PublicPostAsync(author.Id);
        await fixture.Service.CreateAsync(post.Id, "Other", null, reporter.Id);

        var exception = await Assert.ThrowsAsync<OpenReportAlreadyExistsException>(() => fixture.Service.CreateAsync(post.Id, "Spam or misleading content", null, reporter.Id));

        Assert.Equal(OpenReportAlreadyExistsException.FriendlyMessage, exception.Message);
        Assert.Single(await fixture.Context.Reports.Where(report => report.PostId == post.Id && report.ReportingUserId == reporter.Id).ToListAsync());
    }

    [Fact]
    public async Task Different_members_can_report_the_same_post_and_reviewed_or_dismissed_reports_do_not_block_a_new_open_report()
    {
        await using var fixture = await ReportFixture.CreateAsync();
        var author = await fixture.UserAsync("author"); var first = await fixture.UserAsync("first"); var second = await fixture.UserAsync("second");
        var sharedPost = await fixture.PublicPostAsync(author.Id);
        var reviewedPost = await fixture.PublicPostAsync(author.Id);
        var dismissedPost = await fixture.PublicPostAsync(author.Id);

        await fixture.Service.CreateAsync(sharedPost.Id, "Other", null, first.Id);
        await fixture.Service.CreateAsync(sharedPost.Id, "Other", null, second.Id);
        fixture.Context.Reports.AddRange(
            new Report { PostId = reviewedPost.Id, ReportingUserId = first.Id, Reason = "Reviewed", Status = ReportStatus.Reviewed },
            new Report { PostId = dismissedPost.Id, ReportingUserId = first.Id, Reason = "Dismissed", Status = ReportStatus.Dismissed });
        await fixture.Context.SaveChangesAsync();

        var afterReviewed = await fixture.Service.CreateAsync(reviewedPost.Id, "Other", null, first.Id);
        var afterDismissed = await fixture.Service.CreateAsync(dismissedPost.Id, "Other", null, first.Id);

        Assert.Equal(2, await fixture.Context.Reports.CountAsync(report => report.PostId == sharedPost.Id));
        Assert.Equal(ReportStatus.Open, afterReviewed.Status);
        Assert.Equal(ReportStatus.Open, afterDismissed.Status);
    }

    [Fact]
    public async Task Filtered_unique_index_rejects_concurrent_open_duplicate_and_repository_handles_it()
    {
        await using var fixture = await ReportFixture.CreateAsync();
        var author = await fixture.UserAsync("author"); var reporter = await fixture.UserAsync("reporter");
        var post = await fixture.PublicPostAsync(author.Id);
        await fixture.Service.CreateAsync(post.Id, "Other", null, reporter.Id);

        var result = await fixture.Repository.AddAsync(new Report { PostId = post.Id, ReportingUserId = reporter.Id, Reason = "Race", Status = ReportStatus.Open });

        Assert.Equal(ReportAddResult.DuplicateOpenReport, result);
        Assert.Single(await fixture.Context.Reports.Where(report => report.PostId == post.Id && report.ReportingUserId == reporter.Id && report.Status == ReportStatus.Open).ToListAsync());
    }

    [Fact]
    public async Task Own_missing_removed_pending_and_rejected_posts_cannot_be_reported()
    {
        await using var fixture = await ReportFixture.CreateAsync();
        var author = await fixture.UserAsync("author"); var reporter = await fixture.UserAsync("reporter");
        var own = await fixture.PublicPostAsync(author.Id);
        var removed = await fixture.PublicPostAsync(author.Id); removed.IsRemoved = true;
        var pending = await fixture.PostAsync((await fixture.ForumAsync(ForumStatus.Pending, author.Id)).Id, author.Id, "Pending");
        var rejected = await fixture.PostAsync((await fixture.ForumAsync(ForumStatus.Rejected, author.Id)).Id, author.Id, "Rejected");
        await fixture.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<ReportBusinessRuleException>(() => fixture.Service.CreateAsync(own.Id, "Other", null, author.Id));
        foreach (var postId in new[] { 999, removed.Id, pending.Id, rejected.Id })
            await Assert.ThrowsAsync<ReportNotFoundOrForbiddenException>(() => fixture.Service.CreateAsync(postId, "Other", null, reporter.Id));
        Assert.Empty(await fixture.Context.Reports.ToListAsync());
    }

    [Fact]
    public async Task Invalid_categories_and_overlong_details_are_rejected_without_truncation()
    {
        await using var fixture = await ReportFixture.CreateAsync();
        var author = await fixture.UserAsync("author"); var reporter = await fixture.UserAsync("reporter");
        var post = await fixture.PublicPostAsync(author.Id);
        var tooLong = new string('x', CreateReportViewModel.DetailsMaximumLength + 1);

        await Assert.ThrowsAsync<ReportBusinessRuleException>(() => fixture.Service.CreateAsync(post.Id, "Invalid", null, reporter.Id));
        await Assert.ThrowsAsync<ReportBusinessRuleException>(() => fixture.Service.CreateAsync(post.Id, "Other", tooLong, reporter.Id));
        Assert.Equal(CreateReportViewModel.DetailsMaximumLength + 1, tooLong.Length);
        var invalid = new CreateReportViewModel { Category = "", Details = tooLong };
        Assert.False(Validator.TryValidateObject(invalid, new ValidationContext(invalid), [], true));
        Assert.Empty(await fixture.Context.Reports.ToListAsync());
    }

    [Fact]
    public async Task Open_queue_includes_only_open_reports_with_post_and_reporter()
    {
        await using var fixture = await ReportFixture.CreateAsync();
        var author = await fixture.UserAsync("author"); var reporter = await fixture.UserAsync("reporter");
        var post = await fixture.PublicPostAsync(author.Id);
        var open = await fixture.Service.CreateAsync(post.Id, "Other", null, reporter.Id);
        fixture.Context.Reports.Add(new Report { PostId = post.Id, ReportingUserId = reporter.Id, Reason = "Legacy", Status = ReportStatus.Pending });
        await fixture.Context.SaveChangesAsync();

        var queue = await fixture.Service.GetOpenAsync();

        var report = Assert.Single(queue);
        Assert.Equal(open.Id, report.Id);
        Assert.Equal(post.Title, report.Post.Title);
        Assert.Equal(reporter.DisplayName, report.ReportingUser.DisplayName);
    }

    [Fact]
    public void Create_endpoints_and_queue_are_authorized_and_post_is_antiforgery_protected()
    {
        Assert.NotEmpty(typeof(ReportsController).GetCustomAttributes(typeof(AuthorizeAttribute), true));
        var createPost = typeof(ReportsController).GetMethods().Single(method => method.Name == "Create" && method.GetCustomAttributes(typeof(HttpPostAttribute), true).Any());
        Assert.NotNull(createPost.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
        Assert.DoesNotContain(createPost.GetParameters(), parameter => parameter.Name?.Contains("user", StringComparison.OrdinalIgnoreCase) == true);

        var queueController = typeof(Agora.Web.Areas.Moderation.Controllers.ReportsController);
        var authorization = queueController.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        Assert.Equal("Moderator,Admin", authorization.Roles);
    }

    private sealed class ReportFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection; private readonly ServiceProvider provider; private readonly IServiceScope scope;
        private ReportFixture(SqliteConnection connection, ServiceProvider provider) { this.connection = connection; this.provider = provider; scope = provider.CreateScope(); Context = scope.ServiceProvider.GetRequiredService<AgoraDbContext>(); Service = scope.ServiceProvider.GetRequiredService<IReportService>(); Repository = scope.ServiceProvider.GetRequiredService<IReportRepository>(); }
        public AgoraDbContext Context { get; } public IReportService Service { get; } public IReportRepository Repository { get; }
        public static async Task<ReportFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var services = new ServiceCollection(); services.AddDbContext<AgoraDbContext>(options => options.UseSqlite(connection));
            services.AddScoped<IReportRepository, ReportRepository>(); services.AddScoped<IReportService, ReportService>();
            var fixture = new ReportFixture(connection, services.BuildServiceProvider()); await fixture.Context.Database.EnsureCreatedAsync(); return fixture;
        }
        public async Task<ApplicationUser> UserAsync(string name) { var user = new ApplicationUser { UserName = name, Email = $"{name}@agora.local", DisplayName = name }; Context.Users.Add(user); await Context.SaveChangesAsync(); return user; }
        public async Task<Forum> ForumAsync(ForumStatus status, string creatorId) { var suffix = Guid.NewGuid().ToString("N"); var forum = new Forum { Name = $"Forum {suffix}", NormalizedName = suffix.ToUpperInvariant(), Description = "Forum", Status = status, CreatorId = creatorId }; Context.Forums.Add(forum); await Context.SaveChangesAsync(); return forum; }
        public async Task<Post> PostAsync(int forumId, string authorId, string title) { var post = new Post { ForumId = forumId, AuthorId = authorId, Title = title, Content = "Content" }; Context.Posts.Add(post); await Context.SaveChangesAsync(); return post; }
        public async Task<Post> PublicPostAsync(string authorId) => await PostAsync((await ForumAsync(ForumStatus.Approved, authorId)).Id, authorId, "Public post");
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); scope.Dispose(); await provider.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
