using Dapper;
using Npgsql;
using PROManagementSystem.Application.Agent.Queries.GetAgentDocuments;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanies;
using PROManagementSystem.Application.Clients.Queries.GetClientCompanyById;
using PROManagementSystem.Application.Clients.Queries.GetClientEntities;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeById;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeDocuments;
using PROManagementSystem.Application.Employees.Queries.GetEmployees;
using PROManagementSystem.Application.Staff.Queries.GetAllStaffWorkload;
using PROManagementSystem.Application.Staff.Queries.GetStaffWorkload;
using PROManagementSystem.Application.Tasks.Queries.GetAgentServiceFeeInvoice;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskById;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskHistory;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTasks;
using PROManagementSystem.Application.Tasks.Queries.GetRenewalTaskSteps;
using PROManagementSystem.Application.Documents.Queries.GetDocumentDependencies;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence.Sql;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class RenewalTaskReadRepository
    : IRenewalTaskReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public RenewalTaskReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    // Shared SELECT / GROUP BY projections for the task-detail queries
    // (admin GetByIdAsync and agent-scoped GetAgentTaskByIdAsync).
    private static readonly string TaskDetailSelect =
        $$"""
        rt.{{SqlColumnNames.RenewalTaskId}} AS Id,
        rt.{{SqlColumnNames.RenewalTaskStatus}} AS Status,
        rt.{{SqlColumnNames.RenewalTaskDocumentId}} AS DocumentId,
        d.id AS Document_Id,
        d.type AS Document_Type,
        d.document_number AS Document_DocumentNumber,
        d.issue_date AS Document_IssueDate,
        d.expiry_date AS Document_ExpiryDate,
        d.file_name AS Document_FileName,
        rt.{{SqlColumnNames.RenewalTaskClientCompanyId}} AS ClientCompanyId,
        u.id AS Staff_Id,
        u.full_name AS Staff_FullName,
        u.email AS Staff_Email,
        rt.{{SqlColumnNames.RenewalTaskBlockedReason}} AS BlockedReason,
        rt.{{SqlColumnNames.RenewalTaskBlockedSince}} AS BlockedSince,
        rt.{{SqlColumnNames.RenewalTaskCompletedAt}} AS CompletedAt,
        rt.{{SqlColumnNames.RenewalTaskServiceFeeAmount}} AS ServiceFeeAmount,
        COUNT(sl.{{SqlColumnNames.RenewalStepLogId}}) AS StepLogCount,
        rt.{{SqlColumnNames.RenewalTaskCreatedAt}} AS CreatedAt,
        rt.{{SqlColumnNames.RenewalTaskUpdatedAt}} AS UpdatedAt
        """;

    private static readonly string TaskDetailGroupBy =
        $$"""
        rt.{{SqlColumnNames.RenewalTaskId}},
        rt.{{SqlColumnNames.RenewalTaskStatus}},
        rt.{{SqlColumnNames.RenewalTaskDocumentId}},
        d.id,
        d.type,
        d.document_number,
        d.issue_date,
        d.expiry_date,
        d.file_name,
        rt.{{SqlColumnNames.RenewalTaskClientCompanyId}},
        u.id,
        u.full_name,
        u.email,
        rt.{{SqlColumnNames.RenewalTaskBlockedReason}},
        rt.{{SqlColumnNames.RenewalTaskBlockedSince}},
        rt.{{SqlColumnNames.RenewalTaskCompletedAt}},
        rt.{{SqlColumnNames.RenewalTaskServiceFeeAmount}},
        rt.{{SqlColumnNames.RenewalTaskCreatedAt}},
        rt.{{SqlColumnNames.RenewalTaskUpdatedAt}}
        """;

    // ============================================================
    // GET /admin/tasks
    // ============================================================

    public async Task<GetRenewalTasksResponseDto> GetTasksAsync(
        RenewalTaskStatus? status,
        Guid? assignedStaffId,
        Guid? clientCompanyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>
        {
            SqlColumnNames.RenewalTaskIsDeleted + " = false"
        };

        var parameters = new DynamicParameters();

        // Status filter
        if (status.HasValue)
        {
            whereConditions.Add(
                SqlColumnNames.RenewalTaskStatus + " = @Status");

            parameters.Add(
                "Status",
                status.Value);
        }

        // Assigned staff filter
        if (assignedStaffId.HasValue)
        {
            whereConditions.Add(
                SqlColumnNames.RenewalTaskAssignedStaffId +
                " = @AssignedStaffId");

            parameters.Add(
                "AssignedStaffId",
                assignedStaffId.Value);
        }

        // Client company filter
        if (clientCompanyId.HasValue)
        {
            whereConditions.Add(
                SqlColumnNames.RenewalTaskClientCompanyId +
                " = @ClientCompanyId");

            parameters.Add(
                "ClientCompanyId",
                clientCompanyId.Value);
        }

        var whereClause =
            "WHERE " +
            string.Join(
                " AND ",
                whereConditions);

        // Count
        var countSql = $"""
            SELECT COUNT(*)
            FROM renewal_tasks
            {whereClause};
            """;

        // Data
        var dataSql = $"""
            SELECT
                {SqlColumnNames.RenewalTaskId} AS Id,
                {SqlColumnNames.RenewalTaskDocumentId} AS DocumentId,
                {SqlColumnNames.RenewalTaskClientCompanyId} AS ClientCompanyId,
                {SqlColumnNames.RenewalTaskAssignedStaffId} AS AssignedStaffId,
                {SqlColumnNames.RenewalTaskStatus} AS Status,
                {SqlColumnNames.RenewalTaskBlockedReason} AS BlockedReason,
                {SqlColumnNames.RenewalTaskBlockedSince} AS BlockedSince,
                {SqlColumnNames.RenewalTaskCompletedAt} AS CompletedAt,
                {SqlColumnNames.RenewalTaskCreatedAt} AS CreatedAt
            FROM renewal_tasks
            {whereClause}
            ORDER BY {SqlColumnNames.RenewalTaskCreatedAt} DESC,
                     {SqlColumnNames.RenewalTaskId} ASC
            {SqlSnippets.Pagination};
            """;

        parameters.Add(
            "Offset",
            offset);

        parameters.Add(
            "PageSize",
            pageSize);

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var items =
            await connection.QueryAsync<
                RenewalTaskListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetRenewalTasksResponseDto(
            items.ToList(),
            totalCount);
    }


    // ============================================================
    // GET /admin/tasks/{id}
    // ============================================================

    public async Task<GetRenewalTaskByIdResponseDto?> GetByIdAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
    SELECT
        {TaskDetailSelect}

    FROM renewal_tasks rt

    INNER JOIN documents d
        ON d.id = rt.{SqlColumnNames.RenewalTaskDocumentId}

    LEFT JOIN users u
        ON u.id = rt.{SqlColumnNames.RenewalTaskAssignedStaffId}

    LEFT JOIN renewal_step_logs sl
        ON sl.{SqlColumnNames.RenewalStepLogRenewalTaskId} = rt.{SqlColumnNames.RenewalTaskId}

    WHERE rt.{SqlColumnNames.RenewalTaskId} = @TaskId
      AND rt.{SqlColumnNames.RenewalTaskIsDeleted} = false

    GROUP BY
        {TaskDetailGroupBy};
    """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var row =
            await connection.QuerySingleOrDefaultAsync<
                RenewalTaskDetailReadModel>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TaskId = taskId
                    },
                    cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        return new GetRenewalTaskByIdResponseDto(
            row.Id,
            row.Status,
            row.DocumentId,

            new DocumentInfoDto(
                row.Document_Id,
                row.Document_Type,
                row.Document_DocumentNumber,
                row.Document_IssueDate,
                row.Document_ExpiryDate,
                row.Document_FileName),

            row.ClientCompanyId,

            row.Staff_Id.HasValue
                ? new AssignedStaffInfoDto(
                    row.Staff_Id.Value,
                    row.Staff_FullName!,
                    row.Staff_Email!)
                : null,

            row.BlockedReason,
            row.BlockedSince,
            row.CompletedAt,
            row.ServiceFeeAmount,
            row.StepLogCount,
            row.CreatedAt,
            row.UpdatedAt);
    }


    public async Task<GetRenewalTaskStepsResponseDto?> GetStepsAsync(
    Guid taskId,
    CancellationToken cancellationToken)
    {
        var sql = $$"""
        SELECT COUNT(*)
        FROM renewal_tasks
        WHERE {{SqlColumnNames.RenewalTaskId}} = @TaskId
          AND {{SqlColumnNames.RenewalTaskIsDeleted}} = false;

        SELECT
            {{SqlColumnNames.RenewalStepLogId}} AS Id,
            {{SqlColumnNames.RenewalStepLogStepName}} AS StepName,
            {{SqlColumnNames.RenewalStepLogCompletedBy}} AS CompletedBy,
            {{SqlColumnNames.RenewalStepLogCompletedAt}} AS CompletedAt,
            {{SqlColumnNames.RenewalStepLogReferenceNumber}} AS ReferenceNumber,
            {{SqlColumnNames.RenewalStepLogProofFileUrl}} AS ProofFileUrl
        FROM renewal_step_logs
        WHERE {{SqlColumnNames.RenewalStepLogRenewalTaskId}} = @TaskId
          AND {{SqlColumnNames.RenewalStepLogIsDeleted}} = false
        ORDER BY {{SqlColumnNames.RenewalStepLogCompletedAt}} ASC;
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        using var multi =
            await connection.QueryMultipleAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TaskId = taskId
                    },
                    cancellationToken: cancellationToken));

        var taskExists =
            await multi.ReadSingleAsync<int>();

        if (taskExists == 0)
        {
            return null;
        }

        var stepItems =
            (await multi.ReadAsync<RenewalStepLogDto>())
            .ToList();

        return new GetRenewalTaskStepsResponseDto(
            taskId,
            stepItems,
            stepItems.Count);
    }

    public async Task<GetRenewalTaskHistoryResponseDto?> GetHistoryAsync(
    Guid taskId,
    CancellationToken cancellationToken)
    {
        var sql = $$"""
        SELECT COUNT(*)
        FROM renewal_tasks
        WHERE {{SqlColumnNames.RenewalTaskId}} = @TaskId
          AND {{SqlColumnNames.RenewalTaskIsDeleted}} = false;

        SELECT
            {{SqlColumnNames.RenewalTaskHistoryId}} AS Id,
            {{SqlColumnNames.RenewalTaskHistoryStatus}} AS Status,
            {{SqlColumnNames.RenewalTaskHistoryChangedBy}} AS ChangedBy,
            {{SqlColumnNames.RenewalTaskHistoryChangedAt}} AS ChangedAt,
            {{SqlColumnNames.RenewalTaskHistoryNote}} AS Note
        FROM renewal_task_histories
        WHERE {{SqlColumnNames.RenewalTaskHistoryRenewalTaskId}} = @TaskId
          AND {{SqlColumnNames.RenewalTaskHistoryIsDeleted}} = false
        ORDER BY {{SqlColumnNames.RenewalTaskHistoryChangedAt}} ASC;
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        using var multi =
            await connection.QueryMultipleAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TaskId = taskId
                    },
                    cancellationToken: cancellationToken));

        var taskExists =
            await multi.ReadSingleAsync<int>();

        if (taskExists == 0)
        {
            return null;
        }

        var historyItems =
            (await multi.ReadAsync<RenewalTaskHistoryDto>())
            .ToList();

        return new GetRenewalTaskHistoryResponseDto(
            taskId,
            historyItems,
            historyItems.Count);
    }


    // ============================================================
    // GET /client/tasks/{id} — company-scoped task detail
    // ============================================================

    public async Task<GetRenewalTaskByIdResponseDto?> GetByIdForCompanyAsync(
        Guid taskId,
        Guid clientCompanyId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
    SELECT
        {TaskDetailSelect}

    FROM renewal_tasks rt

    INNER JOIN documents d
        ON d.id = rt.{SqlColumnNames.RenewalTaskDocumentId}

    LEFT JOIN users u
        ON u.id = rt.{SqlColumnNames.RenewalTaskAssignedStaffId}

    LEFT JOIN renewal_step_logs sl
        ON sl.{SqlColumnNames.RenewalStepLogRenewalTaskId} = rt.{SqlColumnNames.RenewalTaskId}

    WHERE rt.{SqlColumnNames.RenewalTaskId} = @TaskId
      AND rt.{SqlColumnNames.RenewalTaskClientCompanyId} = @ClientCompanyId
      AND rt.{SqlColumnNames.RenewalTaskIsDeleted} = false

    GROUP BY
        {TaskDetailGroupBy};
    """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var row =
            await connection.QuerySingleOrDefaultAsync<
                RenewalTaskDetailReadModel>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TaskId = taskId,
                        ClientCompanyId = clientCompanyId
                    },
                    cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        return new GetRenewalTaskByIdResponseDto(
            row.Id,
            row.Status,
            row.DocumentId,

            new DocumentInfoDto(
                row.Document_Id,
                row.Document_Type,
                row.Document_DocumentNumber,
                row.Document_IssueDate,
                row.Document_ExpiryDate,
                row.Document_FileName),

            row.ClientCompanyId,

            row.Staff_Id.HasValue
                ? new AssignedStaffInfoDto(
                    row.Staff_Id.Value,
                    row.Staff_FullName!,
                    row.Staff_Email!)
                : null,

            row.BlockedReason,
            row.BlockedSince,
            row.CompletedAt,
            row.ServiceFeeAmount,
            row.StepLogCount,
            row.CreatedAt,
            row.UpdatedAt);
    }


    // ============================================================
    // GET /client/tasks/{id}/history — company-scoped history
    // ============================================================

    public async Task<GetRenewalTaskHistoryResponseDto?> GetHistoryForCompanyAsync(
        Guid taskId,
        Guid clientCompanyId,
        CancellationToken cancellationToken)
    {
        var sql = $$"""
        SELECT COUNT(*)
        FROM renewal_tasks
        WHERE {{SqlColumnNames.RenewalTaskId}} = @TaskId
          AND {{SqlColumnNames.RenewalTaskClientCompanyId}} = @ClientCompanyId
          AND {{SqlColumnNames.RenewalTaskIsDeleted}} = false;

        SELECT
            {{SqlColumnNames.RenewalTaskHistoryId}} AS Id,
            {{SqlColumnNames.RenewalTaskHistoryStatus}} AS Status,
            {{SqlColumnNames.RenewalTaskHistoryChangedBy}} AS ChangedBy,
            {{SqlColumnNames.RenewalTaskHistoryChangedAt}} AS ChangedAt,
            {{SqlColumnNames.RenewalTaskHistoryNote}} AS Note
        FROM renewal_task_histories
        WHERE {{SqlColumnNames.RenewalTaskHistoryRenewalTaskId}} = @TaskId
          AND {{SqlColumnNames.RenewalTaskHistoryIsDeleted}} = false
        ORDER BY {{SqlColumnNames.RenewalTaskHistoryChangedAt}} ASC;
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        using var multi =
            await connection.QueryMultipleAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TaskId = taskId,
                        ClientCompanyId = clientCompanyId
                    },
                    cancellationToken: cancellationToken));

        var taskExists =
            await multi.ReadSingleAsync<int>();

        if (taskExists == 0)
        {
            return null;
        }

        var historyItems =
            (await multi.ReadAsync<RenewalTaskHistoryDto>())
            .ToList();

        return new GetRenewalTaskHistoryResponseDto(
            taskId,
            historyItems,
            historyItems.Count);
    }


    public async Task<GetStaffWorkloadResponseDto?> GetStaffWorkloadAsync(
    Guid staffId,
    CancellationToken cancellationToken)
    {
        var sql = $$"""
        SELECT COUNT(*)
        FROM users
        WHERE id = @StaffId
          AND role = 'Agent'
          AND is_deleted = false;

        SELECT
            {{SqlColumnNames.RenewalTaskStatus}} AS Status,
            COUNT(*) AS TaskCount
        FROM renewal_tasks
        WHERE {{SqlColumnNames.RenewalTaskAssignedStaffId}} = @StaffId
          AND {{SqlColumnNames.RenewalTaskIsDeleted}} = false
        GROUP BY {{SqlColumnNames.RenewalTaskStatus}};
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        using var multi =
            await connection.QueryMultipleAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        StaffId = staffId
                    },
                    cancellationToken: cancellationToken));

        var staffExists =
            await multi.ReadSingleAsync<int>();

        if (staffExists == 0)
        {
            return null;
        }

        var rows =
            await multi.ReadAsync<StaffWorkloadStatusRow>();

        var counts = rows.ToDictionary(
            x => x.Status,
            x => x.TaskCount);

        var tasksByStatus =
            new Dictionary<string, int>
            {
                ["Submitted"] =
                    GetCount(counts, RenewalTaskStatus.Submitted),

                ["FeePaid"] =
                    GetCount(counts, RenewalTaskStatus.FeePaid),

                ["AwaitingApproval"] =
                    GetCount(
                        counts,
                        RenewalTaskStatus.AwaitingApproval),

                ["Blocked"] =
                    GetCount(counts, RenewalTaskStatus.Blocked),

                ["Approved"] =
                    GetCount(counts, RenewalTaskStatus.Approved),

                ["Updated"] =
                    GetCount(counts, RenewalTaskStatus.Updated)
            };

        var activeTaskCount =
            tasksByStatus["Submitted"]
            + tasksByStatus["FeePaid"]
            + tasksByStatus["AwaitingApproval"]
            + tasksByStatus["Approved"];

        var blockedTaskCount =
            tasksByStatus["Blocked"];

        return new GetStaffWorkloadResponseDto(
            staffId,
            activeTaskCount,
            blockedTaskCount,
            tasksByStatus);
    }

    private static int GetCount(
        IReadOnlyDictionary<RenewalTaskStatus, int> counts,
        RenewalTaskStatus status)
    {
        return counts.TryGetValue(
            status,
            out var count)
            ? count
            : 0;
    }

    public async Task<GetAllStaffWorkloadResponseDto> GetAllStaffWorkloadAsync(
    CancellationToken cancellationToken)
    {
        var sql = $$"""
        SELECT
            u.id AS StaffId,

            COALESCE(
                SUM(
                    CASE
                        WHEN rt.{{SqlColumnNames.RenewalTaskStatus}} IN (1, 2, 3, 5)
                        THEN 1
                        ELSE 0
                    END
                ),
                0
            ) AS ActiveTaskCount,

            COALESCE(
                SUM(
                    CASE
                        WHEN rt.{{SqlColumnNames.RenewalTaskStatus}} = 4
                        THEN 1
                        ELSE 0
                    END
                ),
                0
            ) AS BlockedTaskCount,

            COUNT(
                CASE WHEN rt.{{SqlColumnNames.RenewalTaskStatus}} = 1 THEN 1 END
            ) AS Submitted,

            COUNT(
                CASE WHEN rt.{{SqlColumnNames.RenewalTaskStatus}} = 2 THEN 1 END
            ) AS FeePaid,

            COUNT(
                CASE WHEN rt.{{SqlColumnNames.RenewalTaskStatus}} = 3 THEN 1 END
            ) AS AwaitingApproval,

            COUNT(
                CASE WHEN rt.{{SqlColumnNames.RenewalTaskStatus}} = 4 THEN 1 END
            ) AS Blocked,

            COUNT(
                CASE WHEN rt.{{SqlColumnNames.RenewalTaskStatus}} = 5 THEN 1 END
            ) AS Approved,

            COUNT(
                CASE WHEN rt.{{SqlColumnNames.RenewalTaskStatus}} = 6 THEN 1 END
            ) AS Updated

        FROM users u

        LEFT JOIN renewal_tasks rt
            ON rt.{{SqlColumnNames.RenewalTaskAssignedStaffId}} = u.id
            AND rt.{{SqlColumnNames.RenewalTaskIsDeleted}} = false

        WHERE u.role = 'Agent'
          AND u.is_deleted = false

        GROUP BY u.id

        ORDER BY u.id;
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var rows =
            await connection.QueryAsync<AllStaffWorkloadRow>(
                new CommandDefinition(
                    sql,
                    cancellationToken: cancellationToken));

        var items = rows.Select(row =>
            new GetStaffWorkloadResponseDto(
                row.StaffId,
                row.ActiveTaskCount,
                row.BlockedTaskCount,
                new Dictionary<string, int>
                {
                    ["Submitted"] = row.Submitted,
                    ["FeePaid"] = row.FeePaid,
                    ["AwaitingApproval"] = row.AwaitingApproval,
                    ["Blocked"] = row.Blocked,
                    ["Approved"] = row.Approved,
                    ["Updated"] = row.Updated
                }))
            .ToList();

        return new GetAllStaffWorkloadResponseDto(items);
    }


    // ============================================================
    // GET /agent/tasks/{id} — agent-scoped task detail
    // ============================================================

    public async Task<GetRenewalTaskByIdResponseDto?> GetAgentTaskByIdAsync(
        Guid taskId,
        Guid agentId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
    SELECT
        {TaskDetailSelect}

    FROM renewal_tasks rt

    INNER JOIN documents d
        ON d.id = rt.{SqlColumnNames.RenewalTaskDocumentId}

    LEFT JOIN users u
        ON u.id = rt.{SqlColumnNames.RenewalTaskAssignedStaffId}

    LEFT JOIN renewal_step_logs sl
        ON sl.{SqlColumnNames.RenewalStepLogRenewalTaskId} = rt.{SqlColumnNames.RenewalTaskId}

    WHERE rt.{SqlColumnNames.RenewalTaskId} = @TaskId
      AND rt.{SqlColumnNames.RenewalTaskAssignedStaffId} = @AgentId
      AND rt.{SqlColumnNames.RenewalTaskIsDeleted} = false

    GROUP BY
        {TaskDetailGroupBy};
    """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var row =
            await connection.QuerySingleOrDefaultAsync<
                RenewalTaskDetailReadModel>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TaskId = taskId,
                        AgentId = agentId
                    },
                    cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        return new GetRenewalTaskByIdResponseDto(
            row.Id,
            row.Status,
            row.DocumentId,

            new DocumentInfoDto(
                row.Document_Id,
                row.Document_Type,
                row.Document_DocumentNumber,
                row.Document_IssueDate,
                row.Document_ExpiryDate,
                row.Document_FileName),

            row.ClientCompanyId,

            row.Staff_Id.HasValue
                ? new AssignedStaffInfoDto(
                    row.Staff_Id.Value,
                    row.Staff_FullName!,
                    row.Staff_Email!)
                : null,

            row.BlockedReason,
            row.BlockedSince,
            row.CompletedAt,
            row.ServiceFeeAmount,
            row.StepLogCount,
            row.CreatedAt,
            row.UpdatedAt);
    }


    // ============================================================
    // GET /agent/tasks/{id}/steps — agent-scoped steps
    // ============================================================

    public async Task<GetRenewalTaskStepsResponseDto?> GetAgentStepsAsync(
        Guid taskId,
        Guid agentId,
        CancellationToken cancellationToken)
    {
        var sql = $$"""
        SELECT COUNT(*)
        FROM renewal_tasks
        WHERE {{SqlColumnNames.RenewalTaskId}} = @TaskId
          AND {{SqlColumnNames.RenewalTaskAssignedStaffId}} = @AgentId
          AND {{SqlColumnNames.RenewalTaskIsDeleted}} = false;

        SELECT
            {{SqlColumnNames.RenewalStepLogId}} AS Id,
            {{SqlColumnNames.RenewalStepLogStepName}} AS StepName,
            {{SqlColumnNames.RenewalStepLogCompletedBy}} AS CompletedBy,
            {{SqlColumnNames.RenewalStepLogCompletedAt}} AS CompletedAt,
            {{SqlColumnNames.RenewalStepLogReferenceNumber}} AS ReferenceNumber,
            {{SqlColumnNames.RenewalStepLogProofFileUrl}} AS ProofFileUrl
        FROM renewal_step_logs
        WHERE {{SqlColumnNames.RenewalStepLogRenewalTaskId}} = @TaskId
          AND {{SqlColumnNames.RenewalStepLogIsDeleted}} = false
        ORDER BY {{SqlColumnNames.RenewalStepLogCompletedAt}} ASC;
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        using var multi =
            await connection.QueryMultipleAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TaskId = taskId,
                        AgentId = agentId
                    },
                    cancellationToken: cancellationToken));

        var taskExists =
            await multi.ReadSingleAsync<int>();

        if (taskExists == 0)
        {
            return null;
        }

        var stepItems =
            (await multi.ReadAsync<RenewalStepLogDto>())
            .ToList();

        return new GetRenewalTaskStepsResponseDto(
            taskId,
            stepItems,
            stepItems.Count);
    }


    // ============================================================
    // GET /agent/tasks/{id}/history — agent-scoped history
    // ============================================================

    public async Task<GetRenewalTaskHistoryResponseDto?> GetAgentHistoryAsync(
        Guid taskId,
        Guid agentId,
        CancellationToken cancellationToken)
    {
        var sql = $$"""
        SELECT COUNT(*)
        FROM renewal_tasks
        WHERE {{SqlColumnNames.RenewalTaskId}} = @TaskId
          AND {{SqlColumnNames.RenewalTaskAssignedStaffId}} = @AgentId
          AND {{SqlColumnNames.RenewalTaskIsDeleted}} = false;

        SELECT
            {{SqlColumnNames.RenewalTaskHistoryId}} AS Id,
            {{SqlColumnNames.RenewalTaskHistoryStatus}} AS Status,
            {{SqlColumnNames.RenewalTaskHistoryChangedBy}} AS ChangedBy,
            {{SqlColumnNames.RenewalTaskHistoryChangedAt}} AS ChangedAt,
            {{SqlColumnNames.RenewalTaskHistoryNote}} AS Note
        FROM renewal_task_histories
        WHERE {{SqlColumnNames.RenewalTaskHistoryRenewalTaskId}} = @TaskId
          AND {{SqlColumnNames.RenewalTaskHistoryIsDeleted}} = false
        ORDER BY {{SqlColumnNames.RenewalTaskHistoryChangedAt}} ASC;
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        using var multi =
            await connection.QueryMultipleAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TaskId = taskId,
                        AgentId = agentId
                    },
                    cancellationToken: cancellationToken));

        var taskExists =
            await multi.ReadSingleAsync<int>();

        if (taskExists == 0)
        {
            return null;
        }

        var historyItems =
            (await multi.ReadAsync<RenewalTaskHistoryDto>())
            .ToList();

        return new GetRenewalTaskHistoryResponseDto(
            taskId,
            historyItems,
            historyItems.Count);
    }


    // ============================================================
    // GET /agent/tasks/{id}/service-fee-invoice — agent-scoped
    // ============================================================

    public async Task<GetAgentServiceFeeInvoiceResponseDto?> GetServiceFeeInvoiceByTaskIdAsync(
        Guid taskId,
        Guid agentId,
        CancellationToken cancellationToken)
    {
        var sql = $$"""
        SELECT
            si.{{SqlColumnNames.ServiceFeeInvoiceId}} AS Id,
            si.{{SqlColumnNames.ServiceFeeInvoiceInvoiceNumber}} AS InvoiceNumber,
            si.{{SqlColumnNames.ServiceFeeInvoiceInvoiceDate}} AS InvoiceDate,
            si.{{SqlColumnNames.ServiceFeeInvoiceDueDate}} AS DueDate,
            si.{{SqlColumnNames.ServiceFeeInvoiceAmount}} AS Amount,
            si.{{SqlColumnNames.ServiceFeeInvoiceCurrency}} AS Currency,
            si.{{SqlColumnNames.ServiceFeeInvoiceStatus}} AS Status,
            si.{{SqlColumnNames.ServiceFeeInvoiceDescription}} AS Description,
            si.{{SqlColumnNames.ServiceFeeInvoicePaidAt}} AS PaidAt,
            si.{{SqlColumnNames.ServiceFeeInvoiceCreatedAt}} AS CreatedAt
        FROM service_fee_invoices si
        INNER JOIN renewal_tasks rt
            ON rt.{{SqlColumnNames.RenewalTaskId}} = si.{{SqlColumnNames.ServiceFeeInvoiceRenewalTaskId}}
        WHERE si.{{SqlColumnNames.ServiceFeeInvoiceRenewalTaskId}} = @TaskId
          AND rt.{{SqlColumnNames.RenewalTaskAssignedStaffId}} = @AgentId
          AND si.{{SqlColumnNames.ServiceFeeInvoiceIsDeleted}} = false
          AND rt.{{SqlColumnNames.RenewalTaskIsDeleted}} = false;
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetAgentServiceFeeInvoiceResponseDto>(
            new CommandDefinition(
                sql,
                new
                {
                    TaskId = taskId,
                    AgentId = agentId
                },
                cancellationToken: cancellationToken));
    }


    // ============================================================
    // GET /agent/clients — agent-scoped client companies
    // ============================================================

    public async Task<GetClientCompaniesResponseDto> GetAgentClientCompaniesAsync(
        Guid agentId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>
        {
            SqlSnippets.AgentTaskScope,
            "cc.is_deleted = false"
        };

        var parameters = new DynamicParameters();
        parameters.Add("AgentId", agentId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereConditions.Add(
                "(cc.company_name ILIKE @Search " +
                "OR cc.trade_license_number ILIKE @Search)");
            parameters.Add("Search", $"%{search.Trim()}%");
        }

        var whereClause =
            "WHERE " + string.Join(" AND ", whereConditions);

        var countSql = $"""
            SELECT COUNT(DISTINCT cc.id)
            FROM client_companies cc
            INNER JOIN renewal_tasks rt
                ON rt.{SqlColumnNames.RenewalTaskClientCompanyId} = cc.id
            {whereClause};
            """;

        var dataSql = $"""
            SELECT DISTINCT
                cc.id AS Id,
                cc.company_name AS CompanyName,
                cc.trade_license_number AS TradeLicenseNumber,
                cc.phone AS Phone,
                cc.email AS Email,
                cc.emirate AS Emirate,
                cc.is_active AS IsActive,
                cc.is_deleted AS IsDeleted,
                cc.created_at AS CreatedAt
            FROM client_companies cc
            INNER JOIN renewal_tasks rt
                ON rt.{SqlColumnNames.RenewalTaskClientCompanyId} = cc.id
            {whereClause}
            ORDER BY cc.created_at DESC
            {SqlSnippets.Pagination};
            """;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var items =
            await connection.QueryAsync<ClientCompanyListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetClientCompaniesResponseDto(
            items.ToList(), page, pageSize, totalCount);
    }


    // ============================================================
    // GET /agent/clients/{id} — agent-scoped client company by ID
    // ============================================================

    public async Task<GetClientCompanyByIdResponseDto?> GetAgentClientCompanyByIdAsync(
        Guid agentId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
        SELECT
            cc.id AS Id,
            cc.company_name AS CompanyName,
            cc.trade_license_number AS TradeLicenseNumber,
            cc.phone AS Phone,
            cc.email AS Email,
            cc.address AS Address,
            cc.emirate AS Emirate,
            cc.is_active AS IsActive,
            cc.is_deleted AS IsDeleted,
            cc.created_at AS CreatedAt,
            cc.updated_at AS UpdatedAt
        FROM client_companies cc
        INNER JOIN renewal_tasks rt
            ON rt.{SqlColumnNames.RenewalTaskClientCompanyId} = cc.id
        WHERE cc.id = @CompanyId
          AND {SqlSnippets.AgentTaskScope}
          AND cc.is_deleted = false
        LIMIT 1;
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetClientCompanyByIdResponseDto>(
            new CommandDefinition(
                sql,
                new
                {
                    AgentId = agentId,
                    CompanyId = companyId
                },
                cancellationToken: cancellationToken));
    }


    // ============================================================
    // GET /agent/clients/{id}/entities — agent-scoped entities
    // ============================================================

    public async Task<GetClientEntitiesResponseDto> GetAgentClientEntitiesAsync(
        Guid agentId,
        Guid companyId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>
        {
            SqlSnippets.AgentTaskScope,
            $"rt.{SqlColumnNames.RenewalTaskClientCompanyId} = @CompanyId",
            "ce.is_deleted = false",
            "(d.client_entity_id = ce.id OR emp.client_entity_id = ce.id)"
        };

        var parameters = new DynamicParameters();
        parameters.Add("AgentId", agentId);
        parameters.Add("CompanyId", companyId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            whereConditions.Add(
                "(ce.entity_name ILIKE @Search " +
                "OR ce.trade_license_number ILIKE @Search)");
            parameters.Add("Search", $"%{search.Trim()}%");
        }

        var whereClause =
            "WHERE " + string.Join(" AND ", whereConditions);

        var countSql = $"""
            SELECT COUNT(DISTINCT ce.id)
            FROM client_entities ce
            INNER JOIN renewal_tasks rt
                ON rt.{SqlColumnNames.RenewalTaskClientCompanyId} = ce.client_company_id
            INNER JOIN documents d
                ON d.id = rt.{SqlColumnNames.RenewalTaskDocumentId}
            LEFT JOIN employees emp
                ON emp.id = d.employee_id
            {whereClause};
            """;

        var dataSql = $"""
            SELECT DISTINCT
                ce.id AS Id,
                ce.client_company_id AS ClientCompanyId,
                ce.entity_name AS EntityName,
                ce.trade_license_number AS TradeLicenseNumber,
                ce.emirate AS Emirate,
                ce.is_active AS IsActive,
                ce.is_deleted AS IsDeleted,
                ce.created_at AS CreatedAt
            FROM client_entities ce
            INNER JOIN renewal_tasks rt
                ON rt.{SqlColumnNames.RenewalTaskClientCompanyId} = ce.client_company_id
            INNER JOIN documents d
                ON d.id = rt.{SqlColumnNames.RenewalTaskDocumentId}
            LEFT JOIN employees emp
                ON emp.id = d.employee_id
            {whereClause}
            ORDER BY ce.created_at DESC
            {SqlSnippets.Pagination};
            """;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var items =
            await connection.QueryAsync<GetClientEntityListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetClientEntitiesResponseDto(
            items.ToList(), page, pageSize, totalCount);
    }


    // ============================================================
    // GET /agent/employees — agent-scoped employees
    // ============================================================

    public async Task<GetEmployeesResponseDto> GetAgentEmployeesAsync(
        Guid agentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var parameters = new DynamicParameters();
        parameters.Add("AgentId", agentId);

        var countSql = $$"""
            SELECT COUNT(DISTINCT e.id)
            FROM employees e
            INNER JOIN client_entities ce
                ON ce.id = e.client_entity_id
            INNER JOIN documents d
                ON d.employee_id = e.id
            INNER JOIN renewal_tasks rt
                ON rt.{{SqlColumnNames.RenewalTaskDocumentId}} = d.id
            WHERE {{SqlSnippets.AgentTaskScope}}
              AND e.is_deleted = false
              AND ce.is_deleted = false;
            """;

        var dataSql = $$"""
            SELECT DISTINCT
                e.id AS Id,
                e.client_entity_id AS ClientEntityId,
                ce.entity_name AS EntityName,
                e.full_name AS FullName,
                e.passport_number AS PassportNumber,
                e.nationality AS Nationality,
                e.job_title AS JobTitle,
                e.hire_date AS HireDate,
                e.is_active AS IsActive,
                e.is_deleted AS IsDeleted,
                e.created_at AS CreatedAt
            FROM employees e
            INNER JOIN client_entities ce
                ON ce.id = e.client_entity_id
            INNER JOIN documents d
                ON d.employee_id = e.id
            INNER JOIN renewal_tasks rt
                ON rt.{{SqlColumnNames.RenewalTaskDocumentId}} = d.id
            WHERE {{SqlSnippets.AgentTaskScope}}
              AND e.is_deleted = false
              AND ce.is_deleted = false
            ORDER BY e.created_at DESC
            {{SqlSnippets.Pagination}};
            """;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var items =
            await connection.QueryAsync<GetEmployeeListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetEmployeesResponseDto(
            items.ToList(), page, pageSize, totalCount);
    }


    // ============================================================
    // GET /agent/employees/{id} — agent-scoped employee by ID
    // ============================================================

    public async Task<GetEmployeeByIdResponseDto?> GetAgentEmployeeByIdAsync(
        Guid agentId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
        SELECT
            e.id AS Id,
            e.client_entity_id AS ClientEntityId,
            ce.client_company_id AS ClientCompanyId,
            ce.entity_name AS EntityName,
            e.full_name AS FullName,
            e.passport_number AS PassportNumber,
            e.date_of_birth AS DateOfBirth,
            e.nationality AS Nationality,
            e.job_title AS JobTitle,
            e.hire_date AS HireDate,
            e.is_active AS IsActive,
            e.is_deleted AS IsDeleted,
            e.created_at AS CreatedAt,
            e.updated_at AS UpdatedAt
        FROM employees e
        INNER JOIN client_entities ce
            ON ce.id = e.client_entity_id
        INNER JOIN documents d
            ON d.employee_id = e.id
        INNER JOIN renewal_tasks rt
            ON rt.{SqlColumnNames.RenewalTaskDocumentId} = d.id
        WHERE e.id = @EmployeeId
          AND {SqlSnippets.AgentTaskScope}
          AND e.is_deleted = false
        LIMIT 1;
        """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetEmployeeByIdResponseDto>(
            new CommandDefinition(
                sql,
                new
                {
                    AgentId = agentId,
                    EmployeeId = employeeId
                },
                cancellationToken: cancellationToken));
    }


    // ============================================================
    // GET /agent/employees/{id}/documents — agent-scoped docs
    // ============================================================

    public async Task<GetEmployeeDocumentsResponseDto> GetAgentEmployeeDocumentsAsync(
        Guid agentId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("AgentId", agentId);
        parameters.Add("EmployeeId", employeeId);

        var countSql = $$"""
            SELECT COUNT(DISTINCT d.id)
            FROM documents d
            INNER JOIN renewal_tasks rt
                ON rt.{{SqlColumnNames.RenewalTaskDocumentId}} = d.id
            WHERE d.employee_id = @EmployeeId
              AND {{SqlSnippets.AgentTaskScope}}
              AND d.is_deleted = false;
            """;

        var dataSql = $$"""
            SELECT DISTINCT
                d.id AS Id,
                d.employee_id AS EmployeeId,
                d.type AS Type,
                d.document_number AS DocumentNumber,
                d.issue_date AS IssueDate,
                d.expiry_date AS ExpiryDate,
                d.file_url AS FileUrl,
                d.file_name AS FileName,
                d.content_type AS ContentType,
                d.file_size AS FileSize,
                d.is_active AS IsActive,
                d.is_deleted AS IsDeleted,
                d.created_at AS CreatedAt,
                d.updated_at AS UpdatedAt,
                {{SqlSnippets.DocumentStatusNumericCase("d.expiry_date")}}
            FROM documents d
            INNER JOIN renewal_tasks rt
                ON rt.{{SqlColumnNames.RenewalTaskDocumentId}} = d.id
            WHERE d.employee_id = @EmployeeId
              AND {{SqlSnippets.AgentTaskScope}}
              AND d.is_deleted = false
            ORDER BY d.created_at DESC;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var items =
            await connection.QueryAsync<EmployeeDocumentListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetEmployeeDocumentsResponseDto(
            items.ToList(), totalCount);
    }


    // ============================================================
    // GET /agent/documents — agent-scoped document scope checks
    // ============================================================

    public async Task<bool> IsAgentDocumentInScopeAsync(
        Guid agentId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        // Strict task-linked scope. A document is in the agent's scope
        // only when it is the DocumentId of a non-deleted renewal task
        // assigned to the authenticated agent. No company-rooted access.
        var sql = $"""
            SELECT COUNT(*)
            FROM documents d
            WHERE d.id = @DocumentId
              AND d.is_deleted = false
              AND {SqlSnippets.DocumentScopeExists};
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new
                {
                    AgentId = agentId,
                    DocumentId = documentId
                },
                cancellationToken: cancellationToken));

        return count > 0;
    }


    public async Task<bool> IsAgentEmployeeInScopeAsync(
        Guid agentId,
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT COUNT(*)
            FROM employees e
            INNER JOIN documents d
                ON d.employee_id = e.id
            INNER JOIN renewal_tasks rt
                ON rt.{SqlColumnNames.RenewalTaskDocumentId} = d.id
            WHERE e.id = @EmployeeId
              AND {SqlSnippets.AgentTaskScope}
              AND e.is_deleted = false;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new
                {
                    AgentId = agentId,
                    EmployeeId = employeeId
                },
                cancellationToken: cancellationToken));

        return count > 0;
    }


    public async Task<bool> IsAgentEntityInScopeAsync(
        Guid agentId,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT COUNT(*)
            FROM client_entities ce
            INNER JOIN renewal_tasks rt
                ON rt.{SqlColumnNames.RenewalTaskClientCompanyId} = ce.client_company_id
            INNER JOIN documents d
                ON d.id = rt.{SqlColumnNames.RenewalTaskDocumentId}
            LEFT JOIN employees emp
                ON emp.id = d.employee_id
            WHERE ce.id = @EntityId
              AND {SqlSnippets.AgentTaskScope}
              AND ce.is_deleted = false
              AND (d.client_entity_id = ce.id OR emp.client_entity_id = ce.id);
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new
                {
                    AgentId = agentId,
                    EntityId = entityId
                },
                cancellationToken: cancellationToken));

        return count > 0;
    }


    public async Task<bool> IsAgentClientInScopeAsync(
        Guid agentId,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        // Strict task-linked scope. A client company is in the agent's scope
        // only when it is linked (via its entities/employees/documents) to a
        // non-deleted renewal task assigned to the authenticated agent.
        var sql = $"""
            SELECT COUNT(*)
            FROM client_companies cc
            INNER JOIN client_entities ce
                ON ce.client_company_id = cc.id
            INNER JOIN renewal_tasks rt
                ON rt.{SqlColumnNames.RenewalTaskClientCompanyId} = cc.id
            INNER JOIN documents d
                ON d.id = rt.{SqlColumnNames.RenewalTaskDocumentId}
            LEFT JOIN employees emp
                ON emp.id = d.employee_id
            WHERE cc.id = @CompanyId
              AND {SqlSnippets.AgentTaskScope}
              AND cc.is_deleted = false
              AND (d.client_entity_id = ce.id OR emp.client_entity_id = ce.id);
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new
                {
                    AgentId = agentId,
                    CompanyId = companyId
                },
                cancellationToken: cancellationToken));

        return count > 0;
    }


    public async Task<GetDocumentDependenciesResponseDto>
        GetAgentDocumentDependenciesAsync(
            Guid agentId,
            Guid documentId,
            CancellationToken cancellationToken)
    {
        // The scope predicate runs in SQL as an EXISTS subquery over the
        // documents (alias d) so no in-memory scoped-ID set is materialized.
        // Results are the non-deleted dependencies whose DependsOnDocumentId
        // is itself a non-deleted, task-linked, agent-scoped document.
        var sql = $"""
            SELECT
                dd.id AS Id,
                dd.depends_on_document_id AS DependsOnDocumentId,
                d.type AS DocumentType,
                d.document_number AS DocumentNumber,
                d.expiry_date AS ExpiryDate,
                d.is_active AS IsActive,
                d.is_deleted AS IsDeleted
            FROM document_dependencies dd
            INNER JOIN documents d
                ON d.id = dd.depends_on_document_id
            WHERE dd.document_id = @DocumentId
              AND dd.is_deleted = false
              AND d.is_deleted = false
              AND {SqlSnippets.DocumentScopeExists}
            ORDER BY dd.created_at DESC;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var items = await connection.QueryAsync<DocumentDependencyItemDto>(
            new CommandDefinition(
                sql,
                new
                {
                    AgentId = agentId,
                    DocumentId = documentId
                },
                cancellationToken: cancellationToken));

        var list = items.ToList();

        return new GetDocumentDependenciesResponseDto(
            documentId,
            list,
            list.Count);
    }


    // ============================================================
    // GET /agent/documents — agent-scoped document list
    // ============================================================

    public async Task<GetAgentDocumentsResponseDto> GetAgentDocumentsAsync(
        Guid agentId,
        DocumentType? type,
        DocumentStatus? status,
        DateTime? expiresBefore,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>
        {
            // Strict task-linked scope predicate. A document is in scope
            // only when it is the DocumentId of a non-deleted renewal task
            // assigned to the authenticated agent. No company-rooted access.
            SqlSnippets.DocumentScopeExists,
            "d.is_deleted = false"
        };

        var parameters = new DynamicParameters();
        parameters.Add("AgentId", agentId);

        if (type.HasValue)
        {
            whereConditions.Add("d.type = @Type");
            parameters.Add("Type", (int)type.Value);
        }

        if (status.HasValue)
        {
            switch (status.Value)
            {
                case DocumentStatus.Active:
                    // Wrapped as a single atom for the same reason as the client
                    // document scope: without the outer parentheses the "> 90
                    // days" branch would escape DocumentScopeExists and the
                    // soft-delete filter through SQL OR precedence.
                    whereConditions.Add(
                        "(d.expiry_date IS NULL OR " +
                        $"{SqlSnippets.BeyondWindowClause("d.expiry_date", 90)})");
                    break;

                case DocumentStatus.ExpiringSoon:
                    whereConditions.Add(
                        "d.expiry_date IS NOT NULL AND " +
                        "d.expiry_date > CURRENT_TIMESTAMP AND " +
                        SqlSnippets.ExpiryWindowClause("d.expiry_date", 90));
                    break;

                case DocumentStatus.Overdue:
                    whereConditions.Add(
                        "d.expiry_date IS NOT NULL AND " +
                        "d.expiry_date < CURRENT_TIMESTAMP");
                    break;

                case DocumentStatus.InRenewal:
                    whereConditions.Add(
                        "EXISTS (SELECT 1 FROM renewal_tasks rt3 " +
                        $"WHERE rt3.{SqlColumnNames.RenewalTaskDocumentId} = d.id " +
                        $"AND rt3.{SqlColumnNames.RenewalTaskStatus} IN (1, 2, 3, 5) " +
                        $"AND rt3.{SqlColumnNames.RenewalTaskIsDeleted} = false)");
                    break;
            }
        }

        if (expiresBefore.HasValue)
        {
            whereConditions.Add(
                "d.expiry_date <= @ExpiresBefore");
            parameters.Add("ExpiresBefore", expiresBefore.Value);
        }

        var whereClause =
            "WHERE " + string.Join(" AND ", whereConditions);

        var countSql = $"""
            SELECT COUNT(DISTINCT d.id)
            FROM documents d
            {whereClause};
            """;

        var dataSql = $"""
            SELECT DISTINCT
                d.id AS Id,
                d.client_entity_id AS ClientEntityId,
                d.employee_id AS EmployeeId,
                d.type AS Type,
                d.document_number AS DocumentNumber,
                d.issue_date AS IssueDate,
                d.expiry_date AS ExpiryDate,
                d.file_url AS FileUrl,
                d.file_name AS FileName,
                d.content_type AS ContentType,
                d.file_size AS FileSize,
                d.is_active AS IsActive,
                d.is_deleted AS IsDeleted,
                d.created_at AS CreatedAt,
                d.updated_at AS UpdatedAt,
                {SqlSnippets.DocumentStatusNumericCase("d.expiry_date")}
            FROM documents d
            {whereClause}
            ORDER BY d.created_at DESC
            {SqlSnippets.Pagination};
            """;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var items =
            await connection.QueryAsync<AgentDocumentListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetAgentDocumentsResponseDto(
            items.ToList(), page, pageSize, totalCount);
    }


    // ============================================================
    // GET /agent/documents/expiring — agent-scoped expiring docs
    // ============================================================

    public async Task<GetExpiringDocumentsResponseDto> GetAgentExpiringDocumentsAsync(
        Guid agentId,
        int days,
        int page,
        int pageSize,
        bool includeExpired,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>
        {
            // Strict task-linked scope predicate. A document is in scope
            // only when it is the DocumentId of a non-deleted renewal task
            // assigned to the authenticated agent. No company-rooted access.
            SqlSnippets.DocumentScopeExists,
            "d.is_deleted = false"
        };

        var parameters = new DynamicParameters();
        parameters.Add("AgentId", agentId);

        whereConditions.Add(
            SqlSnippets.ExpiryWindowCondition(
                "d.expiry_date",
                includeExpired));

        parameters.Add("Days", days);

        var whereClause =
            "WHERE " + string.Join(" AND ", whereConditions);

        var countSql = $"""
            SELECT COUNT(DISTINCT d.id)
            FROM documents d
            {whereClause};
            """;

        var dataSql = $"""
            SELECT DISTINCT
                d.id AS Id,
                d.client_entity_id AS ClientEntityId,
                d.employee_id AS EmployeeId,
                d.type AS Type,
                d.document_number AS DocumentNumber,
                d.issue_date AS IssueDate,
                d.expiry_date AS ExpiryDate,
                d.file_name AS FileName,
                d.is_active AS IsActive,
                d.is_deleted AS IsDeleted,
                {SqlSnippets.DaysRemaining("d.expiry_date")},
                {SqlSnippets.ExpiryStatusLabelCase("d.expiry_date", 90)}
            FROM documents d
            {whereClause}
            ORDER BY d.expiry_date ASC
            {SqlSnippets.Pagination};
            """;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        await using var connection =
            await _dataSource.OpenConnectionAsync(cancellationToken);

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var items =
            await connection.QueryAsync<ExpiringDocumentListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetExpiringDocumentsResponseDto(
            items.ToList(), page, pageSize, totalCount);
    }
}


