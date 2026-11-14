namespace Api.Domain;

/// <summary>A network rack placed on a floor plan with its switch IPs.</summary>
public class NetworkRack
{
    public int Id { get; set; }
    public int FloorMapId { get; set; }
    public NetworkFloorMap FloorMap { get; set; } = null!;
    public string Name { get; set; } = string.Empty;        // Rack identifier (e.g., "Rack-A1")
    public string Description { get; set; } = string.Empty; // Optional description
    // Fraction of image width/height (0..1), independent of screen size.
    public double X { get; set; }
    public double Y { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<NetworkSwitch> Switches { get; set; } = new List<NetworkSwitch>();
}

/// <summary>A switch inside a network rack, identified by its IP address.</summary>
public class NetworkSwitch
{
    public int Id { get; set; }
    public int RackId { get; set; }
    public NetworkRack Rack { get; set; } = null!;
    public string IpAddress { get; set; } = string.Empty;  // Switch management IP
    public string Hostname { get; set; } = string.Empty;   // Optional hostname/label
    public string Position { get; set; } = string.Empty;   // Optional position within rack (e.g., "U1-U4")
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}