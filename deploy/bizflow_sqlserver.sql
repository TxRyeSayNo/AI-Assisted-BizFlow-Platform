-- =============================================================================
-- BizFlow Platform - Microsoft SQL Server (T-SQL) Schema Script
-- Converted from PostgreSQL 18 pg_dump
-- =============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- =============================================================================
-- 1. SCHEMAS
-- =============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE [name] = N'hangfire')
BEGIN
    EXEC(N'CREATE SCHEMA [hangfire]');
END;
GO

-- =============================================================================
-- 2. HANGFIRE TABLES
-- =============================================================================

CREATE TABLE [hangfire].[schema] (
    [version] INT NOT NULL,
    CONSTRAINT [schema_pkey] PRIMARY KEY CLUSTERED ([version])
);
GO

CREATE TABLE [hangfire].[aggregatedcounter] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [key] NVARCHAR(255) NOT NULL,
    [value] BIGINT NOT NULL,
    [expireat] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [aggregatedcounter_pkey] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [aggregatedcounter_key_key] UNIQUE NONCLUSTERED ([key])
);
GO

CREATE TABLE [hangfire].[counter] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [key] NVARCHAR(255) NOT NULL,
    [value] BIGINT NOT NULL,
    [expireat] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [counter_pkey] PRIMARY KEY CLUSTERED ([id])
);
GO

CREATE TABLE [hangfire].[hash] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [key] NVARCHAR(255) NOT NULL,
    [field] NVARCHAR(255) NOT NULL,
    [value] NVARCHAR(MAX) NULL,
    [expireat] DATETIMEOFFSET(7) NULL,
    [updatecount] INT NOT NULL CONSTRAINT [DF_hangfire_hash_updatecount] DEFAULT (0),
    CONSTRAINT [hash_pkey] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [hash_key_field_key] UNIQUE NONCLUSTERED ([key], [field])
);
GO

CREATE TABLE [hangfire].[job] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [stateid] BIGINT NULL,
    [statename] NVARCHAR(50) NULL,
    [invocationdata] NVARCHAR(MAX) NOT NULL,
    [arguments] NVARCHAR(MAX) NOT NULL,
    [createdat] DATETIMEOFFSET(7) NOT NULL,
    [expireat] DATETIMEOFFSET(7) NULL,
    [updatecount] INT NOT NULL CONSTRAINT [DF_hangfire_job_updatecount] DEFAULT (0),
    CONSTRAINT [job_pkey] PRIMARY KEY CLUSTERED ([id])
);
GO

CREATE TABLE [hangfire].[jobparameter] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [jobid] BIGINT NOT NULL,
    [name] NVARCHAR(100) NOT NULL,
    [value] NVARCHAR(MAX) NULL,
    [updatecount] INT NOT NULL CONSTRAINT [DF_hangfire_jobparameter_updatecount] DEFAULT (0),
    CONSTRAINT [jobparameter_pkey] PRIMARY KEY CLUSTERED ([id])
);
GO

CREATE TABLE [hangfire].[jobqueue] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [jobid] BIGINT NOT NULL,
    [queue] NVARCHAR(100) NOT NULL,
    [fetchedat] DATETIMEOFFSET(7) NULL,
    [updatecount] INT NOT NULL CONSTRAINT [DF_hangfire_jobqueue_updatecount] DEFAULT (0),
    CONSTRAINT [jobqueue_pkey] PRIMARY KEY CLUSTERED ([id])
);
GO

CREATE TABLE [hangfire].[list] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [key] NVARCHAR(255) NOT NULL,
    [value] NVARCHAR(MAX) NULL,
    [expireat] DATETIMEOFFSET(7) NULL,
    [updatecount] INT NOT NULL CONSTRAINT [DF_hangfire_list_updatecount] DEFAULT (0),
    CONSTRAINT [list_pkey] PRIMARY KEY CLUSTERED ([id])
);
GO

CREATE TABLE [hangfire].[lock] (
    [resource] NVARCHAR(255) NOT NULL,
    [updatecount] INT NOT NULL CONSTRAINT [DF_hangfire_lock_updatecount] DEFAULT (0),
    [acquired] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [lock_resource_key] UNIQUE CLUSTERED ([resource])
);
GO

CREATE TABLE [hangfire].[server] (
    [id] NVARCHAR(255) NOT NULL,
    [data] NVARCHAR(MAX) NULL,
    [lastheartbeat] DATETIMEOFFSET(7) NOT NULL,
    [updatecount] INT NOT NULL CONSTRAINT [DF_hangfire_server_updatecount] DEFAULT (0),
    CONSTRAINT [server_pkey] PRIMARY KEY CLUSTERED ([id])
);
GO

CREATE TABLE [hangfire].[set] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [key] NVARCHAR(255) NOT NULL,
    [score] FLOAT NOT NULL,
    [value] NVARCHAR(255) NOT NULL,
    [expireat] DATETIMEOFFSET(7) NULL,
    [updatecount] INT NOT NULL CONSTRAINT [DF_hangfire_set_updatecount] DEFAULT (0),
    CONSTRAINT [set_pkey] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [set_key_value_key] UNIQUE NONCLUSTERED ([key], [value])
);
GO

CREATE TABLE [hangfire].[state] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [jobid] BIGINT NOT NULL,
    [name] NVARCHAR(50) NOT NULL,
    [reason] NVARCHAR(MAX) NULL,
    [createdat] DATETIMEOFFSET(7) NOT NULL,
    [data] NVARCHAR(MAX) NULL,
    [updatecount] INT NOT NULL CONSTRAINT [DF_hangfire_state_updatecount] DEFAULT (0),
    CONSTRAINT [state_pkey] PRIMARY KEY CLUSTERED ([id])
);
GO

-- =============================================================================
-- 3. APPLICATION TABLES ([dbo])
--    Note: PostgreSQL ENUM types are mapped to NVARCHAR(32) with CHECK constraints.
-- =============================================================================

CREATE TABLE [dbo].[__EFMigrationsHistory] (
    [MigrationId] NVARCHAR(150) NOT NULL,
    [ProductVersion] NVARCHAR(32) NOT NULL,
    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED ([MigrationId])
);
GO

CREATE TABLE [dbo].[Company] (
    [CompanyId] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(50) NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [ContactEmail] NVARCHAR(320) NOT NULL,
    [Status] NVARCHAR(32) NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
    [TaxId] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Company_TaxId] DEFAULT (N''),
    [ContactPersonName] NVARCHAR(150) NOT NULL CONSTRAINT [DF_Company_ContactPersonName] DEFAULT (N''),
    [ContactPhone] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Company_ContactPhone] DEFAULT (N''),
    [EvidenceDocumentKey] NVARCHAR(500) NULL,
    [EvidenceDocumentFileName] NVARCHAR(255) NULL,
    CONSTRAINT [PK_Company] PRIMARY KEY CLUSTERED ([CompanyId]),
    CONSTRAINT [CK_Company_Status] CHECK ([Status] IN (N'ACTIVE', N'INACTIVE', N'PENDING', N'SUSPENDED'))
);
GO

CREATE TABLE [dbo].[Permission] (
    [PermissionId] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(120) NOT NULL,
    [Module] NVARCHAR(80) NOT NULL,
    [Action] NVARCHAR(80) NOT NULL,
    [ScopeType] NVARCHAR(32) NOT NULL,
    CONSTRAINT [PK_Permission] PRIMARY KEY CLUSTERED ([PermissionId]),
    CONSTRAINT [CK_Permission_ScopeType] CHECK ([ScopeType] IN (N'ASSIGNED', N'DEPARTMENT', N'PLATFORM', N'SELF', N'TENANT'))
);
GO

CREATE TABLE [dbo].[Tenant] (
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [CompanyId] UNIQUEIDENTIFIER NOT NULL,
    [TenantKey] NVARCHAR(80) NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [Status] NVARCHAR(32) NOT NULL,
    [TimeZone] NVARCHAR(64) NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_Tenant] PRIMARY KEY CLUSTERED ([TenantId]),
    CONSTRAINT [CK_Tenant_Status] CHECK ([Status] IN (N'ACTIVE', N'INACTIVE', N'PROVISIONING', N'SUSPENDED'))
);
GO

CREATE TABLE [dbo].[AuditLog] (
    [AuditLogId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NULL,
    [ActorType] NVARCHAR(32) NOT NULL,
    [ActorId] UNIQUEIDENTIFIER NULL,
    [Action] NVARCHAR(100) NOT NULL,
    [ObjectType] NVARCHAR(60) NULL,
    [ObjectId] UNIQUEIDENTIFIER NULL,
    [BeforeJson] NVARCHAR(MAX) NULL,
    [AfterJson] NVARCHAR(MAX) NULL,
    [MetadataJson] NVARCHAR(MAX) NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_AuditLog] PRIMARY KEY CLUSTERED ([AuditLogId]),
    CONSTRAINT [CK_AuditLog_ActorType] CHECK ([ActorType] IN (N'AI_AGENT', N'SYSTEM', N'USER')),
    CONSTRAINT [CK_AuditLog_BeforeJson] CHECK ([BeforeJson] IS NULL OR ISJSON([BeforeJson]) = 1),
    CONSTRAINT [CK_AuditLog_AfterJson] CHECK ([AfterJson] IS NULL OR ISJSON([AfterJson]) = 1),
    CONSTRAINT [CK_AuditLog_MetadataJson] CHECK ([MetadataJson] IS NULL OR ISJSON([MetadataJson]) = 1)
);
GO

CREATE TABLE [dbo].[Department] (
    [DepartmentId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(50) NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [ParentDepartmentId] UNIQUEIDENTIFIER NULL,
    [Status] NVARCHAR(32) NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_Department] PRIMARY KEY CLUSTERED ([DepartmentId]),
    CONSTRAINT [AK_Department_TenantId_DepartmentId] UNIQUE NONCLUSTERED ([TenantId], [DepartmentId]),
    CONSTRAINT [CK_Department_NoSelfParent] CHECK ([ParentDepartmentId] IS NULL OR [ParentDepartmentId] <> [DepartmentId]),
    CONSTRAINT [CK_Department_Status] CHECK ([Status] IN (N'ACTIVE', N'INACTIVE'))
);
GO

