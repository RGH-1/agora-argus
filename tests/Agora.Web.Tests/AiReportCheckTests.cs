using System.Text.Json;
using Agora.Web.Data.Entities;
using Agora.Web.Services;
using Agora.Web.Repositories;
using Agora.Web.Models.Moderation;
using Microsoft.Extensions.Logging.Abstractions;

namespace Agora.Web.Tests;

public class AiReportCheckTests
{
    [Fact]
    public void Prompt_uses_current_ordered_rules_post_and_saved_reason()
    {
        var report = new Report { Reason = "Reporter allegation", Post = new Post { Title = "Full title", Content = "Full post body" } };
        var rules = new[] { new Rule { Title = "Second current rule", Content = "Second text", DisplayOrder = 2 }, new Rule { Title = "First edited rule", Content = "First text", DisplayOrder = 1 } };
        var prompt = AiReportCheckService.BuildPrompt(report, rules);
        var root = JsonDocument.Parse(prompt).RootElement;
        Assert.Equal("Second current rule", root.GetProperty("rules")[0].GetProperty("Title").GetString());
        Assert.Equal("Full title", root.GetProperty("reportedPost").GetProperty("Title").GetString());
        Assert.Equal("Full post body", root.GetProperty("reportedPost").GetProperty("Content").GetString());
        Assert.Equal("Reporter allegation", root.GetProperty("reportReason").GetString());
    }

    [Fact]
    public void Parser_accepts_complete_valid_verdicts_and_rejects_invalid_data()
    {
        var violation = AiReportCheckService.Parse("{\"violates\":true,\"ruleBroken\":\"Rule A\",\"confidence\":0.93,\"explanation\":\"Clear explanation\"}", ["Rule A"]);
        var clear = AiReportCheckService.Parse("{\"violates\":false,\"ruleBroken\":null,\"confidence\":0.85,\"explanation\":\"No violation\"}", ["Rule A"]);
        Assert.True(violation.Violates); Assert.Equal("Rule A", violation.RuleBroken); Assert.False(clear.Violates);
        Assert.ThrowsAny<JsonException>(() => AiReportCheckService.Parse("not json", ["Rule A"]));
        Assert.Throws<JsonException>(() => AiReportCheckService.Parse("{\"violates\":true,\"ruleBroken\":\"Invented\",\"confidence\":1.2,\"explanation\":\"x\"}", ["Rule A"]));
        Assert.Throws<JsonException>(() => AiReportCheckService.Parse("{\"violates\":false,\"ruleBroken\":\"Rule A\",\"confidence\":0.5,\"explanation\":\"x\"}", ["Rule A"]));
    }

    [Fact]
    public async Task Oversized_serialized_verdict_and_provider_failures_preserve_existing_assessment()
    {
        var repository = new MemoryReports { Report = OpenReport("old", .2m, DateTime.UtcNow.AddDays(-1)) };
        var service = Service(repository, new FakeGemini { Text = Valid(new string('"', 3500)) });
        var result = await service.CheckAsync(1);
        Assert.False(result.Succeeded); Assert.Equal("old", repository.Report.AiAssessment); Assert.Equal(.2m, repository.Report.AiConfidence); Assert.Equal(0, repository.SaveCalls);
        foreach (var exception in new Exception[] { new OperationCanceledException(), new HttpRequestException() })
        {
            var failed = await Service(repository, new FakeGemini { Error = exception }).CheckAsync(1);
            Assert.False(failed.Succeeded); Assert.Equal("old", repository.Report.AiAssessment);
        }
    }

    [Fact]
    public async Task Closed_during_check_is_not_saved_and_success_keeps_status_and_actions_unchanged()
    {
        var repository = new MemoryReports { Report = OpenReport(null, null, null), CloseBeforeSave = true };
        Assert.False((await Service(repository, new FakeGemini { Text = Valid("ok") }).CheckAsync(1)).Succeeded);
        Assert.Null(repository.Report.AiAssessment); Assert.Equal(ReportStatus.Reviewed, repository.Report.Status);
        repository = new MemoryReports { Report = OpenReport(null, null, null) };
        var result = await Service(repository, new FakeGemini { Text = Valid("ok") }).CheckAsync(1);
        Assert.True(result.Succeeded); Assert.Equal(ReportStatus.Open, repository.Report.Status); Assert.NotNull(repository.Report.AiAssessment); Assert.Equal(.9m, repository.Report.AiConfidence);
    }

