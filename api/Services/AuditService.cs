using System.Text.Json;
using Api.Data;
using Api.Domain;

namespace Api.Services;

/// <summary>Canonical action codes. Free text elsewhere would fragment the filter options.</summary>
public static class AuditActions
{
    public const string Login = "login";
    public const string LoginFailed = "login_failed";
    public const string Logout = "logout";
    public const string RecordCreate = "record_create";
    public const string RecordUpdate = "record_update";
    public const string RecordDelete = "record_delete";
    public const string UserCreate = "user_create";
    public const string UserUpdate = "user_update";
    public const string UserDelete = "user_delete";
}

/// <summary>
/// Writes audit rows. Every mutating endpoint calls this AFTER the work succeeds, so the
/// trail contains events that really happened rather than attempts that were rolled back.
///
/// Deliberately NOT a middleware: a middleware sees every request including the ones the UI
/// makes speculatively (a search keystroke is a GET, an autosave retry is a POST that fails),
/// and it cannot tell whether a 400 was a validation error the user corrected or a rejection
/// worth recording. Recording at the call site is the only place that knows both.
/// </summary>
public class AuditService
{
    private readonly AppDbContext _db;

    public AuditService(AppDbContext db) => _db = db;

    public async Task LogAsync(
        string action,
        string target,
        string summary,
        string? username = null,
        int? userId = null,
        string? targetId = null,
        object? details = null,
        bool success = true,
        string? ip = null)
    {
        _db.AuditEntries.Add(new AuditEntry
        {
            CreatedAt = DateTime.UtcNow,
            Username = string.IsNullOrWhiteSpace(username) ? "anonymous" : username!,
            UserId = userId,
            Action = action,
            Target = target,
            TargetId = targetId,
            Summary = summary,
            DetailsJson = details is null ? null : JsonSerializer.Serialize(details),
            Success = success,
            IpAddress = ip
        });
        await _db.SaveChangesAsync();
    }
}