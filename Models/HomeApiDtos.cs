namespace LibraryMPT.Models;

public sealed class HomeIndexResponse
{
    public int TotalUsers { get; set; }
    public int TotalBooks { get; set; }
    public int Downloads { get; set; }
    public int Availability { get; set; }
    public bool? IsTwoFactorEnabled { get; set; }
}

