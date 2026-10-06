namespace Api.Contracts;

// ---- Auth ----
public record LoginRequest(string Username, string Password);
public record CreateUserRequest(string Username, string Password, string Email, string FullName, bool IsActive, List<int> RoleIds);
public record UpdateUserRequest(string? Email, string? FullName, bool? IsActive, string? Password, List<int>? RoleIds);
public record UserDto(int Id, string Username, string Email, string FullName, bool IsActive, DateTime CreatedAt, string CreatedBy, List<RoleDto> Roles);
public record RoleDto(int Id, string Name, string Description);
public record LoginResponse(string Token, DateTime ExpiresAt, UserDto User);

// ---- Dynamic entity metadata ----
public record FieldDto(
    int Id, string Name, string Label, string Type, bool IsRequired, bool IsUnique,
    bool IsSearchable, bool IsVisible, int SortOrder, int? MaxLength,
    string? DefaultValue, string? OptionsJson, int? LookupEntityId, string? LookupDisplayField);

public record EntityDto(
    int Id, string Name, string Slug, string Description, string Kind,
    string? DisplayField, bool IsSystem, bool IsActive, int SortOrder,
    int RecordCount, List<FieldDto> Fields);

public record CreateEntityRequest(string Name, string Slug, string Description, string Kind, string? DisplayField, int SortOrder, List<CreateFieldRequest> Fields);
public record UpdateEntityRequest(string? Name, string? Description, string? Kind, string? DisplayField, int? SortOrder, bool? IsActive);
public record CreateFieldRequest(string Name, string Label, string Type, bool IsRequired, bool IsUnique, bool IsSearchable, bool IsVisible, int? MaxLength, string? DefaultValue, string? OptionsJson, int? LookupEntityId, string? LookupDisplayField);
public record UpdateFieldRequest(string? Label, string? Type, bool? IsRequired, bool? IsUnique, bool? IsSearchable, bool? IsVisible, int? SortOrder, int? MaxLength, string? DefaultValue, string? OptionsJson, int? LookupEntityId, string? LookupDisplayField);

// ---- Records ----
public record RecordDto(
    long Id, int EntityId, string EntitySlug, DateTime CreatedAt, string CreatedBy,
    DateTime? UpdatedAt, string? UpdatedBy,
    Dictionary<string, object?> Values, string? Display);

public record RecordPage(int Page, int PageSize, int Total, List<RecordDto> Items);
public record SaveRecordRequest(Dictionary<string, object?> Values);

// ---- Audit ----
public record AuditDto(
    long Id, DateTime CreatedAt, string Username, int? UserId,
    string Action, string Target, string? TargetId, string Summary,
    bool Success, string? IpAddress);

public record AuditActor(string Username, int Count);

/// <summary>Aggregates over the FILTERED set, so the cards describe what is on screen.</summary>
public record AuditStats(int Total, int Logins, int Failed, int Deletes, List<AuditActor> Actors);

public record AuditPage(int Page, int PageSize, int Total, List<AuditDto> Items, AuditStats Stats);