CREATE TABLE [dbo].[Role] (
    [RoleId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NULL,
    [Name] NVARCHAR(100) NOT NULL,
    [IsSystem] BIT NOT NULL,
    [Status] NVARCHAR(32) NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_Role] PRIMARY KEY CLUSTERED ([RoleId]),
    CONSTRAINT [CK_Role_Scope] CHECK (([IsSystem] = 1 AND [TenantId] IS NULL) OR ([IsSystem] = 0 AND [TenantId] IS NOT NULL)),
    CONSTRAINT [CK_Role_Status] CHECK ([Status] IN (N'ACTIVE', N'INACTIVE'))
);
GO

CREATE TABLE [dbo].[User] (
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NULL,
    [IsPlatformAdministrator] BIT NOT NULL,
    [EmployeeCode] NVARCHAR(50) NOT NULL,
    [NormalizedEmployeeCode] NVARCHAR(50) NOT NULL,
    [Email] NVARCHAR(320) NOT NULL,
    [NormalizedEmail] NVARCHAR(320) NOT NULL,
    [PasswordHash] NVARCHAR(MAX) NOT NULL,
    [SecurityStamp] NVARCHAR(64) NOT NULL,
    [FullName] NVARCHAR(200) NOT NULL,
    [DepartmentId] UNIQUEIDENTIFIER NULL,
    [Status] NVARCHAR(32) NOT NULL,
    [AccessFailedCount] INT NOT NULL,
    [LockoutEnd] DATETIMEOFFSET(7) NULL,
    [MustChangePassword] BIT NOT NULL,
    [LastLoginAt] DATETIMEOFFSET(7) NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
    [DeletedAt] DATETIMEOFFSET(7) NULL,
    [AvatarObjectKey] NVARCHAR(512) NULL,
    CONSTRAINT [PK_User] PRIMARY KEY CLUSTERED ([UserId]),
    CONSTRAINT [CK_User_AccessFailedCount] CHECK ([AccessFailedCount] >= 0),
    CONSTRAINT [CK_User_AccountPlane] CHECK (([TenantId] IS NULL AND [IsPlatformAdministrator] = 1 AND [DepartmentId] IS NULL) OR ([TenantId] IS NOT NULL AND [IsPlatformAdministrator] = 0)),
    CONSTRAINT [CK_User_Status] CHECK ([Status] IN (N'ACTIVE', N'INACTIVE', N'LOCKED'))
);
GO

CREATE TABLE [dbo].[RolePermission] (
    [RoleId] UNIQUEIDENTIFIER NOT NULL,
    [PermissionId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_RolePermission] PRIMARY KEY CLUSTERED ([RoleId], [PermissionId])
);
GO

CREATE TABLE [dbo].[AuthenticationSession] (
    [AuthenticationSessionId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [FamilyId] UNIQUEIDENTIFIER NOT NULL,
    [TokenHash] NVARCHAR(64) NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [ExpiresAt] DATETIMEOFFSET(7) NOT NULL,
    [RevokedAt] DATETIMEOFFSET(7) NULL,
    [ReplacedById] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_AuthenticationSession] PRIMARY KEY CLUSTERED ([AuthenticationSessionId]),
    CONSTRAINT [CK_AuthenticationSession_Expiry] CHECK ([ExpiresAt] > [CreatedAt]),
    CONSTRAINT [CK_AuthenticationSession_Hash] CHECK (LEN([TokenHash]) = 64 AND [TokenHash] NOT LIKE N'%[^0-9a-fA-F]%'),
    CONSTRAINT [CK_AuthenticationSession_Rotation] CHECK ([ReplacedById] IS NULL OR ([RevokedAt] IS NOT NULL AND [ReplacedById] <> [AuthenticationSessionId]))
);
GO

CREATE TABLE [dbo].[UserRole] (
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [RoleId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_UserRole] PRIMARY KEY CLUSTERED ([UserId], [RoleId])
);
GO

CREATE TABLE [dbo].[ManagementScope] (
    [ManagementScopeId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [DepartmentId] UNIQUEIDENTIFIER NOT NULL,
    [IncludeDescendants] BIT NOT NULL,
    [CreatedBy] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_ManagementScope] PRIMARY KEY CLUSTERED ([ManagementScopeId])
);
GO

CREATE TABLE [dbo].[Workflow] (
    [WorkflowId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [BusinessType] NVARCHAR(32) NOT NULL,
    [Status] NVARCHAR(32) NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_Workflow] PRIMARY KEY CLUSTERED ([WorkflowId]),
    CONSTRAINT [CK_Workflow_BusinessType] CHECK ([BusinessType] IN (N'REQUEST', N'TASK')),
    CONSTRAINT [CK_Workflow_Status] CHECK ([Status] IN (N'ACTIVE', N'DRAFT', N'INACTIVE'))
);
GO

CREATE TABLE [dbo].[WorkflowVersion] (
    [WorkflowVersionId] UNIQUEIDENTIFIER NOT NULL,
    [WorkflowId] UNIQUEIDENTIFIER NOT NULL,
    [VersionNo] INT NOT NULL,
    [Status] NVARCHAR(32) NOT NULL,
    [PublishedAt] DATETIMEOFFSET(7) NULL,
    [DefinitionJson] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [PK_WorkflowVersion] PRIMARY KEY CLUSTERED ([WorkflowVersionId]),
    CONSTRAINT [CK_WorkflowVersion_Status] CHECK ([Status] IN (N'DRAFT', N'PUBLISHED', N'RETIRED')),
    CONSTRAINT [CK_WorkflowVersion_JsonObject] CHECK (ISJSON([DefinitionJson]) = 1 AND LEFT(LTRIM([DefinitionJson]), 1) = N'{'),
    CONSTRAINT [CK_WorkflowVersion_Number] CHECK ([VersionNo] > 0),
    CONSTRAINT [CK_WorkflowVersion_Publication] CHECK (([Status] = N'DRAFT' AND [PublishedAt] IS NULL) OR ([Status] <> N'DRAFT' AND [PublishedAt] IS NOT NULL))
);
GO

CREATE TABLE [dbo].[WorkflowStep] (
    [WorkflowStepId] UNIQUEIDENTIFIER NOT NULL,
    [WorkflowVersionId] UNIQUEIDENTIFIER NOT NULL,
    [StepCode] NVARCHAR(80) NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [Type] NVARCHAR(32) NOT NULL,
    [OrderNo] INT NOT NULL,
    [ConfigJson] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [PK_WorkflowStep] PRIMARY KEY CLUSTERED ([WorkflowStepId]),
    CONSTRAINT [CK_WorkflowStep_Type] CHECK ([Type] IN (N'ACTION', N'APPROVAL', N'CONFIRMATION')),
    CONSTRAINT [CK_WorkflowStep_Code] CHECK (LEN([StepCode]) BETWEEN 1 AND 80 AND [StepCode] NOT LIKE N'%[^A-Z0-9_.-]%'),
    CONSTRAINT [CK_WorkflowStep_JsonObject] CHECK (ISJSON([ConfigJson]) = 1 AND LEFT(LTRIM([ConfigJson]), 1) = N'{'),
    CONSTRAINT [CK_WorkflowStep_Order] CHECK ([OrderNo] > 0)
);
GO

CREATE TABLE [dbo].[WorkflowTransition] (
    [TransitionId] UNIQUEIDENTIFIER NOT NULL,
    [WorkflowVersionId] UNIQUEIDENTIFIER NOT NULL,
    [FromState] NVARCHAR(50) NOT NULL,
    [ToState] NVARCHAR(50) NOT NULL,
    [GuardJson] NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_WorkflowTransition] PRIMARY KEY CLUSTERED ([TransitionId]),
    CONSTRAINT [CK_WorkflowTransition_JsonObject] CHECK ([GuardJson] IS NULL OR (ISJSON([GuardJson]) = 1 AND LEFT(LTRIM([GuardJson]), 1) = N'{'))
);
GO

CREATE TABLE [dbo].[Notification] (
    [NotificationId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [RecipientId] UNIQUEIDENTIFIER NOT NULL,
    [Type] NVARCHAR(80) NOT NULL,
    [ObjectType] NVARCHAR(40) NULL,
    [ObjectId] UNIQUEIDENTIFIER NULL,
    [Title] NVARCHAR(200) NOT NULL,
    [Content] NVARCHAR(MAX) NOT NULL,
    [ReadAt] DATETIMEOFFSET(7) NULL,
    [SentAt] DATETIMEOFFSET(7) NULL,
    [IdempotencyKey] NVARCHAR(180) NOT NULL,
    CONSTRAINT [PK_Notification] PRIMARY KEY CLUSTERED ([NotificationId]),
    CONSTRAINT [CK_Notification_Text] CHECK (LEN(LTRIM(RTRIM([Title]))) > 0 AND LEN(LTRIM(RTRIM([Content]))) > 0 AND LEN(LTRIM(RTRIM([IdempotencyKey]))) > 0),
    CONSTRAINT [CK_Notification_Type] CHECK ([Type] IN (
        N'TaskAssigned', N'TaskAccepted', N'TaskRejected', N'ProgressSubmitted', N'ResultSubmitted',
        N'TaskConfirmed', N'RequestSubmitted', N'RequestRouted', N'RequestReceived', N'RequestResolved',
        N'RequestRejected', N'RequestRevised', N'DeadlineWarning', N'Overdue', N'Escalation',
        N'ApprovalRequired', N'Mentioned', N'WatchedItemActivity', N'Announcement', N'MeetingInvitation'
    ))
);
GO

CREATE TABLE [dbo].[TenantSetting] (
    [TenantSettingId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Key] NVARCHAR(128) NOT NULL,
    [ValueJson] NVARCHAR(MAX) NOT NULL,
    [UpdatedBy] UNIQUEIDENTIFIER NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_TenantSetting] PRIMARY KEY CLUSTERED ([TenantSettingId]),
    CONSTRAINT [CK_TenantSetting_Key] CHECK ([Key] IN (
        N'ai.enabled', N'ai.auto_action_enabled', N'sla.pause_on_waiting_for_information',
        N'request.generic_service_allowed', N'notification.email_enabled',
        N'ai.auto_action_tools', N'attachment.allowed_content_types',
        N'ai.confidence_threshold', N'ai.monthly_call_limit', N'attachment.max_size_bytes', N'report.max_range_days'
    ))
);
GO

CREATE TABLE [dbo].[SLAProfile] (
    [SLAProfileId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [Status] NVARCHAR(32) NOT NULL,
    CONSTRAINT [PK_SLAProfile] PRIMARY KEY CLUSTERED ([SLAProfileId]),
    CONSTRAINT [CK_SLAProfile_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) BETWEEN 1 AND 200),
    CONSTRAINT [CK_SLAProfile_Status] CHECK ([Status] IN (N'ACTIVE', N'DRAFT', N'INACTIVE'))
);
GO

