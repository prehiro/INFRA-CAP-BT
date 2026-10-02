using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Generates the sequential "NO" column of the CCTV Log Book, in the format
/// 1/2026/001 (running number / year / zero-padded running number).
///
/// The stored value is the fully formatted string, so the counter is recovered by
/// splitting on '/': part[1] is the year and part[2] is the sequence. Only rows whose
/// year matches are counted, which is what makes the sequence restart every January.
/// Deleting the last row may leave a gap, but two rows can never share a NO.
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
        var year = (forDate ?? DateTime.Now).Year;

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
            // Expected shape "1/2026/001". Anything hand-edited into another shape is
            // skipped rather than guessed at, so a malformed value can never inflate
            // the counter.
            var parts = raw.Split('/', StringSplitOptions.TrimEntries);
            if (parts.Length < 3) continue;
            if (parts[1] != year.ToString()) continue;
            if (int.TryParse(parts[2], out var n) && n > max) max = n;
        }

        max++;
        return $"{max}/{year}/{max:000}";
    }
}