using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Sla;
using BizFlow.Domain.Services;
using BizFlow.Domain.Tenancy;
using BizFlow.Domain.Workflows;
using BizFlow.Domain.Tasks;
using BizFlow.IntegrationTests.Authentication;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using BizFlow.Application.Authentication;

namespace BizFlow.BrowserHarness;

internal static class BrowserRunner
{
    public static async Task<int> Main(string[] testArguments)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "BizFlow.slnx"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("Run the browser harness from the BizFlow repository.");
        var database = new PostgresFixture();
        try
        {
            await database.InitializeAsync();
            var seed = await database.SeedTenantAsync();
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)) + "!aA1";
            string tenantKey;
            await using (var db = database.Create(seed.UserId, seed.TenantId))
            {
                var user = await db.Users.SingleAsync();
                var tenant = await db.Tenants.SingleAsync();
                tenantKey = tenant.TenantKey;
                var hash = new PasswordHasher<UserAccount>().HashPassword(user, password);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "User" SET "PasswordHash" = {hash} WHERE "UserId" = {seed.UserId};
                    UPDATE "Company" SET "Status" = 'ACTIVE' WHERE "CompanyId" = {tenant.CompanyId};
                    """);
                var created = DateTimeOffset.UtcNow;
                var departments = Enumerable.Range(1, 26).Select(number => Department.Create(seed.TenantId,
                    $"OPS-{number:00}", $"Operations {number:00}", null, created.AddSeconds(number))).ToArray();
                db.Departments.AddRange(departments);
                await db.SaveChangesAsync();
                // Test-only fixture for the canonical INACTIVE filter; no production mutation bypass.
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "Department" SET "Status" = 'INACTIVE' WHERE "DepartmentId" = {departments[^1].Id};
                    """);
                var employeeRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "EMPLOYEE");
                db.UserRoles.Add(new(user.Id, employeeRole.Id));
                var readerWorkflow = WorkflowDefinition.CreateDraft(tenant.Id, "Reader workflow", WorkflowBusinessType.Request, created);
                db.SlaProfiles.Add(SlaProfile.CreateDraft(tenant.Id, "Other tenant SLA sentinel"));
                var readerService = InternalService.Create(tenant.Id, "READER", "Reader service", "Employee-visible service", true, created);
                db.AddRange(readerService, ServiceCategory.Create(readerService.Id, "GENERAL", "Reader category"));
                db.Workflows.Add(readerWorkflow); db.WorkflowVersions.Add(WorkflowVersion.CreateDraft(readerWorkflow.Id, 1));
                await db.SaveChangesAsync();
                // Disposable inbox fixtures; no production event producer or sent-email claim.
                db.Notifications.AddRange(Enumerable.Range(1, 26).Select(number => Notification.Create(tenant.Id, user.Id,
                    NotificationEvent.ApprovalRequired, $"Inbox event {number:00}", "Review the assigned work when its workflow is available. This message belongs only to the signed-in recipient.", $"fixture/{number}/{user.Id}")));
                var otherRecipient = UserAccount.CreateTenantUser(tenant.Id, "INBOX-OTHER", "inbox-other@example.test", "Other inbox recipient", "fixture-only", created);
                db.Users.Add(otherRecipient); await db.SaveChangesAsync();
                // Disposable Task-read fixtures; this does not simulate production creation/assignment commands.
                foreach (var number in Enumerable.Range(1, 26))
                {
                    var task = WorkTask.CreateDraft(tenant.Id, number == 26 ? otherRecipient.Id : user.Id,
                        $"Review operational work {number:00}", created.AddSeconds(number),
                        priority: number == 26 ? TaskPriority.Critical : TaskPriority.Medium,
                        deadline: number == 26 ? created.AddHours(2) : null);
                    db.WorkTasks.Add(task); await db.SaveChangesAsync();
                    if (number == 26)
                    {
                        db.TaskAssignments.Add(TaskAssignment.Create(task.Id, otherRecipient.Id, null, user.Id, created));
                        await db.SaveChangesAsync();
                        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Task\" SET \"Status\"='ASSIGNED' WHERE \"TaskId\"={task.Id}");
                    }
                }
                db.WorkTasks.Add(WorkTask.CreateDraft(tenant.Id, otherRecipient.Id, "Private unrelated task sentinel", created));
                await db.SaveChangesAsync();
                db.Notifications.Add(Notification.Create(tenant.Id, otherRecipient.Id, NotificationEvent.TaskAssigned, "Other user's private event", "Not visible to the reader", "private/recipient"));
                await db.SaveChangesAsync();
            }
            var resetAccounts = new Dictionary<string, (Guid UserId, string Key)>();
            var slaEditKeys = new Dictionary<string, string>();
            var taskCreateKeys = new Dictionary<string, string>();
            var adminSeed = await database.SeedTenantAsync();
            string adminKey;
            await using (var db = database.Create(adminSeed.UserId, adminSeed.TenantId))
            {
                var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
                adminKey = tenant.TenantKey;
                var hash = new PasswordHasher<UserAccount>().HashPassword(user, password);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "User" SET "PasswordHash" = {hash} WHERE "UserId" = {user.Id};
                    UPDATE "Company" SET "Status" = 'ACTIVE' WHERE "CompanyId" = {tenant.CompanyId};
                    """);
                var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "COMPANY_ADMIN");
                db.UserRoles.Add(new(user.Id, role.Id));
                await db.SaveChangesAsync();
                db.WorkTasks.Add(WorkTask.CreateDraft(tenant.Id, user.Id, "Foreign task sentinel", DateTimeOffset.UtcNow));
                await db.SaveChangesAsync();
                var peopleDepartment = Department.Create(tenant.Id, "PEOPLE-OPS", "People operations", null, DateTimeOffset.UtcNow);
                db.Departments.Add(peopleDepartment); await db.SaveChangesAsync();
                var created = DateTimeOffset.UtcNow;
                var colleagues = Enumerable.Range(1, 26).Select(number => UserAccount.CreateTenantUser(tenant.Id,
                    $"PERSON-{number:00}", $"person-{number:00}@example.test", $"Workspace colleague {number:00}",
                    "not-an-authenticatable-hash", created.AddSeconds(number), peopleDepartment.Id)).ToArray();
                db.Users.AddRange(colleagues); await db.SaveChangesAsync();
                db.Notifications.Add(Notification.Create(tenant.Id, user.Id, NotificationEvent.TaskAssigned, "Other tenant event", "Not visible to the reader", "private/tenant"));
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\" = 'INACTIVE' WHERE \"UserId\" = {colleagues[^1].Id}");
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"User\" SET \"Status\" = 'LOCKED' WHERE \"UserId\" = {colleagues[^2].Id}");
                // Disposable configuration fixtures only; production graph authoring is not implemented here.
                foreach (var number in Enumerable.Range(1, 26))
                {
                    var service = InternalService.Create(tenant.Id, $"SVC-{number:00}", $"Template service {number:00}", "Service fixture", number != 26, created);
                    db.AddRange(service, ServiceCategory.Create(service.Id, "GENERAL", "General support"));
                    var slaProfile = SlaProfile.CreateDraft(tenant.Id, $"Template SLA {number:00}");
                    db.SlaProfiles.Add(slaProfile);
                    if (number == 26)
                    {
                        var calendar = BusinessCalendar.Create(tenant.Id, "UTC", """{"monday":[{"start":"08:00","end":"17:30"}]}""", """["2026-10-03"]""");
                        db.BusinessCalendars.Add(calendar);
                        foreach (var versionNo in Enumerable.Range(1, 26))
                            db.SlaVersions.Add(SlaVersion.CreateSnapshot(slaProfile.Id, versionNo, 60 + versionNo, 45, calendar.Id));
                    }
                    var type = number == 26 ? WorkflowBusinessType.Request : WorkflowBusinessType.Task;
                    var workflow = WorkflowDefinition.CreateDraft(tenant.Id, $"Template workflow {number:00}", type, created);
                    var version = WorkflowVersion.CreateDraft(workflow.Id, 1);
                    db.AddRange(workflow, version, WorkflowStep.CreateDraft(version.Id, "WORK", "Fixture work step", WorkflowStepType.Action, 1),
                        WorkflowTransition.CreateDraft(version.Id, "DRAFT", type == WorkflowBusinessType.Task ? "ASSIGNED" : "SUBMITTED"));
                }
                await db.SaveChangesAsync();
            }
            await using (var db = database.Create(database.PlatformUserId, null))
            {
                var platform = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == database.PlatformUserId);
                var hash = new PasswordHasher<UserAccount>().HashPassword(platform, password);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "User" SET "PasswordHash" = {hash} WHERE "UserId" = {platform.Id};
                    INSERT INTO "UserRole" ("UserId", "RoleId") SELECT {platform.Id}, "RoleId" FROM "Role"
                    WHERE "Name" = 'PLATFORM_ADMIN' AND "IsSystem";
                    """);
                db.Companies.AddRange(
                    Company.Register("BROWSER-ONBOARDING-A", "Browser onboarding A", "onboarding-a@example.test", DateTimeOffset.UtcNow),
                    Company.Register("BROWSER-ONBOARDING-B", "Browser onboarding B", "onboarding-b@example.test", DateTimeOffset.UtcNow));
                await db.SaveChangesAsync();
            }
            foreach (var project in new[] { "desktop", "mobile" })
            {
                var resetSeed = await database.SeedTenantAsync();
                await using var db = database.Create(resetSeed.UserId, resetSeed.TenantId);
                var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
                var hash = new PasswordHasher<UserAccount>().HashPassword(user, password);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "User" SET "PasswordHash" = {hash} WHERE "UserId" = {user.Id};
                    UPDATE "Company" SET "Status" = 'ACTIVE' WHERE "CompanyId" = {tenant.CompanyId};
                    """);
                resetAccounts[project] = (user.Id, tenant.TenantKey);
                db.Departments.Add(Department.Create(tenant.Id, "OTHER-TENANT", "Other tenant confidential department", null, DateTimeOffset.UtcNow));
                await db.SaveChangesAsync();
            }
            // Separate configuration tenants avoid coupling mutable browser tests or pagination counts.
            foreach (var project in new[] { "desktop", "mobile" })
            {
                var editorSeed = await database.SeedTenantAsync();
                await using var db = database.Create(editorSeed.UserId, editorSeed.TenantId);
                var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
                var hash = new PasswordHasher<UserAccount>().HashPassword(user, password);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "User" SET "PasswordHash" = {hash} WHERE "UserId" = {user.Id};
                    UPDATE "Company" SET "Status" = 'ACTIVE' WHERE "CompanyId" = {tenant.CompanyId};
                    """);
                var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "COMPANY_ADMIN");
                db.UserRoles.Add(new(user.Id, role.Id));
                var profile = SlaProfile.CreateDraft(tenant.Id, "Timing configuration");
                var calendar = BusinessCalendar.Create(tenant.Id, "UTC", """{"monday":[{"start":"08:00","end":"17:30"}]}""", """["2026-10-03"]""");
                var escalation = System.Text.Json.JsonSerializer.Serialize(new { levels = new[] { new { level = 1, afterMinutes = 0, recipientUserIds = new[] { user.Id } } } });
                db.AddRange(profile, calendar, SlaVersion.CreateSnapshot(profile.Id, 1, 60, 45, calendar.Id, escalation));
                await db.SaveChangesAsync();
                slaEditKeys[project] = tenant.TenantKey;
            }
            foreach (var project in new[] { "desktop", "mobile", "assignment_desktop", "assignment_mobile" })
            {
                var creatorSeed = await database.SeedTenantAsync();
                await using var db = database.Create(creatorSeed.UserId, creatorSeed.TenantId);
                var user = await db.Users.SingleAsync(); var tenant = await db.Tenants.SingleAsync();
                var hash = new PasswordHasher<UserAccount>().HashPassword(user, password);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "User" SET "PasswordHash"={hash} WHERE "UserId"={user.Id};
                    UPDATE "Company" SET "Status"='ACTIVE' WHERE "CompanyId"={tenant.CompanyId};
                    """);
                var role = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "MANAGER");
                db.UserRoles.Add(new(user.Id, role.Id));
                var department = Department.Create(tenant.Id, "TASK-TEAM", "Assignment team", null, DateTimeOffset.UtcNow);
                db.Departments.Add(department); await db.SaveChangesAsync();
                var recipient = UserAccount.CreateTenantUser(tenant.Id, "ASSIGNEE", "assignee@example.test", "Assignment recipient", hash, DateTimeOffset.UtcNow, department.Id);
                db.Users.Add(recipient); await db.SaveChangesAsync();
                var employeeRole = await db.Roles.SingleAsync(r => r.IsSystem && r.Name == "EMPLOYEE");
                db.UserRoles.Add(new(recipient.Id, employeeRole.Id));
                db.ManagementScopes.Add(ManagementScope.Create(tenant.Id, user.Id, department.Id, false, user.Id, DateTimeOffset.UtcNow));
                await db.SaveChangesAsync();
                taskCreateKeys[project] = tenant.TenantKey;
            }
            var inbox = new ResetInbox();
            await using var factory = new AuthenticationFactory(database, new Dictionary<string, string?>
            {
                ["ResetEmail:Enabled"] = "true", ["ResetEmail:Host"] = "127.0.0.1",
                ["ResetEmail:FromAddress"] = "no-reply@example.test", ["ResetEmail:FrontendOrigin"] = "http://127.0.0.1:4281"
            }, contentRoot: Path.Combine(root.FullName, "backend", "BizFlow.Api"), configureServices: services =>
            {
                services.RemoveAll<IPasswordResetEmailSender>();
                services.AddSingleton<IPasswordResetEmailSender>(inbox);
            });
            factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));
            using var client = factory.CreateClient();
            var apiUrl = factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            var start = new ProcessStartInfo("node")
            {
                WorkingDirectory = Path.Combine(root.FullName, "tests", "BizFlow.E2ETests"),
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            start.ArgumentList.Add("node_modules/@playwright/test/cli.js");
            start.ArgumentList.Add("test"); start.ArgumentList.Add("--config=playwright.real.config.ts");
            // Optional Playwright selectors allow focused verification after a full regression run.
            // ArgumentList forwards values without constructing shell commands; no change to default coverage.
            foreach (var argument in testArguments) start.ArgumentList.Add(argument);
            start.Environment["BIZFLOW_E2E_API_URL"] = apiUrl;
            start.Environment["BIZFLOW_E2E_TENANT_KEY"] = tenantKey;
            start.Environment["BIZFLOW_E2E_TENANT_ID"] = seed.TenantId.ToString();
            start.Environment["BIZFLOW_E2E_PASSWORD"] = password;
            start.Environment["BIZFLOW_E2E_ADMIN_KEY"] = adminKey;
            foreach (var (project, key) in taskCreateKeys)
                start.Environment["BIZFLOW_E2E_TASK_CREATE_KEY_" + project.ToUpperInvariant()] = key;
            foreach (var (project, key) in slaEditKeys)
                start.Environment["BIZFLOW_E2E_SLA_EDIT_KEY_" + project.ToUpperInvariant()] = key;
            foreach (var (project, account) in resetAccounts)
            {
                using var request = await client.PostAsJsonAsync("/api/v1/auth/forgot-password", new { identifier = "EMP001", tenantKey = account.Key });
                request.EnsureSuccessStatusCode();
                var message = await inbox.NextAsync();
                var ticket = await factory.Services.GetRequiredService<IPasswordResetTokenProvider>().ValidateAsync(message.Token, default);
                if (ticket?.UserId != account.UserId) throw new InvalidOperationException("The reset email targeted the wrong test identity.");
                start.Environment["BIZFLOW_E2E_RESET_TOKEN_" + project.ToUpperInvariant()] = message.Token;
                start.Environment["BIZFLOW_E2E_RESET_KEY_" + project.ToUpperInvariant()] = account.Key;
            }
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start Playwright.");
            var output = RelayAsync(process.StandardOutput, Console.Out);
            var errors = RelayAsync(process.StandardError, Console.Error);
            using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(6));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                throw new TimeoutException("Browser tests exceeded their time budget.");
            }
            await Task.WhenAll(output, errors);
            return process.ExitCode;
        }
        finally { await database.DisposeAsync(); }
    }

    private static async Task RelayAsync(StreamReader source, TextWriter destination)
    {
        while (await source.ReadLineAsync() is { } line) await destination.WriteLineAsync(line);
    }
}