CREATE TABLE [dbo].[BusinessCalendar] (
    [CalendarId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [TimeZone] NVARCHAR(64) NOT NULL,
    [WorkingHoursJson] NVARCHAR(MAX) NOT NULL,
    [HolidaysJson] NVARCHAR(MAX) NOT NULL,
    CONSTRAINT [PK_BusinessCalendar] PRIMARY KEY CLUSTERED ([CalendarId]),
    CONSTRAINT [CK_BusinessCalendar_Holidays] CHECK (ISJSON([HolidaysJson]) = 1 AND LEFT(LTRIM([HolidaysJson]), 1) = N'['),
    CONSTRAINT [CK_BusinessCalendar_Hours] CHECK (ISJSON([WorkingHoursJson]) = 1 AND LEFT(LTRIM([WorkingHoursJson]), 1) = N'{')
);
GO

CREATE TABLE [dbo].[SLAVersion] (
    [SLAVersionId] UNIQUEIDENTIFIER NOT NULL,
    [SLAProfileId] UNIQUEIDENTIFIER NOT NULL,
    [VersionNo] INT NOT NULL,
    [TargetMinutes] INT NOT NULL,
    [WarningMinutes] INT NOT NULL,
    [CalendarId] UNIQUEIDENTIFIER NOT NULL,
    [EscalationConfigJson] NVARCHAR(MAX) NULL,
    CONSTRAINT [PK_SLAVersion] PRIMARY KEY CLUSTERED ([SLAVersionId]),
    CONSTRAINT [CK_SLAVersion_Escalation] CHECK ([EscalationConfigJson] IS NULL OR (ISJSON([EscalationConfigJson]) = 1 AND LEFT(LTRIM([EscalationConfigJson]), 1) = N'{')),
    CONSTRAINT [CK_SLAVersion_Number] CHECK ([VersionNo] > 0),
    CONSTRAINT [CK_SLAVersion_Thresholds] CHECK ([TargetMinutes] > 0 AND [WarningMinutes] >= 0 AND [WarningMinutes] < [TargetMinutes])
);
GO

CREATE TABLE [dbo].[Service] (
    [ServiceId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(80) NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    [Status] NVARCHAR(32) NOT NULL,
    [ActiveWorkflowVersionId] UNIQUEIDENTIFIER NULL,
    [ActiveSLAVersionId] UNIQUEIDENTIFIER NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_Service] PRIMARY KEY CLUSTERED ([ServiceId]),
    CONSTRAINT [CK_Service_Code] CHECK (LEN(LTRIM(RTRIM([Code]))) BETWEEN 1 AND 80),
    CONSTRAINT [CK_Service_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) BETWEEN 1 AND 200),
    CONSTRAINT [CK_Service_Status] CHECK ([Status] IN (N'ACTIVE', N'DRAFT', N'INACTIVE'))
);
GO

CREATE TABLE [dbo].[ServiceCategory] (
    [ServiceCategoryId] UNIQUEIDENTIFIER NOT NULL,
    [ServiceId] UNIQUEIDENTIFIER NOT NULL,
    [Code] NVARCHAR(80) NOT NULL,
    [Name] NVARCHAR(200) NOT NULL,
    [Status] NVARCHAR(32) NOT NULL,
    CONSTRAINT [PK_ServiceCategory] PRIMARY KEY CLUSTERED ([ServiceCategoryId]),
    CONSTRAINT [CK_ServiceCategory_Code] CHECK (LEN(LTRIM(RTRIM([Code]))) BETWEEN 1 AND 80),
    CONSTRAINT [CK_ServiceCategory_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) BETWEEN 1 AND 200),
    CONSTRAINT [CK_ServiceCategory_Status] CHECK ([Status] IN (N'ACTIVE', N'INACTIVE'))
);
GO

CREATE TABLE [dbo].[Request] (
    [RequestId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [RequesterId] UNIQUEIDENTIFIER NOT NULL,
    [ServiceId] UNIQUEIDENTIFIER NOT NULL,
    [CategoryId] UNIQUEIDENTIFIER NOT NULL,
    [ParentRequestId] UNIQUEIDENTIFIER NULL,
    [RevisedFromRequestId] UNIQUEIDENTIFIER NULL,
    [Title] NVARCHAR(300) NOT NULL,
    [Description] NVARCHAR(MAX) NOT NULL,
    [Priority] NVARCHAR(32) NOT NULL CONSTRAINT [DF_Request_Priority] DEFAULT (N'MEDIUM'),
    [Status] NVARCHAR(32) NOT NULL CONSTRAINT [DF_Request_Status] DEFAULT (N'DRAFT'),
    [WorkflowVersionId] UNIQUEIDENTIFIER NULL,
    [SLAVersionId] UNIQUEIDENTIFIER NULL,
    [ResolvedAt] DATETIMEOFFSET(7) NULL,
    [ClosedAt] DATETIMEOFFSET(7) NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
    [DeletedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_Request] PRIMARY KEY CLUSTERED ([RequestId]),
    CONSTRAINT [CK_Request_Description] CHECK (LEN(LTRIM(RTRIM([Description]))) > 0),
    CONSTRAINT [CK_Request_Parent] CHECK ([ParentRequestId] IS NULL OR [ParentRequestId] <> [RequestId]),
    CONSTRAINT [CK_Request_Revision] CHECK ([RevisedFromRequestId] IS NULL OR [RevisedFromRequestId] <> [RequestId]),
    CONSTRAINT [CK_Request_Title] CHECK (LEN(LTRIM(RTRIM([Title]))) BETWEEN 1 AND 300),
    CONSTRAINT [CK_Request_Priority] CHECK ([Priority] IN (N'CRITICAL', N'HIGH', N'LOW', N'MEDIUM')),
    CONSTRAINT [CK_Request_Status] CHECK ([Status] IN (
        N'CANCELLED', N'CLOSED', N'CONFIRMED', N'DRAFT', N'IN_PROGRESS', N'OVERDUE',
        N'RECEIVED', N'REJECTED', N'RESOLVED', N'ROUTED', N'SUBMITTED', N'WAITING_FOR_INFORMATION'
    ))
);
GO

CREATE TABLE [dbo].[RequestResolution] (
    [ResolutionId] UNIQUEIDENTIFIER NOT NULL,
    [RequestId] UNIQUEIDENTIFIER NOT NULL,
    [ResolverId] UNIQUEIDENTIFIER NOT NULL,
    [Content] NVARCHAR(MAX) NOT NULL,
    [RevisionNo] INT NOT NULL CONSTRAINT [DF_RequestResolution_RevisionNo] DEFAULT (1),
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_RequestResolution] PRIMARY KEY CLUSTERED ([ResolutionId]),
    CONSTRAINT [CK_RequestResolution_Content] CHECK (LEN(LTRIM(RTRIM([Content]))) > 0),
    CONSTRAINT [CK_RequestResolution_Revision] CHECK ([RevisionNo] > 0)
);
GO

CREATE TABLE [dbo].[Task] (
    [TaskId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [RequestId] UNIQUEIDENTIFIER NULL,
    [CreatorId] UNIQUEIDENTIFIER NOT NULL,
    [Title] NVARCHAR(300) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    [Priority] NVARCHAR(32) NOT NULL CONSTRAINT [DF_Task_Priority] DEFAULT (N'MEDIUM'),
    [Deadline] DATETIMEOFFSET(7) NULL,
    [Status] NVARCHAR(32) NOT NULL CONSTRAINT [DF_Task_Status] DEFAULT (N'DRAFT'),
    [WorkflowVersionId] UNIQUEIDENTIFIER NULL,
    [SLAVersionId] UNIQUEIDENTIFIER NULL,
    [CompletedAt] DATETIMEOFFSET(7) NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
    [DeletedAt] DATETIMEOFFSET(7) NULL,
    [ParentTaskId] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_Task] PRIMARY KEY CLUSTERED ([TaskId]),
    CONSTRAINT [CK_Task_Title] CHECK (LEN(LTRIM(RTRIM([Title]))) BETWEEN 1 AND 300),
    CONSTRAINT [CK_Task_Priority] CHECK ([Priority] IN (N'CRITICAL', N'HIGH', N'LOW', N'MEDIUM')),
    CONSTRAINT [CK_Task_Status] CHECK ([Status] IN (
        N'ACCEPTED', N'ASSIGNED', N'CANCELLED', N'COMPLETED', N'CONFIRMED',
        N'DRAFT', N'IN_PROGRESS', N'OVERDUE', N'REJECTED', N'SUBMITTED'
    ))
);
GO

CREATE TABLE [dbo].[TaskChecklistItem] (
    [ChecklistItemId] UNIQUEIDENTIFIER NOT NULL,
    [TaskId] UNIQUEIDENTIFIER NOT NULL,
    [Title] NVARCHAR(300) NOT NULL,
    [SortOrder] INT NOT NULL CONSTRAINT [DF_TaskChecklistItem_SortOrder] DEFAULT (1),
    [IsCompleted] BIT NOT NULL CONSTRAINT [DF_TaskChecklistItem_IsCompleted] DEFAULT (0),
    [CompletedBy] UNIQUEIDENTIFIER NULL,
    [CompletedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_TaskChecklistItem] PRIMARY KEY CLUSTERED ([ChecklistItemId]),
    CONSTRAINT [CK_TaskChecklistItem_Completion] CHECK (([IsCompleted] = 1 AND [CompletedBy] IS NOT NULL AND [CompletedAt] IS NOT NULL) OR ([IsCompleted] = 0 AND [CompletedBy] IS NULL AND [CompletedAt] IS NULL)),
    CONSTRAINT [CK_TaskChecklistItem_Order] CHECK ([SortOrder] > 0),
    CONSTRAINT [CK_TaskChecklistItem_Title] CHECK (LEN(LTRIM(RTRIM([Title]))) BETWEEN 1 AND 300)
);
GO

CREATE TABLE [dbo].[TaskProgressReport] (
    [ProgressReportId] UNIQUEIDENTIFIER NOT NULL,
    [TaskId] UNIQUEIDENTIFIER NOT NULL,
    [AuthorId] UNIQUEIDENTIFIER NOT NULL,
    [Percent] SMALLINT NOT NULL CONSTRAINT [DF_TaskProgressReport_Percent] DEFAULT (0),
    [Content] NVARCHAR(MAX) NULL,
    [SubmittedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_TaskProgressReport] PRIMARY KEY CLUSTERED ([ProgressReportId]),
    CONSTRAINT [CK_TaskProgressReport_Percent] CHECK ([Percent] >= 0 AND [Percent] <= 100)
);
GO

