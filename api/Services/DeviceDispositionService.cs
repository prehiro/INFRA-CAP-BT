using System.Data;
using Api.Contracts;
using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>Move a domain-joined PC to Factory PC as one all-or-nothing operation.</summary>
public sealed class DeviceDispositionService
{
    private readonly AppDbContext _db;
    private readonly DynamicRecordService _records;
    private readonly AuditService _audit;

    public DeviceDispositionService(AppDbContext db, DynamicRecordService records, AuditService audit)
    {
        _db = db;
        _records = records;
        _audit = audit;
    }

    public async Task<RecordDto> ConvertToFactoryAsync(int entityId, long recordId, SaveRecordRequest request, string username)
    {
        var sourceEntity = await _records.GetEntityAsync(entityId);
        if (sourceEntity.Slug != LogbookNumberService.PC_LEDGER_SLUG)
            throw new RecordValidationException(new() { ["entity"] = "Only PC Ledger records can be converted to Factory PC." });

        // The UI sends the complete edited form. Never trust an incoming target number or status:
        // the factory number is generated independently and its status always starts Active.
        var input = request.Values is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(request.Values);
        if (!input.TryGetValue("asset_status", out var disposition)
            || disposition?.ToString()?.Trim() != "Convert Factory PC")
            throw new RecordValidationException(new() { ["asset_status"] = "Select Convert Factory PC before saving." });
        input.Remove("asset_status");
        var dispositionField = sourceEntity.Fields.FirstOrDefault(f => f.Name == "asset_status");
        if (dispositionField is not null) input.Remove(dispositionField.Id.ToString());

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // Validate and save the form first. If factory validation or numbering fails, the source
        // edit is rolled back as well; the original record and all its fields remain untouched.
        var source = await _records.UpdateAsync(entityId, recordId, new SaveRecordRequest(input), username);
        var targetEntity = await _records.GetEntityBySlugAsync(LogbookNumberService.FACTORY_PC_SLUG);
        static string Value(RecordDto row, string name)
            => row.Values.TryGetValue(name, out var value) ? value?.ToString()?.Trim() ?? "" : "";

        var firstRemark = Value(source, "remark2");
        var secondRemark = Value(source, "remark3");
        var remarks = firstRemark.Length > 0 && secondRemark.Length > 0
            ? $"Remark1: {firstRemark}\nRemark2: {secondRemark}"
            : firstRemark.Length > 0 ? firstRemark : secondRemark;
        var factoryValues = new Dictionary<string, object?>
        {
            ["pic"] = Value(source, "staff_name"),
            ["email"] = Value(source, "email"),
            ["chassis"] = Value(source, "chassis"),
            ["model"] = Value(source, "computer_model"),
            ["sn"] = Value(source, "computer_sn"),
            ["os"] = Value(source, "os_name"),
            ["remarks"] = remarks,
            ["status"] = "Active"
        };
        // Archive inside the transaction BEFORE creating Factory PC: global serial uniqueness
        // must not treat this same source PC as a different live device. A failed Factory create
        // rolls this archive and the preceding edit back together.
        await _records.DeleteAsync(entityId, recordId, username);
        var factory = await _records.CreateAsync(targetEntity.Id, new SaveRecordRequest(factoryValues), username);
        await _audit.LogAsync(AuditActions.RecordTransfer, "PC Ledger",
            $"Converted PC Ledger record #{recordId} to Factory PC record #{factory.Id}",
            username: username, targetId: recordId.ToString(),
            details: new { sourceEntityId = entityId, sourceRecordId = recordId, targetEntityId = targetEntity.Id, targetRecordId = factory.Id });
        await transaction.CommitAsync();
        return factory;
    }

    /// <summary>Move a standalone Factory PC into the domain PC ledger or its status views.</summary>
    public async Task<RecordDto> ConvertToLedgerAsync(int entityId, long recordId, SaveRecordRequest request, string username)
    {
        var sourceEntity = await _records.GetEntityAsync(entityId);
        if (sourceEntity.Slug != LogbookNumberService.FACTORY_PC_SLUG)
            throw new RecordValidationException(new() { ["entity"] = "Only Factory PC records can be moved to PC Ledger." });

        var input = request.Values is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(request.Values);
        if (!input.TryGetValue("status", out var selected)
            || selected?.ToString()?.Trim() is not ("Standby" or "Plan Dispose" or "Domain PC"))
            throw new RecordValidationException(new() { ["status"] = "Select Standby, Plan Dispose, or Domain PC before moving this record." });
        var destinationStatus = selected!.ToString()!.Trim() == "Domain PC" ? "" : selected.ToString()!.Trim();
        input.Remove("status");
        var statusField = sourceEntity.Fields.FirstOrDefault(f => f.Name == "status");
        if (statusField is not null) input.Remove(statusField.Id.ToString());

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // Persist edits before transferring, but never store the transient action as Factory status.
        // Any failed target validation rolls this edit and the archive back together.
        var source = await _records.UpdateAsync(entityId, recordId, new SaveRecordRequest(input), username);
        var targetEntity = await _records.GetEntityBySlugAsync(LogbookNumberService.PC_LEDGER_SLUG);
        static string Value(RecordDto row, string name)
            => row.Values.TryGetValue(name, out var value) ? value?.ToString()?.Trim() ?? "" : "";
        var remarks = Value(source, "remarks");
        // The Factory field accepts 500 characters; the ledger has two 200-character remarks.
        // Preserve the text across both fields, or reject the transfer instead of truncating it.
        var firstField = targetEntity.Fields.FirstOrDefault(f => f.Name == "remark2");
        var secondField = targetEntity.Fields.FirstOrDefault(f => f.Name == "remark3");
        var firstLimit = firstField is null ? 0 : firstField.MaxLength > 0 ? firstField.MaxLength.Value : remarks.Length;
        var secondLimit = secondField is null ? 0 : secondField.MaxLength > 0 ? secondField.MaxLength.Value : remarks.Length;
        if ((long)remarks.Length > (long)firstLimit + secondLimit)
            throw new RecordValidationException(new() { ["remarks"] = "Remarks are too long for the PC Ledger remark fields. Shorten them before moving this PC." });
        var split = Math.Min(remarks.Length, firstLimit);
        var ledgerValues = new Dictionary<string, object?>
        {
            ["staff_name"] = Value(source, "pic"),
            ["email"] = Value(source, "email"),
            ["chassis"] = Value(source, "chassis"),
            ["computer_model"] = Value(source, "model"),
            ["computer_sn"] = Value(source, "sn"),
            ["os_name"] = Value(source, "os"),
            ["remark2"] = remarks[..split],
            ["remark3"] = remarks[split..],
            ["asset_status"] = destinationStatus
        };
        // Archive first so the global S/N rule sees only one live device. The serial lock
        // acquired by CreateAsync remains owned by this outer transaction until commit.
        await _records.DeleteAsync(entityId, recordId, username);
        var ledger = await _records.CreateAsync(targetEntity.Id, new SaveRecordRequest(ledgerValues), username);
        await _audit.LogAsync(AuditActions.RecordTransfer, "Factory PC",
            $"Moved Factory PC record #{recordId} to PC Ledger record #{ledger.Id} ({(destinationStatus == "" ? "Domain PC" : destinationStatus)})",
            username: username, targetId: recordId.ToString(),
            details: new { sourceEntityId = entityId, sourceRecordId = recordId, targetEntityId = targetEntity.Id, targetRecordId = ledger.Id, destinationStatus });
        await transaction.CommitAsync();
        return ledger;
    }
}
