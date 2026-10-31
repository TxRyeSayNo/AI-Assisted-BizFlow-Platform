using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Security;
using Xunit;

namespace BizFlow.UnitTests.AI;

public sealed class AiTaskDecompositionServiceTests
{
    private sealed class FakeTenantContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? UserId => userId;
        public Guid? TenantId => tenantId;
    }

    private sealed class FakeResourceAuthorizer(HashSet<string>? allowedPermissions = null) : IResourceAuthorizer
    {
        public List<string> CheckedPermissions { get; } = [];

        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
        {
            CheckedPermissions.Add(permission);
            if (allowedPermissions != null && !allowedPermissions.Contains(permission))
            {
                throw new ApplicationFault(FaultKind.Forbidden, "SECURITY.PERMISSION_DENIED", $"Missing permission {permission}.");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAiModelClient(bool isConfigured, string? responseContent = null) : IAiModelClient
    {
        public bool IsConfigured => isConfigured;
        public int CallCount { get; private set; }

        public Task<AiModelResponse> CompleteAsync(string prompt, string systemInstructions, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new AiModelResponse(
                responseContent ?? "{}",
                "gpt-4o-mini",
                120,
                50,
                80));
        }
    }

    private sealed class FakeAiContextReader : IAiContextReader
    {
        public Task<AiTenantContextData> GetTenantContextAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var dept = new AiDepartmentInfo(Guid.NewGuid(), "IT", "Phòng Công nghệ Thông tin");
            var service = new AiServiceInfo(Guid.NewGuid(), "SRV_IT", "Dịch vụ IT", "Hỗ trợ kỹ thuật", [
                new AiCategoryInfo(Guid.NewGuid(), "CAT_HW", "Phần cứng")
            ]);

            return Task.FromResult(new AiTenantContextData(
                tenantId,
                [service],
                [dept],
                [],
                [],
                []));
        }

        public Task<AiTaskContextData?> GetTaskContextAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AiTaskContextData?>(null);

        public Task<AiRequestContextData?> GetRequestContextAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AiRequestContextData?>(null);
    }

    private sealed class FakeTaskDecompositionStore : ITaskDecompositionStore
    {
        public int PersistCallCount { get; private set; }
        public IReadOnlyList<ConfirmTaskDecompositionItem>? LastPersistedItems { get; private set; }

        public Task<IReadOnlyList<CreatedDecompositionItemDto>> PersistDecompositionBatchAsync(
            Guid tenantId,
            Guid actorId,
            IReadOnlyList<ConfirmTaskDecompositionItem> items,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            PersistCallCount++;
            LastPersistedItems = items;

            var result = items.Select((it, idx) => new CreatedDecompositionItemDto(
                Guid.NewGuid(),
                it.Type,
                it.Title,
                "Draft",
                it.DepartmentId)).ToList();

            return Task.FromResult<IReadOnlyList<CreatedDecompositionItemDto>>(result);
        }
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task PreviewAsync_WithoutOpenAi_ProducesDeterministicProposals_AndNeverWritesToDatabase()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var store = new FakeTaskDecompositionStore();
        var modelClient = new FakeAiModelClient(isConfigured: false);

        var service = new AiTaskDecompositionService(
            new FakeTenantContext(tenantId, userId),
            new FakeResourceAuthorizer(),
            new FakeAiContextReader(),
            store,
            new FakeClock(DateTimeOffset.UtcNow),
            modelClient);

        var command = new TaskDecompositionPreviewCommand(
            "1. Cài đặt hệ điều hành cho máy tính mới\n2. Cấu hình mạng VPN công ty\n3. Bàn giao tài khoản email");

        var result = await service.PreviewAsync(command);

        Assert.Equal(3, result.Proposals.Count);
        Assert.Contains(result.Proposals, p => p.Title.Contains("Cài đặt hệ điều hành"));
        Assert.Contains(result.Proposals, p => p.Title.Contains("Cấu hình mạng VPN"));
        Assert.Contains(result.Proposals, p => p.Title.Contains("Bàn giao tài khoản email"));

        // BR-028 Invariant: Zero DB writes during preview
        Assert.Equal(0, store.PersistCallCount);
    }

    [Fact]
    public async Task PreviewAsync_WithOpenAiJsonResponse_ParsesProposalsCorrectly_AndNeverWritesToDatabase()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var store = new FakeTaskDecompositionStore();

        var aiJson = """
        {
            "proposals": [
                {
                    "title": "Kiểm tra hệ thống máy chủ",
                    "description": "Thực hiện rà soát logs và tài nguyên RAM/CPU",
                    "type": "Assignment",
                    "priority": "HIGH",
                    "checklistItems": ["Kiểm tra CPU", "Kiểm tra dung lượng đĩa"],
                    "reasoning": "Cần kiểm tra định kỳ để đảm bảo uptime"
                },
                {
                    "title": "Cấp quyền truy cập VPN",
                    "description": "Yêu cầu cấp VPN cho nhân sự mới",
                    "type": "SupportRequest",
                    "priority": "MEDIUM",
                    "checklistItems": [],
                    "reasoning": "Thuộc quy trình onboarding"
                }
            ]
        }
        """;

        var modelClient = new FakeAiModelClient(isConfigured: true, responseContent: aiJson);

        var service = new AiTaskDecompositionService(
            new FakeTenantContext(tenantId, userId),
            new FakeResourceAuthorizer(),
            new FakeAiContextReader(),
            store,
            new FakeClock(DateTimeOffset.UtcNow),
            modelClient);

        var command = new TaskDecompositionPreviewCommand("Kiểm tra máy chủ và cấp quyền VPN");

        var result = await service.PreviewAsync(command);

        Assert.Equal(2, result.Proposals.Count);
        Assert.Equal("Kiểm tra hệ thống máy chủ", result.Proposals[0].Title);
        Assert.Equal("Assignment", result.Proposals[0].Type);
        Assert.Equal("HIGH", result.Proposals[0].Priority);
        Assert.Equal(2, result.Proposals[0].ChecklistItems.Count);

        Assert.Equal("Cấp quyền truy cập VPN", result.Proposals[1].Title);
        Assert.Equal("SupportRequest", result.Proposals[1].Type);

        // BR-028 Invariant: Zero DB writes during preview
        Assert.Equal(0, store.PersistCallCount);
    }

    [Fact]
    public async Task ConfirmAsync_WhenAuthorized_PersistsTasksAndReturnsResponse()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var store = new FakeTaskDecompositionStore();
        var authorizer = new FakeResourceAuthorizer(["tasks.create", "requests.create"]);

        var service = new AiTaskDecompositionService(
            new FakeTenantContext(tenantId, userId),
            authorizer,
            new FakeAiContextReader(),
            store,
            new FakeClock(DateTimeOffset.UtcNow),
            new FakeAiModelClient(false));

        var command = new ConfirmTaskDecompositionCommand([
            new ConfirmTaskDecompositionItem("Nhiệm vụ 1", "Mô tả 1", "Assignment", null, "MEDIUM", ["Bước 1", "Bước 2"]),
            new ConfirmTaskDecompositionItem("Yêu cầu 1", "Mô tả 2", "SupportRequest", null, "HIGH", null)
        ]);

        var response = await service.ConfirmAsync(command);

        Assert.Equal(2, response.TotalCreated);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(1, store.PersistCallCount);
        Assert.Contains("tasks.create", authorizer.CheckedPermissions);
        Assert.Contains("requests.create", authorizer.CheckedPermissions);
    }

    [Fact]
    public async Task ConfirmAsync_WhenMissingTasksCreatePermission_ThrowsForbiddenAndDoesNotPersist()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var store = new FakeTaskDecompositionStore();
        // Only requests.create is allowed, tasks.create is missing
        var authorizer = new FakeResourceAuthorizer(["requests.create"]);

        var service = new AiTaskDecompositionService(
            new FakeTenantContext(tenantId, userId),
            authorizer,
            new FakeAiContextReader(),
            store,
            new FakeClock(DateTimeOffset.UtcNow),
            new FakeAiModelClient(false));

        var command = new ConfirmTaskDecompositionCommand([
            new ConfirmTaskDecompositionItem("Nhiệm vụ phân công", "Mô tả", "Assignment", null, "MEDIUM", null)
        ]);

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.ConfirmAsync(command));
        Assert.Equal(FaultKind.Forbidden, ex.Kind);
        Assert.Equal(0, store.PersistCallCount);
    }

    [Fact]
    public async Task ConfirmAsync_WhenEmptyItems_ThrowsValidationFault()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var store = new FakeTaskDecompositionStore();

        var service = new AiTaskDecompositionService(
            new FakeTenantContext(tenantId, userId),
            new FakeResourceAuthorizer(),
            new FakeAiContextReader(),
            store,
            new FakeClock(DateTimeOffset.UtcNow),
            new FakeAiModelClient(false));

        var command = new ConfirmTaskDecompositionCommand([]);

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.ConfirmAsync(command));
        Assert.Equal(FaultKind.Validation, ex.Kind);
        Assert.Equal(0, store.PersistCallCount);
    }
}
