using Api.Contracts;
using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class DynamicSchemaService
{
    private readonly AppDbContext _db;
    public DynamicSchemaService(AppDbContext db) => _db = db;

    public async Task<EntityDto> CreateEntityAsync(CreateEntityRequest req)
    {
        var slug = Slugify(req.Slug);
        if (string.IsNullOrWhiteSpace(slug)) throw new RecordValidationException(new() { ["slug"] = "Slug tidak valid" });
        if (await _db.Entities.AnyAsync(e => e.Slug == slug))
            throw new RecordValidationException(new() { ["slug"] = $"Slug '{slug}' sudah dipakai" });

        if (!Enum.TryParse<EntityKind>(req.Kind, true, out var kind))
            throw new RecordValidationException(new() { ["kind"] = "Kind harus Master atau Transaction" });

        var entity = new DynamicEntity
        {
            Name = req.Name.Trim(),
            Slug = slug,
            Description = req.Description ?? "",
            Kind = kind,
            DisplayField = req.DisplayField,
            SortOrder = req.SortOrder,
            IsSystem = false,
            IsActive = true
        };

        var order = 0;
        foreach (var f in req.Fields)
        {
            var name = Slugify(f.Name);
            if (string.IsNullOrWhiteSpace(name))
                throw new RecordValidationException(new() { ["fields"] = $"Nama field '{f.Name}' tidak valid" });
            if (entity.Fields.Any(x => x.Name == name))
                throw new RecordValidationException(new() { ["fields"] = $"Field '{name}' duplikat dalam entity ini" });

            if (!Enum.TryParse<FieldType>(f.Type, true, out var ft))
                throw new RecordValidationException(new() { ["fields"] = $"Tipe field '{f.Type}' tidak dikenal" });

            if (ft == FieldType.Lookup && f.LookupEntityId is null)
                throw new RecordValidationException(new() { ["fields"] = $"Field lookup '{f.Label}' harus menunjuk entity" });

            entity.Fields.Add(new DynamicField
            {
                Name = name,
                Label = string.IsNullOrWhiteSpace(f.Label) ? name : f.Label.Trim(),
                Type = ft,
                IsRequired = f.IsRequired,
                IsUnique = f.IsUnique,
                IsSearchable = f.IsSearchable,
                IsVisible = f.IsVisible,
                MaxLength = ft is FieldType.Text or FieldType.TextArea or FieldType.Email ? f.MaxLength ?? 255 : null,
                DefaultValue = f.DefaultValue,
                OptionsJson = f.OptionsJson,
                LookupEntityId = f.LookupEntityId,
                LookupDisplayField = f.LookupDisplayField,
                SortOrder = order++
            });
        }

        if (entity.Fields.Count == 0)
            throw new RecordValidationException(new() { ["fields"] = "Entity minimal harus punya 1 field" });

        entity.DisplayField ??= entity.Fields.FirstOrDefault(f => f.Type is FieldType.Text)?.Name ?? entity.Fields.First().Name;

        _db.Entities.Add(entity);
        await _db.SaveChangesAsync();

        await _db.Entry(entity).Collection(e => e.Fields).LoadAsync();
        return DynamicRecordService.ToEntityDto(entity, 0);
    }

    public async Task<EntityDto> UpdateEntityAsync(int id, UpdateEntityRequest req)
    {
        var entity = await _db.Entities.Include(e => e.Fields).FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new NotFoundException($"Entity {id} not found");

        if (req.Name is not null) entity.Name = req.Name.Trim();
        if (req.Description is not null) entity.Description = req.Description;
        if (req.Kind is not null)
        {
            if (!Enum.TryParse<EntityKind>(req.Kind, true, out var k))
                throw new RecordValidationException(new() { ["kind"] = "Kind harus Master atau Transaction" });
            entity.Kind = k;
        }
        if (req.DisplayField is not null)
        {
            if (!entity.Fields.Any(f => f.Name == req.DisplayField))
                throw new RecordValidationException(new() { ["displayField"] = $"Field '{req.DisplayField}' tidak ada di entity ini" });
            entity.DisplayField = req.DisplayField;
        }
        if (req.SortOrder is not null) entity.SortOrder = req.SortOrder.Value;
        if (req.IsActive is not null) entity.IsActive = req.IsActive.Value;

        await _db.SaveChangesAsync();
        var count = await _db.Records.CountAsync(r => r.EntityId == id && !r.IsDeleted);
        return DynamicRecordService.ToEntityDto(entity, count);
    }

    public async Task<FieldDto> AddFieldAsync(int entityId, CreateFieldRequest req)
    {
        var entity = await _db.Entities.Include(e => e.Fields).FirstOrDefaultAsync(e => e.Id == entityId)
            ?? throw new NotFoundException($"Entity {entityId} not found");

        var name = Slugify(req.Name);
        if (string.IsNullOrWhiteSpace(name)) throw new RecordValidationException(new() { ["name"] = "Nama field tidak valid" });
        if (entity.Fields.Any(f => f.Name == name))
            throw new RecordValidationException(new() { ["name"] = $"Field '{name}' sudah ada" });
        if (!Enum.TryParse<FieldType>(req.Type, true, out var ft))
            throw new RecordValidationException(new() { ["type"] = $"Tipe '{req.Type}' tidak dikenal" });
        if (ft == FieldType.Lookup && req.LookupEntityId is null)
            throw new RecordValidationException(new() { ["lookupEntityId"] = "Field lookup harus menunjuk entity" });

        var maxSort = entity.Fields.Count == 0 ? 0 : entity.Fields.Max(f => f.SortOrder);
        var field = new DynamicField
        {
            EntityId = entityId,
            Name = name,
            Label = string.IsNullOrWhiteSpace(req.Label) ? name : req.Label.Trim(),
            Type = ft,
            IsRequired = req.IsRequired,
            IsUnique = req.IsUnique,
            IsSearchable = req.IsSearchable,
            IsVisible = req.IsVisible,
            MaxLength = ft is FieldType.Text or FieldType.TextArea or FieldType.Email ? req.MaxLength ?? 255 : null,
            DefaultValue = req.DefaultValue,
            OptionsJson = req.OptionsJson,
            LookupEntityId = req.LookupEntityId,
            LookupDisplayField = req.LookupDisplayField,
            SortOrder = maxSort + 1
        };
        _db.Fields.Add(field);
        await _db.SaveChangesAsync();
        return DynamicRecordService.ToFieldDto(field);
    }

    public async Task<FieldDto> UpdateFieldAsync(int fieldId, UpdateFieldRequest req)
    {
        var field = await _db.Fields.FirstOrDefaultAsync(f => f.Id == fieldId)
            ?? throw new NotFoundException($"Field {fieldId} not found");

        if (req.Label is not null) field.Label = req.Label.Trim();
        if (req.IsRequired is not null) field.IsRequired = req.IsRequired.Value;
        if (req.IsUnique is not null) field.IsUnique = req.IsUnique.Value;
        if (req.IsSearchable is not null) field.IsSearchable = req.IsSearchable.Value;
        if (req.IsVisible is not null) field.IsVisible = req.IsVisible.Value;
        if (req.SortOrder is not null) field.SortOrder = req.SortOrder.Value;
        if (req.MaxLength is not null) field.MaxLength = req.MaxLength;
        if (req.DefaultValue is not null) field.DefaultValue = req.DefaultValue;
        if (req.OptionsJson is not null) field.OptionsJson = req.OptionsJson;
        if (req.LookupEntityId is not null) field.LookupEntityId = req.LookupEntityId;
        if (req.LookupDisplayField is not null) field.LookupDisplayField = req.LookupDisplayField;
        if (req.Type is not null)
        {
            if (!Enum.TryParse<FieldType>(req.Type, true, out var ft))
                throw new RecordValidationException(new() { ["type"] = $"Tipe '{req.Type}' tidak dikenal" });
            // Type change on a populated column would orphan existing values; guard it.
            var hasData = await _db.RecordValues.AnyAsync(v => v.FieldId == fieldId);
            if (hasData && ft != field.Type)
                throw new RecordValidationException(new() { ["type"] = "Tipe field tidak bisa diubah karena sudah ada data" });
            field.Type = ft;
        }

        await _db.SaveChangesAsync();
        return DynamicRecordService.ToFieldDto(field);
    }

    public async Task DeleteFieldAsync(int fieldId)
    {
        var field = await _db.Fields.FirstOrDefaultAsync(f => f.Id == fieldId)
            ?? throw new NotFoundException($"Field {fieldId} not found");
        if (field.Entity is null) field.Entity = await _db.Entities.FirstOrDefaultAsync(e => e.Id == field.EntityId);
        if (field.Entity?.IsSystem == true)
            throw new RecordValidationException(new() { ["field"] = "Field milik system entity tidak bisa dihapus" });

        var hasData = await _db.RecordValues.AnyAsync(v => v.FieldId == fieldId);
        if (hasData)
            throw new RecordValidationException(new() { ["field"] = "Field masih punya data. Hapus/archive record terkait atau tandai field tidak terlihat." });

        _db.Fields.Remove(field);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteEntityAsync(int entityId)
    {
        var entity = await _db.Entities.FirstOrDefaultAsync(e => e.Id == entityId)
            ?? throw new NotFoundException($"Entity {entityId} not found");
        if (entity.IsSystem)
            throw new RecordValidationException(new() { ["entity"] = "System entity tidak bisa dihapus" });

        _db.Entities.Remove(entity);
        await _db.SaveChangesAsync();
    }

    public static string Slugify(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var sb = new System.Text.StringBuilder();
        var lastDash = false;
        foreach (var ch in s.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); lastDash = false; }
            else if (!lastDash && sb.Length > 0) { sb.Append('_'); lastDash = true; }
        }
        return sb.ToString().Trim('_');
    }
}
