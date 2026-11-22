namespace Api.Domain;

/// <summary>A floor-plan image and its access-point marker collection.</summary>
public class NetworkFloorMap
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    // Which page owns this plan: "ap", "rack", or "both" for plans that predate the split.
    // Deleting a shared plan from one page only detaches it from that page.
    public string Scope { get; set; } = "both";
    public string ImageContentType { get; set; } = string.Empty;
    public byte[] ImageBytes { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<NetworkAccessPoint> AccessPoints { get; set; } = new List<NetworkAccessPoint>();
}

public class NetworkAccessPoint
{
    public int Id { get; set; }
    public int FloorMapId { get; set; }
    public NetworkFloorMap FloorMap { get; set; } = null!;
    public string Hostname { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    // Brand/Model as the user typed it, e.g. "Aruba AP-515" - one free-text column rather than two,
    // because the register is small and the pair is always read together. The add dialog offers the
    // values already used as suggestions so the spelling does not drift.
    public string BrandModel { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    // Existing access points are primary; BCP units are standby/failover devices.
    public string ApType { get; set; } = "primary";
    // Fractions of image width/height (0..1), independent of screen size.
    public double X { get; set; }
    public double Y { get; set; }
}
