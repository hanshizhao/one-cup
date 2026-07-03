using OneCup.Domain.Enums;

namespace OneCup.Application.Dtos.System;

// ── 操作日志 ──

public class OperationLogDto
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string Username { get; init; } = "";
    public string Module { get; init; } = "";
    public string Action { get; init; } = "";
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    public string? TargetName { get; init; }
    public OperationResult Result { get; init; }
    public string HttpMethod { get; init; } = "";
    public string RequestPath { get; init; } = "";
    public int StatusCode { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? RequestPayload { get; init; }
    public string? ErrorMessage { get; init; }
    public string? StackTrace { get; init; }
    public int DurationMs { get; init; }
    public string? TraceId { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>列表项（不含 StackTrace/RequestPayload，减少传输）。</summary>
public class OperationLogListItemDto
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string Username { get; init; } = "";
    public string Module { get; init; } = "";
    public string Action { get; init; } = "";
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    public string? TargetName { get; init; }
    public OperationResult Result { get; init; }
    public int StatusCode { get; init; }
    public int DurationMs { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record OperationLogQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public Guid? UserId { get; init; }
    public string? Username { get; init; }
    public string? Module { get; init; }
    public string? Action { get; init; }
    public OperationResult? Result { get; init; }
    public string? Keyword { get; init; }
}

// ── 登录日志 ──

public class LoginLogDto
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string Username { get; init; } = "";
    public LoginEventType EventType { get; init; }
    public OperationResult Result { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? FailureReason { get; init; }
    public string? Message { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record LoginLogQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public Guid? UserId { get; init; }
    public string? Username { get; init; }
    public LoginEventType? EventType { get; init; }
    public OperationResult? Result { get; init; }
    public string? FailureReason { get; init; }
}
