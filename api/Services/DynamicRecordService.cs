using System.Globalization;
using Api.Contracts;
using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class RecordValidationException : Exception
{
    public Dictionary<string, string> Errors { get; }
    public RecordValidationException(Dictionary<string, string> errors) : base("Validation failed")
        => Errors = errors;
}

public class NotFoundException : Exception
{
    public NotFoundException(string msg) : base(msg) { }
}

/// <summary>
/// Metadata-driven CRUD engine. All dynamic entities share the Record/RecordValue tables,
/// so a new master-data or transaction type is created at runtime with zero code changes.
/// </summary>
public class DynamicRecordService
{
    private readonly AppDbContext _db;
    public DynamicRecordService(AppDbContext db) => _db = db;

    // ---------- metadata ----------

    public async Task<DynamicEntity> GetEntityAsync(int id)
    {
        var e = await _db.Entities.Include(x => x.Fields).FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new NotFoundException($"Entity {id} not found");
        return e;
    }

    public async Task<DynamicEntity> GetEntityBySlugAsync(string slug)
    {
        var e = await _db.Entities.Include(x => x.Fields).FirstOrDefaultAsync(x => x.Slug == slug)
            ?? throw new NotFoundException($"Entity '{slug}' not found");
        return e;
    }

    public async Task<List<EntityDto>> ListEntitiesAsync(bool activeOnly = true)
    {
        var q = _db.Entities
            .Include(e => e.Fields)
            .AsQueryable();
        if (activeOnly) q = q.Where(e => e.IsActive);
        var list = await q.OrderBy(e => e.SortOrder).ThenBy(e => e.Name).ToListAsync();
        var counts = await _db.Records.Where(r => !r.IsDeleted)
            .GroupBy(r => r.EntityId)
            .Select(g => new { EntityId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.EntityId, x => x.Count);
        return list.Select(e => ToEntityDto(e, counts.GetValueOrDefault(e.Id))).ToList();
    }

    public static EntityDto ToEntityDto(DynamicEntity e, int recordCount) => new(
        e.Id, e.Name, e.Slug, e.Description ?? "", e.Kind.ToString(),
        e.DisplayField, e.IsSystem, e.IsActive, e.SortOrder, recordCount,
        e.Fields.OrderBy(f => f.SortOrder).Select(ToFieldDto).ToList());

    public static FieldDto ToFieldDto(DynamicField f) => new(
        f.Id, f.Name, f.Label, f.Type.ToString(), f.IsRequired, f.IsUnique,
        f.IsSearchable, f.IsVisible, f.SortOrder, f.MaxLength,
        f.DefaultValue, f.OptionsJson, f.LookupEntityId, f.LookupDisplayField);

    // ---------- records ----------

    public async Task<RecordPage> ListAsync(
        int entityId, int page, int pageSize,
        string? search = null, int? lookupFieldId = null, long? lookupValue = null,
        string? sortField = null, bool sortDesc = false)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 20 : Math.Clamp(pageSize, 1, 500);

        var entity = await GetEntityAsync(entityId);
        var total = await _db.Records.CountAsync(r => r.EntityId == entityId && !r.IsDeleted);

        var q = _db.Records
            .Include(r => r.Values)
            .Where(r => r.EntityId == entityId && !r.IsDeleted);

        if (lookupFieldId.HasValue && lookupValue.HasValue)
            q = q.Where(r => r.Values.Any(v => v.FieldId == lookupFieldId && v.LookupValue == lookupValue));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var searchable = entity.Fields.Where(f => f.IsSearchable).ToList();
            var textFieldIds = searchable.Where(f => f.Type is FieldType.Text or FieldType.TextArea or FieldType.Email)
                                            .Select(f => f.Id).ToList();

            // Matching target record ids for lookup fields, so search also covers resolved display labels.
            var lookupHits = new List<long>();
            foreach (var lf in searchable.Where(f => f.Type == FieldType.Lookup && f.LookupEntityId.HasValue))
            {
                var targetEntityId = lf.LookupEntityId!.Value;
                var targetDisplay = lf.LookupDisplayField;
                if (targetDisplay is null) continue;

                var target = await _db.Entities.AsNoTracking().FirstOrDefaultAsync(e => e.Id == targetEntityId);
                if (target is null) continue;
                var targetField = await _db.Fields.AsNoTracking().FirstOrDefaultAsync(f => f.EntityId == targetEntityId && f.Name == targetDisplay);
                if (targetField is null) continue;

                var ids = await _db.RecordValues
                    .Where(v => v.FieldId == targetField.Id && v.TextValue != null && v.TextValue.Contains(term))
                    .Select(v => v.RecordId)
                    .ToListAsync();
                if (ids.Count > 0) lookupHits.AddRange(ids);
            }

            q = q.Where(r =>
                (textFieldIds.Count > 0 && r.Values.Any(v => textFieldIds.Contains(v.FieldId) && v.TextValue != null && v.TextValue.Contains(term)))
                || r.Values.Any(v => v.LookupValue != null && lookupHits.Contains(v.LookupValue.Value))
                || r.CreatedBy.Contains(term));
        }