// ================================================================
// Internal Dapper read model
// ================================================================

internal sealed class RenewalTaskDetailReadModel
{
    public Guid Id { get; set; }

    public RenewalTaskStatus Status { get; set; }

    public Guid DocumentId { get; set; }

    public Guid Document_Id { get; set; }

    public DocumentType Document_Type { get; set; }

    public string Document_DocumentNumber { get; set; }
        = string.Empty;

    public DateTime? Document_IssueDate { get; set; }

    public DateTime? Document_ExpiryDate { get; set; }

    public string? Document_FileName { get; set; }

    public Guid ClientCompanyId { get; set; }

    public Guid? Staff_Id { get; set; }

    public string? Staff_FullName { get; set; }

    public string? Staff_Email { get; set; }

    public string? BlockedReason { get; set; }

    public DateTime? BlockedSince { get; set; }

    public DateTime? CompletedAt { get; set; }

    public decimal? ServiceFeeAmount { get; set; }

    public int StepLogCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}


internal sealed class StaffWorkloadStatusRow
{
    public RenewalTaskStatus Status { get; set; }

    public int TaskCount { get; set; }
}

internal sealed class AllStaffWorkloadRow
{
    public Guid StaffId { get; set; }

    public int ActiveTaskCount { get; set; }

    public int BlockedTaskCount { get; set; }

    public int Submitted { get; set; }

    public int FeePaid { get; set; }

    public int AwaitingApproval { get; set; }

    public int Blocked { get; set; }

    public int Approved { get; set; }

    public int Updated { get; set; }
}