CREATE TABLE [dbo].[TaskResult] (
    [TaskResultId] UNIQUEIDENTIFIER NOT NULL,
    [TaskId] UNIQUEIDENTIFIER NOT NULL,
    [AuthorId] UNIQUEIDENTIFIER NOT NULL,
    [Content] NVARCHAR(MAX) NULL,
    [RevisionNo] INT NOT NULL CONSTRAINT [DF_TaskResult_RevisionNo] DEFAULT (1),
    [SubmittedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_TaskResult] PRIMARY KEY CLUSTERED ([TaskResultId]),
    CONSTRAINT [CK_TaskResult_Revision] CHECK ([RevisionNo] > 0)
);
GO

CREATE TABLE [dbo].[TaskAssignment] (
    [TaskAssignmentId] UNIQUEIDENTIFIER NOT NULL,
    [TaskId] UNIQUEIDENTIFIER NOT NULL,
    [DepartmentId] UNIQUEIDENTIFIER NULL,
    [UserId] UNIQUEIDENTIFIER NULL,
    [AssignedBy] UNIQUEIDENTIFIER NOT NULL,
    [AssignedAt] DATETIMEOFFSET(7) NOT NULL,
    [AcceptedAt] DATETIMEOFFSET(7) NULL,
    [RejectedAt] DATETIMEOFFSET(7) NULL,
    [RejectionReason] NVARCHAR(MAX) NULL,
    [EndedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_TaskAssignment] PRIMARY KEY CLUSTERED ([TaskAssignmentId]),
    CONSTRAINT [CK_TaskAssignment_Reason] CHECK (([RejectedAt] IS NULL AND [RejectionReason] IS NULL) OR ([RejectedAt] IS NOT NULL AND [RejectionReason] IS NOT NULL AND LEN(LTRIM(RTRIM([RejectionReason]))) > 0)),
    CONSTRAINT [CK_TaskAssignment_Receipt] CHECK (NOT ([AcceptedAt] IS NOT NULL AND [RejectedAt] IS NOT NULL)),
    CONSTRAINT [CK_TaskAssignment_Target] CHECK ([DepartmentId] IS NOT NULL OR [UserId] IS NOT NULL),
    CONSTRAINT [CK_TaskAssignment_Times] CHECK (
        ([AcceptedAt] IS NULL OR [AcceptedAt] >= [AssignedAt]) AND
        ([RejectedAt] IS NULL OR [RejectedAt] >= [AssignedAt]) AND
        ([EndedAt] IS NULL OR ([EndedAt] >= [AssignedAt] AND ([AcceptedAt] IS NULL OR [AcceptedAt] <= [EndedAt]) AND ([RejectedAt] IS NULL OR [RejectedAt] <= [EndedAt])))
    )
);
GO