        // Sorting on a dynamic column is done in memory after materialising the page;
        // for correctness on large sets a dedicated projection should replace this.
        if (string.IsNullOrWhiteSpace(sortField))
            q = q.OrderByDescending(r => r.Id);
        else
            q = q.OrderBy(r => r.Id);

        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        if (!string.IsNullOrWhiteSpace(sortField))
            rows = SortRows(rows, entity, sortField, sortDesc);

        var items = await ToDtoListAsync(entity, rows);
        return new RecordPage(page, pageSize, total, items);
    }

    private static List<Record> SortRows(List<Record> rows, DynamicEntity entity, string sortField, bool desc)
    {
        var field = entity.Fields.FirstOrDefault(f => f.Name == sortField);
        if (field is null) return rows;
        Comparison<Record> cmp = (a, b) =>
        {
            var av = a.Values.FirstOrDefault(v => v.FieldId == field.Id);
            var bv = b.Values.FirstOrDefault(v => v.FieldId == field.Id);
            int c = field.Type switch
            {
                FieldType.Number or FieldType.Decimal =>
                    Nullable.Compare(av?.NumberValue, bv?.NumberValue),
                FieldType.Date => Nullable.Compare(av?.DateValue, bv?.DateValue),
                FieldType.Boolean => Nullable.Compare(av?.BoolValue, bv?.BoolValue),
                FieldType.Lookup => Nullable.Compare(av?.LookupValue, bv?.LookupValue),
                _ => string.Compare(av?.TextValue ?? "", bv?.TextValue ?? "", StringComparison.OrdinalIgnoreCase),
            };
            return c;
        };
        rows.Sort(cmp);
        if (desc) rows.Reverse();
        return rows;
    }

    public async Task<RecordDto> GetAsync(int entityId, long recordId)
    {
        var entity = await GetEntityAsync(entityId);
        var rec = await _db.Records.Include(r => r.Values)
            .FirstOrDefaultAsync(r => r.Id == recordId && r.EntityId == entityId && !r.IsDeleted)
            ?? throw new NotFoundException($"Record {recordId} not found");
        var list = await ToDtoListAsync(entity, new List<Record> { rec });
        return list[0];
    }

    public async Task<RecordDto> CreateAsync(int entityId, SaveRecordRequest req, string username)
    {
        var entity = await GetEntityAsync(entityId);
        var values = await MaterializeAsync(entity, req.Values);

        await ValidateAsync(entity, values, null);

        var rec = new Record
        {
            EntityId = entityId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = username
        };
        foreach (var v in values) rec.Values.Add(v);

        _db.Records.Add(rec);
        await _db.SaveChangesAsync();

        return (await ToDtoListAsync(entity, new List<Record> { rec }))[0];
    }

    public async Task<RecordDto> UpdateAsync(int entityId, long recordId, SaveRecordRequest req, string username)
    {
        var entity = await GetEntityAsync(entityId);
        var rec = await _db.Records.Include(r => r.Values)
            .FirstOrDefaultAsync(r => r.Id == recordId && r.EntityId == entityId && !r.IsDeleted)
            ?? throw new NotFoundException($"Record {recordId} not found");

        var values = await MaterializeAsync(entity, req.Values);
        await ValidateAsync(entity, values, recordId);

        foreach (var incoming in values)
        {
            var existing = rec.Values.FirstOrDefault(v => v.FieldId == incoming.FieldId);
            if (existing is null)
            {
                incoming.Record = rec;
                incoming.RecordId = rec.Id;
                _db.RecordValues.Add(incoming);
            }
            else
            {
                existing.TextValue = incoming.TextValue;
                existing.NumberValue = incoming.NumberValue;
                existing.DateValue = incoming.DateValue;
                existing.BoolValue = incoming.BoolValue;
                existing.LookupValue = incoming.LookupValue;
            }
        }

        rec.UpdatedAt = DateTime.UtcNow;
        rec.UpdatedBy = username;
        await _db.SaveChangesAsync();

        await _db.Entry(rec).Collection(r => r.Values).LoadAsync();
        return (await ToDtoListAsync(entity, new List<Record> { rec }))[0];
    }

    public async Task DeleteAsync(int entityId, long recordId, string username)
    {
        var rec = await _db.Records.FirstOrDefaultAsync(r => r.Id == recordId && r.EntityId == entityId)
            ?? throw new NotFoundException($"Record {recordId} not found");
        if (rec.IsDeleted) return;
        rec.IsDeleted = true;
        rec.UpdatedAt = DateTime.UtcNow;
        rec.UpdatedBy = username;
        await _db.SaveChangesAsync();
    }

    public async Task<int> BulkDeleteAsync(int entityId, List<long> ids, string username)
    {
        var rows = await _db.Records
            .Where(r => r.EntityId == entityId && ids.Contains(r.Id) && !r.IsDeleted)
            .ToListAsync();
        foreach (var r in rows)
        {
            r.IsDeleted = true;
            r.UpdatedAt = DateTime.UtcNow;
            r.UpdatedBy = username;
        }
        await _db.SaveChangesAsync();
        return rows.Count;
    }

    // ---------- value materialisation / validation ----------

    private async Task<List<RecordValue>> MaterializeAsync(DynamicEntity entity, Dictionary<string, object?> input)
    {
        input ??= new Dictionary<string, object?>();
        var result = new List<RecordValue>();

        foreach (var f in entity.Fields)
        {
            var key = f.Name;
            var has = input.TryGetValue(key, out var raw);
            if (!has)
            {
                // fall back to field id as key
                if (input.TryGetValue(f.Id.ToString(), out raw)) has = true;
            }
            if (!has)
            {
                if (f.IsRequired)
                    throw new RecordValidationException(new Dictionary<string, string> { [f.Name] = $"{f.Label} wajib diisi" });
                continue;
            }

            var rv = new RecordValue { FieldId = f.Id };
            switch (f.Type)
            {
                case FieldType.Text:
                case FieldType.TextArea:
                case FieldType.Email:
                    rv.TextValue = raw?.ToString();
                    if (f.MaxLength is > 0 && rv.TextValue?.Length > f.MaxLength)
                        throw new RecordValidationException(new Dictionary<string, string> { [f.Name] = $"{f.Label} maksimal {f.MaxLength} karakter" });
                    break;

                case FieldType.Number:
                case FieldType.Decimal:
                    if (raw is not null and not "")
                    {
                        if (!TryToDecimal(raw, out var num))
                            throw new RecordValidationException(new Dictionary<string, string> { [f.Name] = $"{f.Label} harus berupa angka" });
                        rv.NumberValue = num;
                    }
                    break;

                case FieldType.Date:
                    if (raw is not null and not "")
                    {
                        if (!TryToDate(raw, out var dt))
                            throw new RecordValidationException(new Dictionary<string, string> { [f.Name] = $"{f.Label} harus berupa tanggal" });
                        rv.DateValue = dt;
                    }
                    break;

                case FieldType.Boolean:
                    rv.BoolValue = raw switch
                    {
                        null => null,
                        bool b => b,
                        string s => s is "1" or "true" or "True" ? true : s is "0" or "false" or "False" ? false : null,
                        int i => i != 0,
                        _ => null
                    };
                    break;

                case FieldType.Lookup:
                    if (raw is not null and not "")
                    {
                        if (!TryToLong(raw, out var lv))
                            throw new RecordValidationException(new Dictionary<string, string> { [f.Name] = $"{f.Label} tidak valid" });
                        if (f.LookupEntityId is int targetId)
                        {
                            var ok = await _db.Records.AnyAsync(r => r.Id == lv && r.EntityId == targetId && !r.IsDeleted);
                            if (!ok)
                                throw new RecordValidationException(new Dictionary<string, string> { [f.Name] = $"{f.Label} tidak ditemukan" });
                        }
                        rv.LookupValue = lv;
                    }
                    break;
            }

            if (f.IsRequired && IsEmpty(rv))
                throw new RecordValidationException(new Dictionary<string, string> { [f.Name] = $"{f.Label} wajib diisi" });

            result.Add(rv);
        }

        return result;
    }

    private static bool IsEmpty(RecordValue v) =>
        string.IsNullOrEmpty(v.TextValue) && v.NumberValue is null && v.DateValue is null
        && v.BoolValue is null && v.LookupValue is null;

    private async Task ValidateAsync(DynamicEntity entity, List<RecordValue> values, long? excludeRecordId)
    {
        var errors = new Dictionary<string, string>();

        foreach (var f in entity.Fields.Where(x => x.IsUnique))
        {
            var v = values.FirstOrDefault(x => x.FieldId == f.Id);
            if (v is null) continue;

            bool clash = f.Type switch
            {
                FieldType.Text or FieldType.TextArea or FieldType.Email =>
                    v.TextValue != null && await _db.RecordValues.AnyAsync(rv =>
                        rv.FieldId == f.Id && rv.TextValue == v.TextValue &&
                        (excludeRecordId == null || rv.RecordId != excludeRecordId) &&
                        _db.Records.Any(r => r.Id == rv.RecordId && !r.IsDeleted)),

                FieldType.Lookup =>
                    v.LookupValue != null && await _db.RecordValues.AnyAsync(rv =>
                        rv.FieldId == f.Id && rv.LookupValue == v.LookupValue &&
                        (excludeRecordId == null || rv.RecordId != excludeRecordId) &&
                        _db.Records.Any(r => r.Id == rv.RecordId && !r.IsDeleted)),

                _ => false
            };

            if (clash) errors[f.Name] = $"{f.Label} sudah dipakai";
        }

        if (errors.Count > 0) throw new RecordValidationException(errors);
    }

    // ---------- projection ----------

    private async Task<List<RecordDto>> ToDtoListAsync(DynamicEntity entity, List<Record> rows)
    {
        if (rows.Count == 0) return new List<RecordDto>();

        // Batch-resolve every lookup column across the page in one query per target entity.
        var lookupFields = entity.Fields.Where(f => f.Type == FieldType.Lookup && f.LookupEntityId.HasValue).ToList();
        var lookupMaps = new Dictionary<int, Dictionary<long, string>>();

        foreach (var lf in lookupFields)
        {
            var ids = rows.SelectMany(r => r.Values)
                .Where(v => v.FieldId == lf.Id && v.LookupValue != null)
                .Select(v => v.LookupValue!.Value).Distinct().ToList();
            if (ids.Count == 0) { lookupMaps[lf.Id] = new(); continue; }

            var targetId = lf.LookupEntityId!.Value;
            var targetDisplay = lf.LookupDisplayField
                ?? (await _db.Entities.AsNoTracking().Where(e => e.Id == targetId).Select(e => e.DisplayField).FirstOrDefaultAsync());

            var targetEntity = await _db.Entities.AsNoTracking().FirstOrDefaultAsync(e => e.Id == targetId);
            var displayName = targetDisplay ?? targetEntity?.Fields.OrderBy(f => f.SortOrder).FirstOrDefault()?.Name;
            if (displayName is null) { lookupMaps[lf.Id] = new(); continue; }

            var targetField = await _db.Fields.AsNoTracking()
                .FirstOrDefaultAsync(f => f.EntityId == targetId && f.Name == displayName);
            if (targetField is null) { lookupMaps[lf.Id] = new(); continue; }

            var map = new Dictionary<long, string>();
            var targetRecordIds = await _db.RecordValues
                .Where(v => v.FieldId == targetField.Id && ids.Contains(v.RecordId))
                .Select(v => new { v.RecordId, v.TextValue, v.NumberValue })
                .ToListAsync();

            foreach (var t in targetRecordIds)
            {
                map[t.RecordId] = targetField.Type switch
                {
                    FieldType.Number or FieldType.Decimal => t.NumberValue?.ToString() ?? "",
                    _ => t.TextValue ?? ""
                };
            }
            lookupMaps[lf.Id] = map;
        }

        var list = new List<RecordDto>(rows.Count);
        foreach (var r in rows)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var f in entity.Fields)
            {
                var v = r.Values.FirstOrDefault(x => x.FieldId == f.Id);
                dict[f.Name] = f.Type switch
                {
                    FieldType.Text or FieldType.TextArea or FieldType.Email => v?.TextValue,
                    FieldType.Number or FieldType.Decimal => v?.NumberValue,
                    FieldType.Date => v?.DateValue,
                    FieldType.Boolean => v?.BoolValue,
                    FieldType.Lookup => v?.LookupValue == null
                        ? null
                        : (lookupMaps.TryGetValue(f.Id, out var m) && m.TryGetValue(v!.LookupValue!.Value, out var lbl) ? lbl : v.LookupValue.ToString()),
                    _ => null
                };
            }

            var displayField = entity.DisplayField
                ?? entity.Fields.OrderBy(f => f.SortOrder).FirstOrDefault()?.Name;
            var display = displayField != null && dict.TryGetValue(displayField, out var dv) ? dv?.ToString() : null;
            if (string.IsNullOrWhiteSpace(display)) display = $"#{r.Id}";

            list.Add(new RecordDto(r.Id, entity.Id, entity.Slug, r.CreatedAt, r.CreatedBy,
                r.UpdatedAt, r.UpdatedBy, dict, display));
        }

        return list;
    }

    // ---------- conversion helpers ----------

    private static bool TryToDecimal(object raw, out decimal value)
    {
        value = 0m;
        switch (raw)
        {
            case decimal d: value = d; return true;
            case int i: value = i; return true;
            case long l: value = l; return true;
            case double db: value = (decimal)db; return true;
            case float f: value = (decimal)f; return true;
        }
        var s = raw.ToString();
        if (string.IsNullOrWhiteSpace(s)) { value = 0m; return true; }
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryToDate(object raw, out DateTime value)
    {
        value = default;
        switch (raw)
        {
            case DateTime dt: value = dt; return true;
            case DateTimeOffset dto: value = dto.DateTime; return true;
        }
        var s = raw.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(s)) return false;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out value)) return true;
        return DateTime.TryParse(s, out value);
    }

    private static bool TryToLong(object raw, out long value)
    {
        value = 0;
        switch (raw)
        {
            case long l: value = l; return true;
            case int i: value = i; return true;
            case decimal d: value = (long)d; return true;
        }
        var s = raw.ToString();
        return long.TryParse(s, out value);
    }
}
