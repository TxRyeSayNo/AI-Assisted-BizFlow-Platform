using System.Net;
using System.Net.Http.Json;
using BizFlow.Application.Authentication;
using BizFlow.Application.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Notifications;
using BizFlow.IntegrationTests.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BizFlow.IntegrationTests.Persistence;

/// <summary>
/// End-to-end checks for the deadline reminder module (F4, closing GAP-01): the configuration
/// endpoints that had no controller, and the background sweep that provisions and dispatches
/// reminders idempotently while writing a NotificationLog row per channel.
/// </summary>
[Collection("Postgres")]
public sealed class DeadlineReminderApiTests(PostgresFixture database)
{
    private const string Password = "Deadline-reminder-test!2048";
    private const string Config = "/api/v1/notifications/reminders/config";

    [Fact]
    public async Task The_sweep_provisions_the_documented_marks_only_for_dated_open_assigned_work()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDeadlineReminderDispatcher>();

        var result = await dispatcher.SweepTenantAsync(a.Tenant, default);

        await using var db = database.Create(a.User, a.Tenant);
        var schedules = await db.DeadlineReminderSchedules.AsNoTracking().ToListAsync();

        // Only the far-future task gets all three warnings; every dated open task gets an overdue mark.
        Assert.Equal(schedules.Count, result.SchedulesCreated);
        Assert.All(schedules, s => Assert.Equal(a.User, s.UserId));
        Assert.DoesNotContain(schedules, s => s.TaskId == a.UndatedTaskId);
        Assert.DoesNotContain(schedules, s => s.TaskId == a.CompletedTaskId);
        Assert.DoesNotContain(schedules, s => s.TaskId == a.UnassignedTaskId);

        var future = schedules.Where(s => s.TaskId == a.FutureTaskId).ToList();
        Assert.Equal(["1h", "24h", "4h", "Overdue"], future.Select(s => s.TriggerType).Order().ToArray());
        Assert.Equal(
            DeadlineReminderDispatcher.LeadTimesMinutes.Order().ToArray(),
            future.Where(s => s.TriggerType != "Overdue").Select(s => s.LeadTimeMinutes).Order().ToArray());

        // A deadline two hours out can no longer carry the 24h or 4h warning.
        var soon = schedules.Where(s => s.TaskId == a.SoonTaskId).ToList();
        Assert.Equal(["1h", "Overdue"], soon.Select(s => s.TriggerType).Order().ToArray());