    private static AiReportCheckService Service(MemoryReports reports, FakeGemini gemini) => new(reports, new FakeRules(), gemini, NullLogger<AiReportCheckService>.Instance);
    private static Report OpenReport(string? assessment, decimal? confidence, DateTime? assessed) => new() { Id = 1, Status = ReportStatus.Open, Reason = "reason", AiAssessment = assessment, AiConfidence = confidence, AiAssessedAtUtc = assessed, Post = new Post { Title = "title", Content = "body" } };
    private static string Valid(string explanation) => $"{{\"violates\":false,\"ruleBroken\":null,\"confidence\":0.9,\"explanation\":{JsonSerializer.Serialize(explanation)}}}";

    private sealed class FakeGemini : IGeminiClient { public string? Text; public Exception? Error; public Task<string> GenerateAsync(string p, CancellationToken c = default) => GenerateJsonAsync(p,c); public Task<string> GenerateJsonAsync(string p, CancellationToken c = default) => Error is null ? Task.FromResult(Text!) : Task.FromException<string>(Error); }
    private sealed class FakeRules : IRuleService { public Task<IReadOnlyList<Rule>> GetOrderedAsync(CancellationToken c = default) => Task.FromResult<IReadOnlyList<Rule>>([new Rule { Title="Rule A", Content="Rule" }]); public Task<Rule?> GetByIdAsync(int i,CancellationToken c=default)=>Task.FromResult<Rule?>(null); public Task<Rule> CreateAsync(string a,string b,CancellationToken c=default)=>throw new NotImplementedException(); public Task UpdateAsync(int i,string a,string b,CancellationToken c=default)=>throw new NotImplementedException(); public Task DeleteAsync(int i,CancellationToken c=default)=>throw new NotImplementedException(); public Task<RuleMoveResult> MoveAsync(int i,int d,CancellationToken c=default)=>throw new NotImplementedException(); }
    private sealed class MemoryReports : IReportRepository
    {
        public Report Report = null!; public bool CloseBeforeSave; public int SaveCalls;
        public Task<Report?> GetOpenForAiCheckAsync(int id,CancellationToken c=default)=>Task.FromResult<Report?>(Report.Status==ReportStatus.Open?Report:null);
        public Task<bool> TrySaveAiAssessmentAsync(int id,string a,decimal confidence,DateTime at,CancellationToken c=default) { SaveCalls++; if(CloseBeforeSave) { Report.Status=ReportStatus.Reviewed; return Task.FromResult(false); } if(Report.Status!=ReportStatus.Open)return Task.FromResult(false); Report.AiAssessment=a;Report.AiConfidence=confidence;Report.AiAssessedAtUtc=at;return Task.FromResult(true); }
        public Task<Post?> GetPublicPostAsync(int a,CancellationToken c=default)=>throw new NotImplementedException(); public Task<bool> HasOpenReportAsync(int a,string b,CancellationToken c=default)=>throw new NotImplementedException(); public Task<ReportAddResult>AddAsync(Report a,CancellationToken c=default)=>throw new NotImplementedException(); public Task<IReadOnlyList<Report>>GetOpenAsync(CancellationToken c=default)=>throw new NotImplementedException(); public Task<Report?>GetOpenForModerationAsync(int a,CancellationToken c=default)=>throw new NotImplementedException(); public Task<ModerationResolutionResult>ResolveAsync(int a,string b,ModerationActionType d,string? e,CancellationToken c=default)=>throw new NotImplementedException(); public Task<IReadOnlyList<ModerationHistoryItemViewModel>>GetHistoryAsync(CancellationToken c=default)=>throw new NotImplementedException(); public Task<IReadOnlyList<UserWarning>>GetWarningsForUserAsync(string a,CancellationToken c=default)=>throw new NotImplementedException();
    }
}
