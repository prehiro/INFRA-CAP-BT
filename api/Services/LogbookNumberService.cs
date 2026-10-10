using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Supplies an automatic sequence number to the generic CRUD engine. Injected into
/// <see cref="DynamicRecordService"/> so that an entity whose number field is required
/// and unique can be filled server-side when the UI no longer sends it — the CCTV
/// Log Book's NO column was hidden from the table and the form on 2026-10-02, but the
/// field itself stays required+unique in the database.
/// </summary>
public interface ISequentialNumberProvider
{
    /// <returns>false when the entity has no auto-numbered field; value is then unset.</returns>
    Task<(bool handled, string value)> TryNextAsync(string entitySlug, DateTime? forDate, CancellationToken ct);
}

/// <summary>
/// Generates the sequential "NO" column of the CCTV Log Book as a plain integer string:
/// 1, 2, 3, ... (2026-10-02, at HIRO's request — the earlier 1/2026/001 format was
/// dropped because the year component is already carried by the Date column).
///
/// The counter is the highest existing plain-integer NO across all NON-deleted rows.
/// There is no year component any more, so the sequence is continuous for the life of
/// the logbook rather than resetting every January. Soft-deleted rows do not consume a
/// number, so deleting the tail row frees its NO for reuse and the sheet stays gapless.
/// Two live rows can never share a NO.
/// </summary>
public class LogbookNumberService : ISequentialNumberProvider
{
    private readonly AppDbContext _db;
    public LogbookNumberService(AppDbContext db) => _db = db;

    public const string CCTV_SLUG = "cctv_log_book";

    /// <summary>The Handover Log Book, seeded 2026-10-05. Same sequential-NO shape as CCTV.</summary>
    public const string HANDOVER_SLUG = "handover_log_book";

    /// <summary>The PC Ledger, added 2026-10-08. Same sequential-NO shape again.</summary>
    public const string PC_LEDGER_SLUG = "pc_ledger";

        /// <summary>Factory PC - the second device register, added 2026-10-10. Numbered like the others.</summary>
        public const string FACTORY_PC_SLUG = "factory_pc";

    /// <summary>Field name holding the printed NO value on the CCTV Log Book entity.</summary>
    public const string NO_FIELD = "nomor";

    /// <summary>
    /// Every entity whose NO column this service fills. A set rather than a single comparison,
    /// because the Handover Log Book uses the identical mechanism - without it TryNextAsync
    /// returned (false, "") for handover, and its required+unique `nomor` field would then
    /// fail validation on every create.
    /// </summary>
    private static readonly HashSet<string> NumberedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        CCTV_SLUG,
        HANDOVER_SLUG,
        PC_LEDGER_SLUG,
        FACTORY_PC_SLUG,
    };

    public async Task<string> NextCctvNumberAsync(DateTime? forDate, CancellationToken ct = default)
        => await NextNumberAsync(CCTV_SLUG, forDate, ct);

    /// <summary>
    /// Next sequential NO for ANY numbered logbook entity. The slug is a parameter rather
    /// than hard-wired to CCTV so the second logbook needs no copy of this method: the counter
    /// is read from that entity's own `nomor` field, so each logbook numbers independently.
    /// </summary>
    public async Task<string> NextNumberAsync(string slug, DateTime? forDate, CancellationToken ct = default)
    {
        // forDate is intentionally unused now that the number no longer embeds a year. It
        // stays in the signature so the controller and API contract do not change.
        _ = forDate;

        var fieldId = await _db.Fields
            .Where(f => f.Name == NO_FIELD && f.Entity!.Slug == slug)
            .Select(f => f.Id)
            .FirstOrDefaultAsync(ct);

        if (fieldId == 0) throw new InvalidOperationException($"Entity {slug} belum ada.");

        // Soft-deleted rows must NOT consume a number: Record.IsDeleted filters the
        // join, so deleting a row releases its NO back into the sequence instead of
        // leaving a permanent gap in the printed logbook.
        var stored = await _db.RecordValues
            .Where(v => v.FieldId == fieldId
                        && v.TextValue != null
                        && v.Record != null
                        && !v.Record.IsDeleted)
            .Select(v => v.TextValue!)
            .ToListAsync(ct);

        var max = 0;
        foreach (var raw in stored)
        {
            // Expected shape "7". Anything hand-edited into a non-integer (for example a
            // leftover value from the old 1/2026/001 format) is skipped rather than
            // guessed at, so a malformed value can never inflate the counter.
            if (int.TryParse(raw.Trim(), out var n) && n > max) max = n;
        }

        max++;
        return max.ToString();
    }

    public async Task<(bool handled, string value)> TryNextAsync(string entitySlug, DateTime? forDate, CancellationToken ct = default)
    {
        if (!NumberedSlugs.Contains(entitySlug)) return (false, "");
        return (true, await NextNumberAsync(entitySlug, forDate, ct));
    }
}