namespace Api.Domain;

/// <summary>Kind of entity: Master data (lookup) or Transaction (recordable movements).</summary>
public enum EntityKind
{
    Master = 0,
    Transaction = 1
}

/// <summary>Datatype of a dynamic field. Drives validation + UI widget on the frontend.</summary>
public enum FieldType
{
    Text = 0,
    TextArea = 1,
    Number = 2,
    Decimal = 3,
    Date = 4,
    Boolean = 5,
    /// <summary>Reference to another dynamic entity (master data lookup).</summary>
    Lookup = 6,
    Email = 7
}

/// <summary>
/// A user-defined entity. Rows live in the dynamic Record/RecordValue tables,
/// so new master data or transaction types are created at runtime without code changes.
/// </summary>
public class DynamicEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public EntityKind Kind { get; set; } = EntityKind.Master;
    /// <summary>Field acting as the human-readable label in the UI.</summary>
    public string? DisplayField { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<DynamicField> Fields { get; set; } = new List<DynamicField>();
    public ICollection<Record> Records { get; set; } = new List<Record>();
}

public class DynamicField
{
    public int Id { get; set; }
    public int EntityId { get; set; }
    public DynamicEntity Entity { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public FieldType Type { get; set; } = FieldType.Text;
    public bool IsRequired { get; set; }
    public bool IsUnique { get; set; }
    public bool IsSearchable { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public int SortOrder { get; set; }
    public int? MaxLength { get; set; }
    public string? DefaultValue { get; set; }
    public string? OptionsJson { get; set; }
    /// <summary>For Lookup fields: which entity this points at.</summary>
    public int? LookupEntityId { get; set; }
    /// <summary>For Lookup fields: which field of the target entity to show.</summary>
    public string? LookupDisplayField { get; set; }
}

/// <summary>One row of a dynamic entity.</summary>
public class Record
{
    public long Id { get; set; }
    public int EntityId { get; set; }
    public DynamicEntity Entity { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public ICollection<RecordValue> Values { get; set; } = new List<RecordValue>();
}

/// <summary>Single field value of a Record. One column per FieldType, only the relevant one is set.</summary>
public class RecordValue
{
    public long Id { get; set; }
    public long RecordId { get; set; }
    public Record Record { get; set; } = null!;
    public int FieldId { get; set; }
    public DynamicField Field { get; set; } = null!;

    public string? TextValue { get; set; }
    public decimal? NumberValue { get; set; }
    public DateTime? DateValue { get; set; }
    public bool? BoolValue { get; set; }
    public long? LookupValue { get; set; }
}
