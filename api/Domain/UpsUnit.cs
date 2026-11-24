using System.ComponentModel.DataAnnotations;

namespace Api.Domain;

/// <summary>One UPS in the register: what it is, its serial, and where it stands.</summary>
/// <remarks>
/// Deliberately WITHOUT the sheet's "UPS Condition" columns (Physical Condition / Lamp Indicator).
/// HIRO asked for the register only ("jangan includekan kolom ups condition"), so condition tracking is
/// not modelled at all rather than stored and hidden - a column nobody can edit is worse than none.
/// </remarks>
public class UpsUnit
{
    public int Id { get; set; }
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    /// <summary>Manufacturer serial, the natural key of the sheet. Unique per unit.</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>Where the unit stands, e.g. "CAP Server Room B2L2".</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// How the unit is carried in the asset register: "Fixed Asset" or "Non Fixed Asset".
    /// Kept as a plain string with a default rather than an enum/lookup table: it is a two-value
    /// classification that the register writes and filters on, and a lookup would add a join to the
    /// one screen that has to stay instant.
    /// </summary>
    [MaxLength(40)]
    public string AssetType { get; set; } = "Fixed Asset";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