CREATE TABLE [dbo].[Confirmation] (
    [ConfirmationId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [ObjectType] NVARCHAR(32) NOT NULL CONSTRAINT [DF_Confirmation_ObjectType] DEFAULT (N'TASK'),
    [ObjectId] UNIQUEIDENTIFIER NOT NULL,
    [MilestoneType] NVARCHAR(32) NOT NULL CONSTRAINT [DF_Confirmation_MilestoneType] DEFAULT (N'RESULT'),
    [ActorId] UNIQUEIDENTIFIER NOT NULL,
    [Decision] NVARCHAR(32) NOT NULL CONSTRAINT [DF_Confirmation_Decision] DEFAULT (N'CONFIRMED'),
    [Note] NVARCHAR(MAX) NULL,
    [ConfirmedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_Confirmation] PRIMARY KEY CLUSTERED ([ConfirmationId]),
    CONSTRAINT [CK_Confirmation_Decision] CHECK ([Decision] IN (N'CONFIRMED', N'REJECTED')),
    CONSTRAINT [CK_Confirmation_Milestone] CHECK ([MilestoneType] IN (N'RECEIVE', N'RESULT', N'RESOLUTION')),
    CONSTRAINT [CK_Confirmation_ObjectType] CHECK ([ObjectType] IN (N'TASK', N'REQUEST'))
);
GO

CREATE TABLE [dbo].[RequestRouting] (
    [RequestRoutingId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [RequestId] UNIQUEIDENTIFIER NOT NULL,
    [FromDepartmentId] UNIQUEIDENTIFIER NULL,
    [ToDepartmentId] UNIQUEIDENTIFIER NULL,
    [FromUserId] UNIQUEIDENTIFIER NULL,
    [ToUserId] UNIQUEIDENTIFIER NULL,
    [RoutedBy] UNIQUEIDENTIFIER NOT NULL,
    [RoutedAt] DATETIMEOFFSET(7) NOT NULL,
    [Reason] NVARCHAR(2000) NULL,
    [Source] NVARCHAR(20) NOT NULL,
    CONSTRAINT [PK_RequestRouting] PRIMARY KEY CLUSTERED ([RequestRoutingId]),
    CONSTRAINT [CK_RequestRouting_Source] CHECK ([Source] IN (N'MANUAL', N'AI', N'RULE'))
);
GO

CREATE TABLE [dbo].[Comment] (
    [CommentId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [ObjectType] NVARCHAR(32) NOT NULL,
    [ObjectId] UNIQUEIDENTIFIER NOT NULL,
    [AuthorId] UNIQUEIDENTIFIER NOT NULL,
    [Content] NVARCHAR(MAX) NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [EditedAt] DATETIMEOFFSET(7) NULL,
    [DeletedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_Comment] PRIMARY KEY CLUSTERED ([CommentId]),
    CONSTRAINT [CK_Comment_ObjectType] CHECK ([ObjectType] IN (N'TASK', N'REQUEST', N'RESULT', N'PROGRESS'))
);
GO

CREATE TABLE [dbo].[Attachment] (
    [AttachmentId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [ObjectType] NVARCHAR(32) NOT NULL,
    [ObjectId] UNIQUEIDENTIFIER NOT NULL,
    [UploadedBy] UNIQUEIDENTIFIER NOT NULL,
    [FileName] NVARCHAR(255) NOT NULL,
    [ContentType] NVARCHAR(100) NOT NULL,
    [SizeBytes] BIGINT NOT NULL,
    [ObjectKey] NVARCHAR(500) NOT NULL,
    [Hash] NVARCHAR(128) NOT NULL,
    [Status] NVARCHAR(32) NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [DeletedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_Attachment] PRIMARY KEY CLUSTERED ([AttachmentId]),
    CONSTRAINT [CK_Attachment_ObjectType] CHECK ([ObjectType] IN (N'TASK', N'REQUEST', N'COMMENT', N'RESULT', N'PROGRESS')),
    CONSTRAINT [CK_Attachment_SizeBytes] CHECK ([SizeBytes] > 0 AND [SizeBytes] <= 524288000),
    CONSTRAINT [CK_Attachment_Status] CHECK ([Status] IN (N'UPLOADING', N'READY', N'FAILED', N'DELETED'))
);
GO

CREATE TABLE [dbo].[AIInteraction] (
    [AIInteractionId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [Feature] NVARCHAR(80) NOT NULL,
    [ModelName] NVARCHAR(120) NOT NULL,
    [InputRef] NVARCHAR(255) NULL,
    [OutputRef] NVARCHAR(255) NULL,
    [Status] NVARCHAR(32) NOT NULL,
    [LatencyMs] INT NULL,
    [TokenUsage] NVARCHAR(MAX) NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_AIInteraction] PRIMARY KEY CLUSTERED ([AIInteractionId]),
    CONSTRAINT [CK_AIInteraction_Status] CHECK ([Status] IN (N'SUCCEEDED', N'FAILED', N'TIMEOUT', N'REJECTED')),
    CONSTRAINT [CK_AIInteraction_TokenUsage] CHECK ([TokenUsage] IS NULL OR ISJSON([TokenUsage]) = 1)
);
GO

CREATE TABLE [dbo].[AIRecommendation] (
    [RecommendationId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [AIInteractionId] UNIQUEIDENTIFIER NOT NULL,
    [ObjectType] NVARCHAR(32) NOT NULL,
    [ObjectId] UNIQUEIDENTIFIER NULL,
    [RecommendationType] NVARCHAR(80) NOT NULL,
    [PayloadJson] NVARCHAR(MAX) NOT NULL,
    [Confidence] DECIMAL(5,4) NULL,
    [HumanDecision] NVARCHAR(32) NULL,
    [DecisionBy] UNIQUEIDENTIFIER NULL,
    [DecidedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_AIRecommendation] PRIMARY KEY CLUSTERED ([RecommendationId]),
    CONSTRAINT [CK_AIRecommendation_HumanDecision] CHECK ([HumanDecision] IS NULL OR [HumanDecision] IN (N'ACCEPTED', N'OVERRIDDEN', N'DISMISSED')),
    CONSTRAINT [CK_AIRecommendation_ObjectType] CHECK ([ObjectType] IN (N'TASK', N'REQUEST')),
    CONSTRAINT [CK_AIRecommendation_PayloadJson] CHECK (ISJSON([PayloadJson]) = 1)
);
GO

CREATE TABLE [dbo].[AIAgentAction] (
    [AIAgentActionId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [AIInteractionId] UNIQUEIDENTIFIER NOT NULL,
    [ToolName] NVARCHAR(100) NOT NULL,
    [ArgsJson] NVARCHAR(MAX) NOT NULL,
    [AuthorizationResult] NVARCHAR(32) NOT NULL,
    [ExecutionStatus] NVARCHAR(32) NOT NULL,
    [ResultRef] NVARCHAR(255) NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_AIAgentAction] PRIMARY KEY CLUSTERED ([AIAgentActionId]),
    CONSTRAINT [CK_AIAgentAction_AuthorizationResult] CHECK ([AuthorizationResult] IN (N'AUTHORIZED', N'DENIED')),
    CONSTRAINT [CK_AIAgentAction_ExecutionStatus] CHECK ([ExecutionStatus] IN (N'NOT_EXECUTED', N'SUCCEEDED', N'FAILED')),
    CONSTRAINT [CK_AIAgentAction_ArgsJson] CHECK (ISJSON([ArgsJson]) = 1)
);
GO

CREATE TABLE [dbo].[DeadlineReminderSchedule] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [TaskId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [LeadTimeMinutes] INT NOT NULL,
    [RemindAt] DATETIMEOFFSET(7) NOT NULL,
    [TriggerType] NVARCHAR(50) NOT NULL,
    [Status] NVARCHAR(30) NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_DeadlineReminderSchedule] PRIMARY KEY CLUSTERED ([Id])
);
GO

CREATE TABLE [dbo].[WebPushSubscription] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [Endpoint] VARCHAR(1000) NOT NULL,
    [P256dh] NVARCHAR(250) NOT NULL,
    [Auth] NVARCHAR(150) NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [LastActiveAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_WebPushSubscription] PRIMARY KEY CLUSTERED ([Id])
);
GO

CREATE TABLE [dbo].[NotificationLog] (
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [TaskId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [TriggerType] NVARCHAR(50) NOT NULL,
    [ScheduledAt] DATETIMEOFFSET(7) NOT NULL,
    [Channel] NVARCHAR(30) NOT NULL,
    [Status] NVARCHAR(30) NOT NULL,
    [DeliveredAt] DATETIMEOFFSET(7) NOT NULL,
    [ErrorMessage] NVARCHAR(1000) NULL,
    CONSTRAINT [PK_NotificationLog] PRIMARY KEY CLUSTERED ([Id])
);
GO

CREATE TABLE [dbo].[AIVectorMemory] (
    [MemoryId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [MemoryScope] NVARCHAR(64) NOT NULL,
    [Title] NVARCHAR(255) NOT NULL,
    [Content] NVARCHAR(MAX) NOT NULL,
    [MetadataJson] NVARCHAR(MAX) NULL,
    [EmbeddingJson] NVARCHAR(MAX) NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_AIVectorMemory] PRIMARY KEY CLUSTERED ([MemoryId]),
    CONSTRAINT [CK_AIVectorMemory_MetadataJson] CHECK ([MetadataJson] IS NULL OR ISJSON([MetadataJson]) = 1),
    CONSTRAINT [CK_AIVectorMemory_EmbeddingJson] CHECK (ISJSON([EmbeddingJson]) = 1)
);
GO

CREATE TABLE [dbo].[AiLearnedChoice] (
    [ChoiceId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NULL,
    [SemanticKey] NVARCHAR(120) NOT NULL,
    [ChoiceValue] NVARCHAR(500) NOT NULL,
    [ChoiceLabel] NVARCHAR(255) NOT NULL,
    [Scope] NVARCHAR(32) NOT NULL CONSTRAINT [DF_AiLearnedChoice_Scope] DEFAULT (N'PERSONAL'),
    [UsageCount] INT NOT NULL CONSTRAINT [DF_AiLearnedChoice_UsageCount] DEFAULT (1),
    [LastUsedAt] DATETIMEOFFSET(7) NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_AiLearnedChoice] PRIMARY KEY CLUSTERED ([ChoiceId])
);
GO

CREATE TABLE [dbo].[RequestEscalation] (
    [RequestEscalationId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [RequestId] UNIQUEIDENTIFIER NOT NULL,
    [EscalatedById] UNIQUEIDENTIFIER NOT NULL,
    [Reason] NVARCHAR(2000) NOT NULL,
    [State] NVARCHAR(20) NOT NULL,
    [EscalatedAt] DATETIMEOFFSET(7) NOT NULL,
    [DecidedById] UNIQUEIDENTIFIER NULL,
    [DecisionNote] NVARCHAR(2000) NULL,
    [DecidedAt] DATETIMEOFFSET(7) NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_RequestEscalation] PRIMARY KEY CLUSTERED ([RequestEscalationId]),
    CONSTRAINT [CK_RequestEscalation_Reason] CHECK (LEN(LTRIM(RTRIM([Reason]))) > 0),
    CONSTRAINT [CK_RequestEscalation_State] CHECK ([State] IN (N'PENDING', N'APPROVED', N'REJECTED'))
);
GO

CREATE TABLE [dbo].[CommentMention] (
    [CommentMentionId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [CommentId] UNIQUEIDENTIFIER NOT NULL,
    [ObjectType] NVARCHAR(32) NOT NULL,
    [ObjectId] UNIQUEIDENTIFIER NOT NULL,
    [MentionedUserId] UNIQUEIDENTIFIER NOT NULL,
    [MentionedByUserId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    CONSTRAINT [PK_CommentMention] PRIMARY KEY CLUSTERED ([CommentMentionId]),
    CONSTRAINT [CK_CommentMention_ObjectType] CHECK ([ObjectType] IN (N'TASK', N'REQUEST'))
);
GO

CREATE TABLE [dbo].[WorkItemWatcher] (
    [WorkItemWatcherId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [ObjectType] NVARCHAR(32) NOT NULL,
    [ObjectId] UNIQUEIDENTIFIER NOT NULL,
    [UserId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [LastNotifiedAt] DATETIMEOFFSET(7) NULL,
    [RemovedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_WorkItemWatcher] PRIMARY KEY CLUSTERED ([WorkItemWatcherId]),
    CONSTRAINT [CK_WorkItemWatcher_ObjectType] CHECK ([ObjectType] IN (N'TASK', N'REQUEST'))
);
GO

CREATE TABLE [dbo].[Label] (
    [LabelId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [Name] NVARCHAR(60) NOT NULL,
    [NormalizedName] NVARCHAR(60) NOT NULL,
    [Colour] NVARCHAR(7) NOT NULL,
    [CreatedBy] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [UpdatedAt] DATETIMEOFFSET(7) NULL,
    [ArchivedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_Label] PRIMARY KEY CLUSTERED ([LabelId]),
    CONSTRAINT [CK_Label_Colour] CHECK (LEN([Colour]) = 7 AND [Colour] LIKE N'#[0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f][0-9A-Fa-f]'),
    CONSTRAINT [CK_Label_Name] CHECK (LEN(LTRIM(RTRIM([Name]))) > 0)
);
GO

CREATE TABLE [dbo].[WorkItemLabel] (
    [WorkItemLabelId] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] UNIQUEIDENTIFIER NOT NULL,
    [ObjectType] NVARCHAR(32) NOT NULL,
    [ObjectId] UNIQUEIDENTIFIER NOT NULL,
    [LabelId] UNIQUEIDENTIFIER NOT NULL,
    [CreatedBy] UNIQUEIDENTIFIER NOT NULL,
    [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
    [RemovedAt] DATETIMEOFFSET(7) NULL,
    CONSTRAINT [PK_WorkItemLabel] PRIMARY KEY CLUSTERED ([WorkItemLabelId]),
    CONSTRAINT [CK_WorkItemLabel_ObjectType] CHECK ([ObjectType] IN (N'TASK', N'REQUEST'))
);
GO

-- =============================================================================
-- 4. INDEXES
-- =============================================================================

-- Hangfire indexes
CREATE NONCLUSTERED INDEX [ix_hangfire_counter_expireat] ON [hangfire].[counter] ([expireat]);
CREATE NONCLUSTERED INDEX [ix_hangfire_counter_key] ON [hangfire].[counter] ([key]);
CREATE NONCLUSTERED INDEX [ix_hangfire_hash_expireat] ON [hangfire].[hash] ([expireat]);
CREATE NONCLUSTERED INDEX [ix_hangfire_job_expireat] ON [hangfire].[job] ([expireat]);
CREATE NONCLUSTERED INDEX [ix_hangfire_job_statename] ON [hangfire].[job] ([statename]);
CREATE NONCLUSTERED INDEX [ix_hangfire_job_statename_is_not_null] ON [hangfire].[job] ([statename]) INCLUDE ([id]) WHERE ([statename] IS NOT NULL);
CREATE NONCLUSTERED INDEX [ix_hangfire_jobparameter_jobidandname] ON [hangfire].[jobparameter] ([jobid], [name]);
CREATE NONCLUSTERED INDEX [ix_hangfire_jobqueue_fetchedat_queue_jobid] ON [hangfire].[jobqueue] ([fetchedat], [queue], [jobid]);
CREATE NONCLUSTERED INDEX [ix_hangfire_jobqueue_jobidandqueue] ON [hangfire].[jobqueue] ([jobid], [queue]);
CREATE NONCLUSTERED INDEX [ix_hangfire_jobqueue_queueandfetchedat] ON [hangfire].[jobqueue] ([queue], [fetchedat]);
CREATE NONCLUSTERED INDEX [ix_hangfire_list_expireat] ON [hangfire].[list] ([expireat]);
CREATE NONCLUSTERED INDEX [ix_hangfire_set_expireat] ON [hangfire].[set] ([expireat]);
CREATE NONCLUSTERED INDEX [ix_hangfire_set_key_score] ON [hangfire].[set] ([key], [score]);
CREATE NONCLUSTERED INDEX [ix_hangfire_state_jobid] ON [hangfire].[state] ([jobid]);
GO

-- Application indexes
CREATE NONCLUSTERED INDEX [IX_AIAgentAction_TenantId_AIInteractionId] ON [dbo].[AIAgentAction] ([TenantId], [AIInteractionId]);
CREATE NONCLUSTERED INDEX [IX_AIAgentAction_TenantId_CreatedAt] ON [dbo].[AIAgentAction] ([TenantId], [CreatedAt]);

CREATE NONCLUSTERED INDEX [IX_AIInteraction_TenantId_CreatedAt] ON [dbo].[AIInteraction] ([TenantId], [CreatedAt]);
CREATE NONCLUSTERED INDEX [IX_AIInteraction_TenantId_UserId] ON [dbo].[AIInteraction] ([TenantId], [UserId]);

CREATE NONCLUSTERED INDEX [IX_AIRecommendation_TenantId_AIInteractionId] ON [dbo].[AIRecommendation] ([TenantId], [AIInteractionId]);
CREATE NONCLUSTERED INDEX [IX_AIRecommendation_TenantId_ObjectType_ObjectId] ON [dbo].[AIRecommendation] ([TenantId], [ObjectType], [ObjectId]);

CREATE NONCLUSTERED INDEX [IX_AIVectorMemory_TenantId_CreatedAt] ON [dbo].[AIVectorMemory] ([TenantId], [CreatedAt]);
CREATE NONCLUSTERED INDEX [IX_AIVectorMemory_TenantId_MemoryScope] ON [dbo].[AIVectorMemory] ([TenantId], [MemoryScope]);

CREATE NONCLUSTERED INDEX [IX_AiLearnedChoice_TenantId_SemanticKey_UsageCount] ON [dbo].[AiLearnedChoice] ([TenantId], [SemanticKey], [UsageCount]);
CREATE NONCLUSTERED INDEX [IX_AiLearnedChoice_TenantId_UserId_SemanticKey] ON [dbo].[AiLearnedChoice] ([TenantId], [UserId], [SemanticKey]);
CREATE NONCLUSTERED INDEX [IX_AiLearnedChoice_UserId] ON [dbo].[AiLearnedChoice] ([UserId]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_Attachment_TenantId_ObjectKey] ON [dbo].[Attachment] ([TenantId], [ObjectKey]);
CREATE NONCLUSTERED INDEX [IX_Attachment_TenantId_ObjectType_ObjectId_CreatedAt] ON [dbo].[Attachment] ([TenantId], [ObjectType], [ObjectId], [CreatedAt]);
CREATE NONCLUSTERED INDEX [IX_Attachment_UploadedBy] ON [dbo].[Attachment] ([UploadedBy]);

CREATE NONCLUSTERED INDEX [IX_AuditLog_TenantId_CreatedAt_AuditLogId] ON [dbo].[AuditLog] ([TenantId], [CreatedAt], [AuditLogId]);

CREATE NONCLUSTERED INDEX [IX_AuthenticationSession_ExpiresAt] ON [dbo].[AuthenticationSession] ([ExpiresAt]);
CREATE NONCLUSTERED INDEX [IX_AuthenticationSession_FamilyId] ON [dbo].[AuthenticationSession] ([FamilyId]);
CREATE NONCLUSTERED INDEX [IX_AuthenticationSession_ReplacedById] ON [dbo].[AuthenticationSession] ([ReplacedById]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_AuthenticationSession_TokenHash] ON [dbo].[AuthenticationSession] ([TokenHash]);
CREATE NONCLUSTERED INDEX [IX_AuthenticationSession_UserId] ON [dbo].[AuthenticationSession] ([UserId]);

CREATE NONCLUSTERED INDEX [IX_BusinessCalendar_TenantId] ON [dbo].[BusinessCalendar] ([TenantId]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_CommentMention_CommentId_MentionedUserId] ON [dbo].[CommentMention] ([CommentId], [MentionedUserId]);
CREATE NONCLUSTERED INDEX [IX_CommentMention_MentionedUserId] ON [dbo].[CommentMention] ([MentionedUserId]);
CREATE NONCLUSTERED INDEX [IX_CommentMention_TenantId_MentionedUserId_CreatedAt] ON [dbo].[CommentMention] ([TenantId], [MentionedUserId], [CreatedAt]);

CREATE NONCLUSTERED INDEX [IX_Comment_AuthorId] ON [dbo].[Comment] ([AuthorId]);
CREATE NONCLUSTERED INDEX [IX_Comment_TenantId_ObjectType_ObjectId_CreatedAt] ON [dbo].[Comment] ([TenantId], [ObjectType], [ObjectId], [CreatedAt]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_Company_Code] ON [dbo].[Company] ([Code]);

CREATE NONCLUSTERED INDEX [IX_Confirmation_ActorId] ON [dbo].[Confirmation] ([ActorId]);
CREATE NONCLUSTERED INDEX [IX_Confirmation_TenantId_ObjectType_ObjectId_ConfirmedAt] ON [dbo].[Confirmation] ([TenantId], [ObjectType], [ObjectId], [ConfirmedAt]);

CREATE NONCLUSTERED INDEX [IX_DeadlineReminderSchedule_TenantId_Status_RemindAt] ON [dbo].[DeadlineReminderSchedule] ([TenantId], [Status], [RemindAt]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_DeadlineReminderSchedule_TenantId_TaskId_UserId_TriggerType] ON [dbo].[DeadlineReminderSchedule] ([TenantId], [TaskId], [UserId], [TriggerType]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_Department_TenantId_Code] ON [dbo].[Department] ([TenantId], [Code]);
CREATE NONCLUSTERED INDEX [IX_Department_TenantId_ParentDepartmentId] ON [dbo].[Department] ([TenantId], [ParentDepartmentId]);

CREATE NONCLUSTERED INDEX [IX_Label_CreatedBy] ON [dbo].[Label] ([CreatedBy]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_Label_TenantId_NormalizedName] ON [dbo].[Label] ([TenantId], [NormalizedName]);

CREATE NONCLUSTERED INDEX [IX_ManagementScope_CreatedBy] ON [dbo].[ManagementScope] ([CreatedBy]);
CREATE NONCLUSTERED INDEX [IX_ManagementScope_TenantId_DepartmentId] ON [dbo].[ManagementScope] ([TenantId], [DepartmentId]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_ManagementScope_TenantId_UserId_DepartmentId] ON [dbo].[ManagementScope] ([TenantId], [UserId], [DepartmentId]);
CREATE NONCLUSTERED INDEX [IX_ManagementScope_UserId] ON [dbo].[ManagementScope] ([UserId]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_NotificationLog_TenantId_TaskId_UserId_TriggerType_Scheduled] ON [dbo].[NotificationLog] ([TenantId], [TaskId], [UserId], [TriggerType], [ScheduledAt]);

CREATE NONCLUSTERED INDEX [IX_Notification_RecipientId] ON [dbo].[Notification] ([RecipientId]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_Notification_TenantId_IdempotencyKey] ON [dbo].[Notification] ([TenantId], [IdempotencyKey]);
CREATE NONCLUSTERED INDEX [IX_Notification_TenantId_RecipientId_NotificationId] ON [dbo].[Notification] ([TenantId], [RecipientId], [NotificationId]);
CREATE NONCLUSTERED INDEX [IX_Notification_TenantId_RecipientId_ReadAt] ON [dbo].[Notification] ([TenantId], [RecipientId], [ReadAt]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_Permission_Code] ON [dbo].[Permission] ([Code]);

CREATE NONCLUSTERED INDEX [IX_RequestEscalation_DecidedById] ON [dbo].[RequestEscalation] ([DecidedById]);
CREATE NONCLUSTERED INDEX [IX_RequestEscalation_EscalatedById] ON [dbo].[RequestEscalation] ([EscalatedById]);
CREATE NONCLUSTERED INDEX [IX_RequestEscalation_RequestId] ON [dbo].[RequestEscalation] ([RequestId]);
CREATE NONCLUSTERED INDEX [IX_RequestEscalation_TenantId_RequestId_EscalatedAt] ON [dbo].[RequestEscalation] ([TenantId], [RequestId], [EscalatedAt]);
CREATE NONCLUSTERED INDEX [IX_RequestEscalation_TenantId_State] ON [dbo].[RequestEscalation] ([TenantId], [State]);

CREATE NONCLUSTERED INDEX [IX_RequestResolution_RequestId_RevisionNo] ON [dbo].[RequestResolution] ([RequestId], [RevisionNo]);
CREATE NONCLUSTERED INDEX [IX_RequestResolution_ResolverId] ON [dbo].[RequestResolution] ([ResolverId]);

CREATE NONCLUSTERED INDEX [IX_RequestRouting_TenantId_RequestId_RoutedAt] ON [dbo].[RequestRouting] ([TenantId], [RequestId], [RoutedAt]);

CREATE NONCLUSTERED INDEX [IX_Request_CategoryId] ON [dbo].[Request] ([CategoryId]);
CREATE NONCLUSTERED INDEX [IX_Request_ParentRequestId] ON [dbo].[Request] ([ParentRequestId]);
CREATE NONCLUSTERED INDEX [IX_Request_RequesterId] ON [dbo].[Request] ([RequesterId]);
CREATE NONCLUSTERED INDEX [IX_Request_RevisedFromRequestId] ON [dbo].[Request] ([RevisedFromRequestId]);
CREATE NONCLUSTERED INDEX [IX_Request_SLAVersionId] ON [dbo].[Request] ([SLAVersionId]);
CREATE NONCLUSTERED INDEX [IX_Request_ServiceId] ON [dbo].[Request] ([ServiceId]);
CREATE NONCLUSTERED INDEX [IX_Request_TenantId_RequesterId_CreatedAt] ON [dbo].[Request] ([TenantId], [RequesterId], [CreatedAt]);
CREATE NONCLUSTERED INDEX [IX_Request_TenantId_Status_CreatedAt] ON [dbo].[Request] ([TenantId], [Status], [CreatedAt]);
CREATE NONCLUSTERED INDEX [IX_Request_Tenant_DeletedAt] ON [dbo].[Request] ([TenantId], [DeletedAt]);
CREATE NONCLUSTERED INDEX [IX_Request_WorkflowVersionId] ON [dbo].[Request] ([WorkflowVersionId]);

CREATE NONCLUSTERED INDEX [IX_RolePermission_PermissionId] ON [dbo].[RolePermission] ([PermissionId]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_Role_TenantId_Name] ON [dbo].[Role] ([TenantId], [Name]);

CREATE NONCLUSTERED INDEX [IX_SLAProfile_TenantId_Status_Name] ON [dbo].[SLAProfile] ([TenantId], [Status], [Name]);

CREATE NONCLUSTERED INDEX [IX_SLAVersion_CalendarId] ON [dbo].[SLAVersion] ([CalendarId]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_SLAVersion_SLAProfileId_VersionNo] ON [dbo].[SLAVersion] ([SLAProfileId], [VersionNo]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_ServiceCategory_ServiceId_Code] ON [dbo].[ServiceCategory] ([ServiceId], [Code]);

CREATE NONCLUSTERED INDEX [IX_Service_ActiveSLAVersionId] ON [dbo].[Service] ([ActiveSLAVersionId]);
CREATE NONCLUSTERED INDEX [IX_Service_ActiveWorkflowVersionId] ON [dbo].[Service] ([ActiveWorkflowVersionId]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_Service_TenantId_Code] ON [dbo].[Service] ([TenantId], [Code]);
CREATE NONCLUSTERED INDEX [IX_Service_TenantId_Status_Name] ON [dbo].[Service] ([TenantId], [Status], [Name]);

CREATE NONCLUSTERED INDEX [IX_TaskAssignment_AssignedBy] ON [dbo].[TaskAssignment] ([AssignedBy]);
CREATE NONCLUSTERED INDEX [IX_TaskAssignment_DepartmentId] ON [dbo].[TaskAssignment] ([DepartmentId]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_TaskAssignment_TaskId] ON [dbo].[TaskAssignment] ([TaskId]) WHERE ([EndedAt] IS NULL);
CREATE NONCLUSTERED INDEX [IX_TaskAssignment_TaskId_AssignedAt] ON [dbo].[TaskAssignment] ([TaskId], [AssignedAt]);
CREATE NONCLUSTERED INDEX [IX_TaskAssignment_UserId] ON [dbo].[TaskAssignment] ([UserId]);

CREATE NONCLUSTERED INDEX [IX_TaskChecklistItem_CompletedBy] ON [dbo].[TaskChecklistItem] ([CompletedBy]);
CREATE NONCLUSTERED INDEX [IX_TaskChecklistItem_TaskId_SortOrder] ON [dbo].[TaskChecklistItem] ([TaskId], [SortOrder]);

CREATE NONCLUSTERED INDEX [IX_TaskProgressReport_AuthorId] ON [dbo].[TaskProgressReport] ([AuthorId]);
CREATE NONCLUSTERED INDEX [IX_TaskProgressReport_TaskId_SubmittedAt] ON [dbo].[TaskProgressReport] ([TaskId], [SubmittedAt]);

CREATE NONCLUSTERED INDEX [IX_TaskResult_AuthorId] ON [dbo].[TaskResult] ([AuthorId]);
CREATE NONCLUSTERED INDEX [IX_TaskResult_TaskId_RevisionNo] ON [dbo].[TaskResult] ([TaskId], [RevisionNo]);

CREATE NONCLUSTERED INDEX [IX_Task_CreatorId] ON [dbo].[Task] ([CreatorId]);
CREATE NONCLUSTERED INDEX [IX_Task_ParentTaskId] ON [dbo].[Task] ([ParentTaskId]);
CREATE NONCLUSTERED INDEX [IX_Task_RequestId] ON [dbo].[Task] ([RequestId]);
CREATE NONCLUSTERED INDEX [IX_Task_SLAVersionId] ON [dbo].[Task] ([SLAVersionId]);
CREATE NONCLUSTERED INDEX [IX_Task_TenantId_CreatorId_CreatedAt] ON [dbo].[Task] ([TenantId], [CreatorId], [CreatedAt]);
CREATE NONCLUSTERED INDEX [IX_Task_TenantId_Status_Deadline] ON [dbo].[Task] ([TenantId], [Status], [Deadline]);
CREATE NONCLUSTERED INDEX [IX_Task_Tenant_DeletedAt] ON [dbo].[Task] ([TenantId], [DeletedAt]);
CREATE NONCLUSTERED INDEX [IX_Task_WorkflowVersionId] ON [dbo].[Task] ([WorkflowVersionId]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_TenantSetting_TenantId_Key] ON [dbo].[TenantSetting] ([TenantId], [Key]);
CREATE NONCLUSTERED INDEX [IX_TenantSetting_UpdatedBy] ON [dbo].[TenantSetting] ([UpdatedBy]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_Tenant_CompanyId] ON [dbo].[Tenant] ([CompanyId]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_Tenant_TenantKey] ON [dbo].[Tenant] ([TenantKey]);

CREATE NONCLUSTERED INDEX [IX_UserRole_RoleId] ON [dbo].[UserRole] ([RoleId]);

CREATE NONCLUSTERED INDEX [IX_User_TenantId_DepartmentId] ON [dbo].[User] ([TenantId], [DepartmentId]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_User_TenantId_NormalizedEmail] ON [dbo].[User] ([TenantId], [NormalizedEmail]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_User_TenantId_NormalizedEmployeeCode] ON [dbo].[User] ([TenantId], [NormalizedEmployeeCode]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_WebPushSubscription_TenantId_UserId_Endpoint] ON [dbo].[WebPushSubscription] ([TenantId], [UserId], [Endpoint]);

CREATE NONCLUSTERED INDEX [IX_WorkItemLabel_CreatedBy] ON [dbo].[WorkItemLabel] ([CreatedBy]);
CREATE NONCLUSTERED INDEX [IX_WorkItemLabel_LabelId] ON [dbo].[WorkItemLabel] ([LabelId]);
CREATE NONCLUSTERED INDEX [IX_WorkItemLabel_TenantId_LabelId_RemovedAt] ON [dbo].[WorkItemLabel] ([TenantId], [LabelId], [RemovedAt]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_WorkItemLabel_TenantId_ObjectType_ObjectId_LabelId] ON [dbo].[WorkItemLabel] ([TenantId], [ObjectType], [ObjectId], [LabelId]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_WorkItemWatcher_TenantId_ObjectType_ObjectId_UserId] ON [dbo].[WorkItemWatcher] ([TenantId], [ObjectType], [ObjectId], [UserId]);
CREATE NONCLUSTERED INDEX [IX_WorkItemWatcher_TenantId_UserId] ON [dbo].[WorkItemWatcher] ([TenantId], [UserId]);
CREATE NONCLUSTERED INDEX [IX_WorkItemWatcher_UserId] ON [dbo].[WorkItemWatcher] ([UserId]);

CREATE UNIQUE NONCLUSTERED INDEX [IX_WorkflowStep_WorkflowVersionId_OrderNo] ON [dbo].[WorkflowStep] ([WorkflowVersionId], [OrderNo]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_WorkflowStep_WorkflowVersionId_StepCode] ON [dbo].[WorkflowStep] ([WorkflowVersionId], [StepCode]);
CREATE NONCLUSTERED INDEX [IX_WorkflowTransition_WorkflowVersionId] ON [dbo].[WorkflowTransition] ([WorkflowVersionId]);
CREATE UNIQUE NONCLUSTERED INDEX [IX_WorkflowVersion_WorkflowId_VersionNo] ON [dbo].[WorkflowVersion] ([WorkflowId], [VersionNo]);
CREATE NONCLUSTERED INDEX [IX_Workflow_TenantId] ON [dbo].[Workflow] ([TenantId]);

-- AuditLog action lookup index (replaces PostgreSQL JSONB expression indexes on MetadataJson)
CREATE NONCLUSTERED INDEX [IX_AuditLog_TenantId_ActorId_Action] ON [dbo].[AuditLog] ([TenantId], [ActorId], [Action]);
GO

-- =============================================================================
-- 5. FOREIGN KEY CONSTRAINTS
-- =============================================================================

-- Hangfire FKs
ALTER TABLE [hangfire].[jobparameter]
    ADD CONSTRAINT [jobparameter_jobid_fkey] FOREIGN KEY ([jobid]) REFERENCES [hangfire].[job] ([id]) ON UPDATE CASCADE ON DELETE CASCADE;

ALTER TABLE [hangfire].[state]
    ADD CONSTRAINT [state_jobid_fkey] FOREIGN KEY ([jobid]) REFERENCES [hangfire].[job] ([id]) ON UPDATE CASCADE ON DELETE CASCADE;
GO

-- Application FKs
ALTER TABLE [dbo].[AIAgentAction]
    ADD CONSTRAINT [FK_AIAgentAction_AIInteraction_AIInteractionId] FOREIGN KEY ([AIInteractionId]) REFERENCES [dbo].[AIInteraction] ([AIInteractionId]) ON DELETE CASCADE;

ALTER TABLE [dbo].[AIAgentAction]
    ADD CONSTRAINT [FK_AIAgentAction_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[AIInteraction]
    ADD CONSTRAINT [FK_AIInteraction_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[AIInteraction]
    ADD CONSTRAINT [FK_AIInteraction_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[AIRecommendation]
    ADD CONSTRAINT [FK_AIRecommendation_AIInteraction_AIInteractionId] FOREIGN KEY ([AIInteractionId]) REFERENCES [dbo].[AIInteraction] ([AIInteractionId]) ON DELETE CASCADE;

ALTER TABLE [dbo].[AIRecommendation]
    ADD CONSTRAINT [FK_AIRecommendation_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[AIRecommendation]
    ADD CONSTRAINT [FK_AIRecommendation_User_DecisionBy] FOREIGN KEY ([DecisionBy]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[AIVectorMemory]
    ADD CONSTRAINT [FK_AIVectorMemory_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE CASCADE;

ALTER TABLE [dbo].[AiLearnedChoice]
    ADD CONSTRAINT [FK_AiLearnedChoice_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE CASCADE;

ALTER TABLE [dbo].[AiLearnedChoice]
    ADD CONSTRAINT [FK_AiLearnedChoice_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE CASCADE;

ALTER TABLE [dbo].[Attachment]
    ADD CONSTRAINT [FK_Attachment_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Attachment]
    ADD CONSTRAINT [FK_Attachment_User_UploadedBy] FOREIGN KEY ([UploadedBy]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[AuditLog]
    ADD CONSTRAINT [FK_AuditLog_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[AuthenticationSession]
    ADD CONSTRAINT [FK_AuthenticationSession_AuthenticationSession_ReplacedById] FOREIGN KEY ([ReplacedById]) REFERENCES [dbo].[AuthenticationSession] ([AuthenticationSessionId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[AuthenticationSession]
    ADD CONSTRAINT [FK_AuthenticationSession_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[BusinessCalendar]
    ADD CONSTRAINT [FK_BusinessCalendar_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[CommentMention]
    ADD CONSTRAINT [FK_CommentMention_Comment_CommentId] FOREIGN KEY ([CommentId]) REFERENCES [dbo].[Comment] ([CommentId]) ON DELETE CASCADE;

ALTER TABLE [dbo].[CommentMention]
    ADD CONSTRAINT [FK_CommentMention_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[CommentMention]
    ADD CONSTRAINT [FK_CommentMention_User_MentionedUserId] FOREIGN KEY ([MentionedUserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Comment]
    ADD CONSTRAINT [FK_Comment_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Comment]
    ADD CONSTRAINT [FK_Comment_User_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Confirmation]
    ADD CONSTRAINT [FK_Confirmation_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Confirmation]
    ADD CONSTRAINT [FK_Confirmation_User_ActorId] FOREIGN KEY ([ActorId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[DeadlineReminderSchedule]
    ADD CONSTRAINT [FK_DeadlineReminderSchedule_Task_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [dbo].[Task] ([TaskId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[DeadlineReminderSchedule]
    ADD CONSTRAINT [FK_DeadlineReminderSchedule_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[DeadlineReminderSchedule]
    ADD CONSTRAINT [FK_DeadlineReminderSchedule_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Department]
    ADD CONSTRAINT [FK_Department_Department_TenantId_ParentDepartmentId] FOREIGN KEY ([TenantId], [ParentDepartmentId]) REFERENCES [dbo].[Department] ([TenantId], [DepartmentId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Department]
    ADD CONSTRAINT [FK_Department_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Label]
    ADD CONSTRAINT [FK_Label_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Label]
    ADD CONSTRAINT [FK_Label_User_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[ManagementScope]
    ADD CONSTRAINT [FK_ManagementScope_Department_TenantId_DepartmentId] FOREIGN KEY ([TenantId], [DepartmentId]) REFERENCES [dbo].[Department] ([TenantId], [DepartmentId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[ManagementScope]
    ADD CONSTRAINT [FK_ManagementScope_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[ManagementScope]
    ADD CONSTRAINT [FK_ManagementScope_User_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[ManagementScope]
    ADD CONSTRAINT [FK_ManagementScope_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[NotificationLog]
    ADD CONSTRAINT [FK_NotificationLog_Task_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [dbo].[Task] ([TaskId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[NotificationLog]
    ADD CONSTRAINT [FK_NotificationLog_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[NotificationLog]
    ADD CONSTRAINT [FK_NotificationLog_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Notification]
    ADD CONSTRAINT [FK_Notification_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Notification]
    ADD CONSTRAINT [FK_Notification_User_RecipientId] FOREIGN KEY ([RecipientId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RequestEscalation]
    ADD CONSTRAINT [FK_RequestEscalation_Request_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [dbo].[Request] ([RequestId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RequestEscalation]
    ADD CONSTRAINT [FK_RequestEscalation_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RequestEscalation]
    ADD CONSTRAINT [FK_RequestEscalation_User_DecidedById] FOREIGN KEY ([DecidedById]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RequestEscalation]
    ADD CONSTRAINT [FK_RequestEscalation_User_EscalatedById] FOREIGN KEY ([EscalatedById]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RequestResolution]
    ADD CONSTRAINT [FK_RequestResolution_Request_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [dbo].[Request] ([RequestId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RequestResolution]
    ADD CONSTRAINT [FK_RequestResolution_User_ResolverId] FOREIGN KEY ([ResolverId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RequestRouting]
    ADD CONSTRAINT [FK_RequestRouting_Department_FromDepartmentId] FOREIGN KEY ([FromDepartmentId]) REFERENCES [dbo].[Department] ([DepartmentId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RequestRouting]
    ADD CONSTRAINT [FK_RequestRouting_Department_ToDepartmentId] FOREIGN KEY ([ToDepartmentId]) REFERENCES [dbo].[Department] ([DepartmentId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RequestRouting]
    ADD CONSTRAINT [FK_RequestRouting_Request_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [dbo].[Request] ([RequestId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RequestRouting]
    ADD CONSTRAINT [FK_RequestRouting_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RequestRouting]
    ADD CONSTRAINT [FK_RequestRouting_User_FromUserId] FOREIGN KEY ([FromUserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RequestRouting]
    ADD CONSTRAINT [FK_RequestRouting_User_RoutedBy] FOREIGN KEY ([RoutedBy]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RequestRouting]
    ADD CONSTRAINT [FK_RequestRouting_User_ToUserId] FOREIGN KEY ([ToUserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Request]
    ADD CONSTRAINT [FK_Request_Request_ParentRequestId] FOREIGN KEY ([ParentRequestId]) REFERENCES [dbo].[Request] ([RequestId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Request]
    ADD CONSTRAINT [FK_Request_Request_RevisedFromRequestId] FOREIGN KEY ([RevisedFromRequestId]) REFERENCES [dbo].[Request] ([RequestId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Request]
    ADD CONSTRAINT [FK_Request_SLAVersion_SLAVersionId] FOREIGN KEY ([SLAVersionId]) REFERENCES [dbo].[SLAVersion] ([SLAVersionId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Request]
    ADD CONSTRAINT [FK_Request_ServiceCategory_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[ServiceCategory] ([ServiceCategoryId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Request]
    ADD CONSTRAINT [FK_Request_Service_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [dbo].[Service] ([ServiceId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Request]
    ADD CONSTRAINT [FK_Request_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Request]
    ADD CONSTRAINT [FK_Request_User_RequesterId] FOREIGN KEY ([RequesterId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Request]
    ADD CONSTRAINT [FK_Request_WorkflowVersion_WorkflowVersionId] FOREIGN KEY ([WorkflowVersionId]) REFERENCES [dbo].[WorkflowVersion] ([WorkflowVersionId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RolePermission]
    ADD CONSTRAINT [FK_RolePermission_Permission_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [dbo].[Permission] ([PermissionId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[RolePermission]
    ADD CONSTRAINT [FK_RolePermission_Role_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Role] ([RoleId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Role]
    ADD CONSTRAINT [FK_Role_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[SLAProfile]
    ADD CONSTRAINT [FK_SLAProfile_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[SLAVersion]
    ADD CONSTRAINT [FK_SLAVersion_BusinessCalendar_CalendarId] FOREIGN KEY ([CalendarId]) REFERENCES [dbo].[BusinessCalendar] ([CalendarId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[SLAVersion]
    ADD CONSTRAINT [FK_SLAVersion_SLAProfile_SLAProfileId] FOREIGN KEY ([SLAProfileId]) REFERENCES [dbo].[SLAProfile] ([SLAProfileId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[ServiceCategory]
    ADD CONSTRAINT [FK_ServiceCategory_Service_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [dbo].[Service] ([ServiceId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Service]
    ADD CONSTRAINT [FK_Service_SLAVersion_ActiveSLAVersionId] FOREIGN KEY ([ActiveSLAVersionId]) REFERENCES [dbo].[SLAVersion] ([SLAVersionId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Service]
    ADD CONSTRAINT [FK_Service_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Service]
    ADD CONSTRAINT [FK_Service_WorkflowVersion_ActiveWorkflowVersionId] FOREIGN KEY ([ActiveWorkflowVersionId]) REFERENCES [dbo].[WorkflowVersion] ([WorkflowVersionId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[TaskAssignment]
    ADD CONSTRAINT [FK_TaskAssignment_Department_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [dbo].[Department] ([DepartmentId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[TaskAssignment]
    ADD CONSTRAINT [FK_TaskAssignment_Task_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [dbo].[Task] ([TaskId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[TaskAssignment]
    ADD CONSTRAINT [FK_TaskAssignment_User_AssignedBy] FOREIGN KEY ([AssignedBy]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[TaskAssignment]
    ADD CONSTRAINT [FK_TaskAssignment_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[TaskChecklistItem]
    ADD CONSTRAINT [FK_TaskChecklistItem_Task_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [dbo].[Task] ([TaskId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[TaskChecklistItem]
    ADD CONSTRAINT [FK_TaskChecklistItem_User_CompletedBy] FOREIGN KEY ([CompletedBy]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[TaskProgressReport]
    ADD CONSTRAINT [FK_TaskProgressReport_Task_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [dbo].[Task] ([TaskId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[TaskProgressReport]
    ADD CONSTRAINT [FK_TaskProgressReport_User_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[TaskResult]
    ADD CONSTRAINT [FK_TaskResult_Task_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [dbo].[Task] ([TaskId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[TaskResult]
    ADD CONSTRAINT [FK_TaskResult_User_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Task]
    ADD CONSTRAINT [FK_Task_Request_RequestId] FOREIGN KEY ([RequestId]) REFERENCES [dbo].[Request] ([RequestId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Task]
    ADD CONSTRAINT [FK_Task_SLAVersion_SLAVersionId] FOREIGN KEY ([SLAVersionId]) REFERENCES [dbo].[SLAVersion] ([SLAVersionId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Task]
    ADD CONSTRAINT [FK_Task_Task_ParentTaskId] FOREIGN KEY ([ParentTaskId]) REFERENCES [dbo].[Task] ([TaskId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Task]
    ADD CONSTRAINT [FK_Task_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Task]
    ADD CONSTRAINT [FK_Task_User_CreatorId] FOREIGN KEY ([CreatorId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Task]
    ADD CONSTRAINT [FK_Task_WorkflowVersion_WorkflowVersionId] FOREIGN KEY ([WorkflowVersionId]) REFERENCES [dbo].[WorkflowVersion] ([WorkflowVersionId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[TenantSetting]
    ADD CONSTRAINT [FK_TenantSetting_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[TenantSetting]
    ADD CONSTRAINT [FK_TenantSetting_User_UpdatedBy] FOREIGN KEY ([UpdatedBy]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Tenant]
    ADD CONSTRAINT [FK_Tenant_Company_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [dbo].[Company] ([CompanyId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[UserRole]
    ADD CONSTRAINT [FK_UserRole_Role_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[Role] ([RoleId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[UserRole]
    ADD CONSTRAINT [FK_UserRole_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[User]
    ADD CONSTRAINT [FK_User_Department_TenantId_DepartmentId] FOREIGN KEY ([TenantId], [DepartmentId]) REFERENCES [dbo].[Department] ([TenantId], [DepartmentId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[User]
    ADD CONSTRAINT [FK_User_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[WebPushSubscription]
    ADD CONSTRAINT [FK_WebPushSubscription_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[WebPushSubscription]
    ADD CONSTRAINT [FK_WebPushSubscription_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[WorkItemLabel]
    ADD CONSTRAINT [FK_WorkItemLabel_Label_LabelId] FOREIGN KEY ([LabelId]) REFERENCES [dbo].[Label] ([LabelId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[WorkItemLabel]
    ADD CONSTRAINT [FK_WorkItemLabel_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[WorkItemLabel]
    ADD CONSTRAINT [FK_WorkItemLabel_User_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[WorkItemWatcher]
    ADD CONSTRAINT [FK_WorkItemWatcher_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[WorkItemWatcher]
    ADD CONSTRAINT [FK_WorkItemWatcher_User_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([UserId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[WorkflowStep]
    ADD CONSTRAINT [FK_WorkflowStep_WorkflowVersion_WorkflowVersionId] FOREIGN KEY ([WorkflowVersionId]) REFERENCES [dbo].[WorkflowVersion] ([WorkflowVersionId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[WorkflowTransition]
    ADD CONSTRAINT [FK_WorkflowTransition_WorkflowVersion_WorkflowVersionId] FOREIGN KEY ([WorkflowVersionId]) REFERENCES [dbo].[WorkflowVersion] ([WorkflowVersionId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[WorkflowVersion]
    ADD CONSTRAINT [FK_WorkflowVersion_Workflow_WorkflowId] FOREIGN KEY ([WorkflowId]) REFERENCES [dbo].[Workflow] ([WorkflowId]) ON DELETE NO ACTION;

ALTER TABLE [dbo].[Workflow]
    ADD CONSTRAINT [FK_Workflow_Tenant_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenant] ([TenantId]) ON DELETE NO ACTION;
GO