        // Running it again provisions nothing new.
        Assert.Equal(0, (await dispatcher.SweepTenantAsync(a.Tenant, default)).SchedulesCreated);
    }

    [Fact]
    public async Task A_due_reminder_is_sent_once_and_leaves_an_inbox_row_plus_a_channel_log()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDeadlineReminderDispatcher>();

        // The overdue task's deadline has already passed, so its overdue mark is due at once.
        var first = await dispatcher.SweepTenantAsync(a.Tenant, default);
        Assert.True(first.RemindersSent >= 1);

        await using var db = database.Create(a.User, a.Tenant);
        var overdueSchedules = await db.DeadlineReminderSchedules.AsNoTracking()
            .Where(s => s.TaskId == a.OverdueTaskId).ToListAsync();
        Assert.All(overdueSchedules, s => Assert.Equal("Sent", s.Status));

        var inbox = await db.Notifications.AsNoTracking()
            .Where(n => n.ObjectId == a.OverdueTaskId).ToListAsync();
        var overdueNotification = Assert.Single(inbox, n => n.Type == "Overdue");
        Assert.Equal(a.User, overdueNotification.RecipientId);
        Assert.Equal("TASK", overdueNotification.ObjectType);
        Assert.Contains("quá hạn", overdueNotification.Content);

        // Exactly one log row per reminder, as the table's unique index requires, naming the
        // channel the escalation ended on. Nobody is connected over SignalR in this host, so the
        // sweep went past the realtime channel.
        var log = Assert.Single(await db.NotificationLogs.AsNoTracking()
            .Where(l => l.TaskId == a.OverdueTaskId && l.TriggerType == "Overdue").ToListAsync());
        Assert.Contains(log.Channel, new[] { "WebPush", "Email" });
        Assert.Equal(a.User, log.UserId);

        // A second sweep must not resend anything that was already delivered.
        var second = await dispatcher.SweepTenantAsync(a.Tenant, default);
        Assert.Equal(0, second.RemindersSent);
        Assert.Equal(inbox.Count, await db.Notifications.AsNoTracking()
            .Where(n => n.ObjectId == a.OverdueTaskId).CountAsync());
    }

    [Fact]
    public async Task A_reminder_for_work_that_already_closed_is_retired_rather_than_sent()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDeadlineReminderDispatcher>();

        await dispatcher.SweepTenantAsync(a.Tenant, default);

        // The task is closed after its reminders were scheduled but before they came due.
        await using var db = database.Create(a.User, a.Tenant);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Task" SET "Status"='COMPLETED' WHERE "TaskId"={a.SoonTaskId};
            UPDATE "DeadlineReminderSchedule" SET "RemindAt"={DateTimeOffset.UtcNow.AddMinutes(-1)}
                WHERE "TaskId"={a.SoonTaskId} AND "Status"='Scheduled';
            """);

        await dispatcher.SweepTenantAsync(a.Tenant, default);

        var schedules = await db.DeadlineReminderSchedules.AsNoTracking()
            .Where(s => s.TaskId == a.SoonTaskId).ToListAsync();
        Assert.All(schedules, s => Assert.Equal("Cancelled", s.Status));
        Assert.Empty(await db.Notifications.AsNoTracking().Where(n => n.ObjectId == a.SoonTaskId).ToListAsync());
        Assert.All(await db.NotificationLogs.AsNoTracking().Where(l => l.TaskId == a.SoonTaskId).ToListAsync(),
            l => Assert.Equal("Skipped", l.Status));
    }

    [Fact]
    public async Task One_tenants_sweep_never_touches_another_tenants_work()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        var foreign = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDeadlineReminderDispatcher>();

        await dispatcher.SweepTenantAsync(a.Tenant, default);

        await using var db = database.Create(foreign.User, foreign.Tenant);
        Assert.Empty(await db.DeadlineReminderSchedules.AsNoTracking().ToListAsync());
        Assert.Empty(await db.NotificationLogs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task The_configuration_endpoints_replace_read_and_cancel_the_callers_own_marks()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        using var created = await client.PostAsJsonAsync(Config,
            new { taskId = a.FutureTaskId, leadTimeMinutes = new[] { 120, 30 } });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var marks = (await created.Content.ReadFromJsonAsync<DeadlineReminderDto[]>())!;
        Assert.Equal([30, 120], marks.Select(m => m.LeadTimeMinutes).Order().ToArray());
        Assert.All(marks, m => Assert.Equal(a.User, m.UserId));

        var read = (await client.GetFromJsonAsync<DeadlineReminderDto[]>($"{Config}?taskId={a.FutureTaskId}"))!;
        Assert.Equal(marks.Length, read.Length);

        // Reconfiguring the same marks must re-arm the existing rows rather than fail: the table
        // allows one row per (tenant, task, user, trigger) and the platform forbids hard deletes.
        using var again = await client.PostAsJsonAsync(Config,
            new { taskId = a.FutureTaskId, leadTimeMinutes = new[] { 120, 30 } });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var reconfigured = (await again.Content.ReadFromJsonAsync<DeadlineReminderDto[]>())!;
        // The same rows came back, identified the same way, so the response describes real rows.
        Assert.Equal(marks.Select(m => m.Id).Order(), reconfigured.Select(m => m.Id).Order());
        var stored = (await client.GetFromJsonAsync<DeadlineReminderDto[]>($"{Config}?taskId={a.FutureTaskId}"))!;
        Assert.Equal(reconfigured.Length, stored.Length);
        Assert.All(stored, m => Assert.Equal("Scheduled", m.Status));

        using var cancelled = await client.DeleteAsync($"/api/v1/notifications/reminders/{marks[0].Id}");
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);

        var key = await client.GetFromJsonAsync<DeadlineRemindersControllerProbe>(
            "/api/v1/notifications/reminders/vapid-public-key");
        Assert.NotNull(key);
        Assert.Null(factory.LastServerError);
    }

    [Fact]
    public async Task Configuration_is_validated_authenticated_and_scoped_to_a_visible_task()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database);
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Config}?taskId={a.FutureTaskId}")).StatusCode);

        await LoginAsync(client, a);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.GetAsync($"{Config}?taskId={Guid.Empty}")).StatusCode);
        // A task without a deadline cannot carry reminders.
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await client.PostAsJsonAsync(Config,
            new { taskId = a.UndatedTaskId, leadTimeMinutes = new[] { 60 } })).StatusCode);
        // A task of another tenant does not exist as far as this caller is concerned.
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Config,
            new { taskId = Guid.NewGuid(), leadTimeMinutes = new[] { 60 } })).StatusCode);

        using var response = await client.GetAsync($"{Config}?taskId={a.FutureTaskId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain("PasswordHash", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Turning_the_flag_off_hides_the_reminder_endpoints_without_touching_notifications()
    {
        var a = await SeedAsync("COMPANY_ADMIN");
        await using var factory = new AuthenticationFactory(database,
            new Dictionary<string, string?> { ["Features:DeadlineReminders"] = "false" });
        using var client = factory.CreateClient();
        await LoginAsync(client, a);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Config}?taskId={a.FutureTaskId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(Config,
            new { taskId = a.FutureTaskId, leadTimeMinutes = new[] { 60 } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/v1/notifications/reminders/vapid-public-key")).StatusCode);

        // The notification inbox that existed before keeps answering exactly as it did.
        using var inbox = await client.GetAsync("/api/v1/notifications");
        Assert.Equal(HttpStatusCode.OK, inbox.StatusCode);
    }

    // -------------------------------------------------------------------------------------------
    // Fixture
    // -------------------------------------------------------------------------------------------

    private async Task<Account> SeedAsync(string? roleName)
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var user = await db.Users.SingleAsync();
        var tenant = await db.Tenants.SingleAsync();
        var now = DateTimeOffset.UtcNow;

        var department = Department.Create(tenant.Id, "OURS", "Phòng của chúng tôi", null, now);
        db.Add(department);
        await db.SaveChangesAsync();

        var future = WorkTask.CreateDraft(tenant.Id, user.Id, "Hạn xa", now, deadline: now.AddDays(5));
        var soon = WorkTask.CreateDraft(tenant.Id, user.Id, "Hạn gần", now, deadline: now.AddHours(2));
        var overdue = WorkTask.CreateDraft(tenant.Id, user.Id, "Đã quá hạn", now, deadline: now.AddHours(-3));
        var undated = WorkTask.CreateDraft(tenant.Id, user.Id, "Không hạn", now);
        var completed = WorkTask.CreateDraft(tenant.Id, user.Id, "Đã xong", now, deadline: now.AddDays(2));
        var unassigned = WorkTask.CreateDraft(tenant.Id, user.Id, "Chưa phân công", now, deadline: now.AddDays(3));
        db.AddRange(future, soon, overdue, undated, completed, unassigned);
        await db.SaveChangesAsync();

        db.AddRange(
            TaskAssignment.Create(future.Id, user.Id, null, user.Id, now),
            TaskAssignment.Create(soon.Id, user.Id, null, user.Id, now),
            TaskAssignment.Create(overdue.Id, user.Id, null, user.Id, now),
            TaskAssignment.Create(undated.Id, user.Id, null, user.Id, now),
            TaskAssignment.Create(completed.Id, user.Id, null, user.Id, now),
            // Routed to a department rather than a person: nobody to remind.
            TaskAssignment.Create(unassigned.Id, user.Id, department.Id, null, now));
        await db.SaveChangesAsync();

        var hash = new PasswordHasher<UserAccount>().HashPassword(user, Password);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "User" SET "PasswordHash"={hash}, "DepartmentId"={department.Id} WHERE "UserId"={user.Id};
            UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
            UPDATE "Task" SET "Status"='COMPLETED' WHERE "TaskId"={completed.Id};
            """);

        if (roleName is not null)
        {
            var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == roleName);
            db.UserRoles.Add(new(user.Id, role.Id));
            await db.SaveChangesAsync();
        }

        return new(user.Id, tenant.Id, tenant.TenantKey, future.Id, soon.Id, overdue.Id,
            undated.Id, completed.Id, unassigned.Id);
    }

    private static async Task LoginAsync(HttpClient client, Account a)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { identifier = "EMP001", password = Password, tenantKey = a.Key });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        client.DefaultRequestHeaders.Authorization =
            new("Bearer", (await response.Content.ReadFromJsonAsync<AuthenticationResponse>())!.AccessToken);
    }

    private sealed record DeadlineRemindersControllerProbe(string PublicKey);

    private sealed record Account(Guid User, Guid Tenant, string Key, Guid FutureTaskId, Guid SoonTaskId,
        Guid OverdueTaskId, Guid UndatedTaskId, Guid CompletedTaskId, Guid UnassignedTaskId);
}
