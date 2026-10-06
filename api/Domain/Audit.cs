namespace Api.Domain;

/// <summary>
/// One recorded action by a user. Append-only: there is no update or delete path,
/// which is the whole point of an audit trail.
/// </summary>
public class AuditEntry
{
    public long Id { get; set; }

    /// <summary>UTC. Rendered in the client's local time.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Username of the actor, or the literal "anonymous" for a failed sign-in.</summary>
    public string Username { get; set; } = string.Empty;

    public int? UserId { get; set; }

    /// <summary>See <see cref="Api.Services.AuditActions"/>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>What was acted on, e.g. "CCTV Access Request Log" or "User Management".</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Primary identifier of the affected row, when there is one.</summary>
    public string? TargetId { get; set; }

    /// <summary>One human-readable line: "Deleted record #182 from CCTV Access Request Log".</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Extra structured context as JSON. Never holds secrets or field values.</summary>
    public string? DetailsJson { get; set; }

    /// <summary>False for rejected attempts (bad password, failed validation).</summary>
    public bool Success { get; set; } = true;

    public string? IpAddress { get; set; }
}