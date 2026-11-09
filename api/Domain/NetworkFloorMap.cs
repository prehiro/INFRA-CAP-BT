namespace Api.Domain;

/// <summary>A floor-plan image and its access-point marker collection.</summary>
public class NetworkFloorMap
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
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
    public string Position { get; set; } = string.Empty;
    // Fractions of image width/height (0..1), independent of screen size.
    public double X { get; set; }
    public double Y { get; set; }
}
