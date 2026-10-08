using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Tasks;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.Persistence;

public sealed partial class TaskListApiTests
{
    private static async Task<HttpResponseMessage> SubmitResultAsync(HttpClient client, Guid task, string? key = null, object? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}/{task}/result") { Content = JsonContent.Create(body ?? new { content = "Deliverable" }) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> ConfirmResultAsync(HttpClient client, Guid task, string? key = null, object? body = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Endpoint}/{task}/confirmation") { Content = JsonContent.Create(body ?? new { decision = "CONFIRMED", note = "Approved" }) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Full_submission_rework_and_completion_lifecycle_persists_evidence_and_audits()
    {
        var a = await SeedAsync("MANAGER");
        await using var factory = new AuthenticationFactory(database); using var client = factory.CreateClient(); await LoginAsync(client, a);
        var initial = await AssignReceiptFixture(client, a, true);
        var accepted = await AcceptAsync(client, initial.TaskId, new { note = "Ready" });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var started = await StartAsync(client, initial.TaskId);
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        // 1. Submit initial result (revision 1)
        var submitKey = Guid.NewGuid().ToString();
        var submitResponse = await SubmitResultAsync(client, initial.TaskId, submitKey, new { content = "Draft deliverable v1" });
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        var submittedView = (await submitResponse.Content.ReadFromJsonAsync<TaskSubmittedView>())!;
        Assert.Equal(initial.TaskId, submittedView.TaskId);
        Assert.Equal(1, submittedView.RevisionNo);
        Assert.Equal("SUBMITTED", submittedView.Status);

        // Replay submission returns identical result
        var replaySubmit = await SubmitResultAsync(client, initial.TaskId, submitKey, new { content = "Draft deliverable v1" });
        Assert.Equal(HttpStatusCode.OK, replaySubmit.StatusCode);
        var replayedView = await replaySubmit.Content.ReadFromJsonAsync<TaskSubmittedView>();
        Assert.Equal(submittedView.TaskResultId, replayedView!.TaskResultId);

        // 2. Reviewer requests rework
        var reworkKey = Guid.NewGuid().ToString();
        var reworkResponse = await ConfirmResultAsync(client, initial.TaskId, reworkKey, new { decision = "REWORK", note = "Please refine section 2." });
        Assert.Equal(HttpStatusCode.OK, reworkResponse.StatusCode);
        var reworkView = (await reworkResponse.Content.ReadFromJsonAsync<TaskConfirmedView>())!;
        Assert.Equal("REWORK", reworkView.Decision);
        Assert.Equal("IN_PROGRESS", reworkView.Status);

        // 3. Performer submits revision 2
        var submit2Key = Guid.NewGuid().ToString();
        var submit2Response = await SubmitResultAsync(client, initial.TaskId, submit2Key, new { content = "Refined deliverable v2" });
        Assert.Equal(HttpStatusCode.OK, submit2Response.StatusCode);
        var submittedView2 = (await submit2Response.Content.ReadFromJsonAsync<TaskSubmittedView>())!;
        Assert.Equal(2, submittedView2.RevisionNo);
        Assert.Equal("SUBMITTED", submittedView2.Status);

        // 4. Reviewer approves & confirms completion
        var confirmKey = Guid.NewGuid().ToString();
        var confirmResponse = await ConfirmResultAsync(client, initial.TaskId, confirmKey, new { decision = "CONFIRMED", note = "Deliverables verified." });
        var confirmErrorBody = await confirmResponse.Content.ReadAsStringAsync();
        Assert.True(confirmResponse.StatusCode == HttpStatusCode.OK, $"Confirm failed with: {confirmResponse.StatusCode} - {confirmErrorBody}");
        var confirmedView = (await confirmResponse.Content.ReadFromJsonAsync<TaskConfirmedView>())!;
        Assert.Equal("CONFIRMED", confirmedView.Decision);
        Assert.Equal("COMPLETED", confirmedView.Status);

        // Verify database state
        await using var db = database.Create(a.User, a.Tenant);
        var completedTask = await db.WorkTasks.AsNoTracking().SingleAsync(t => t.Id == initial.TaskId);
        Assert.Equal(TaskState.Completed, completedTask.Status);
        Assert.NotNull(completedTask.CompletedAt);

        var results = await db.TaskResults.Where(r => r.TaskId == initial.TaskId).OrderBy(r => r.RevisionNo).ToArrayAsync();
        Assert.Equal(2, results.Length);
        Assert.Equal("Draft deliverable v1", results[0].Content);
        Assert.Equal("Refined deliverable v2", results[1].Content);

        var confirmations = await db.Confirmations.Where(c => c.ObjectId == initial.TaskId && c.MilestoneType == "RESULT").OrderBy(c => c.ConfirmedAt).ToArrayAsync();
        Assert.Equal(2, confirmations.Length);
        Assert.Equal("REJECTED", confirmations[0].Decision);
        Assert.Equal("Please refine section 2.", confirmations[0].Note);
        Assert.Equal("CONFIRMED", confirmations[1].Decision);
        Assert.Equal("Deliverables verified.", confirmations[1].Note);

        var audits = await db.AuditLogs.Where(a => a.ObjectId == initial.TaskId).Select(a => a.Action).ToArrayAsync();
        Assert.Contains("TASK.RESULT_SUBMITTED", audits);
        Assert.Contains("TASK.RESULT_REWORK", audits);
        Assert.Contains("TASK.RESULT_CONFIRMED", audits);

        // Detail endpoint verification
        var detail = (await client.GetFromJsonAsync<TaskDetailView>($"{Endpoint}/{initial.TaskId}"))!;
        Assert.Equal("COMPLETED", detail.Task.Status);
        Assert.Equal(2, detail.Results.Total);
    }
}
