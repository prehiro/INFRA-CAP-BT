using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

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
public class LogbookNumberService
{
    private readonly AppDbContext _db;
    public LogbookNumberService(AppDbContext db) => _db = db;

    public const string CCTV_SLUG = "cctv_log_book";

    /// <summary>Field name holding the printed NO value on the CCTV Log Book entity.</summary>
    public const string NO_FIELD = "nomor";

    public async Task<string> NextCctvNumberAsync(DateTime? forDate, CancellationToken ct = default)
    {
        // forDate is intentionally unused now that the number no longer embeds a year. It
        // stays in the signature so the controller and API contract do not change.
        _ = forDate;

        var fieldId = await _db.Fields
            .Where(f => f.Name == NO_FIELD && f.Entity!.Slug == CCTV_SLUG)
            .Select(f => f.Id)
            .FirstOrDefaultAsync(ct);

        if (fieldId == 0) throw new InvalidOperationException("Entity CCTV Log Book belum ada.");

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
}