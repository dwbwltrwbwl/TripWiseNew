using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class Document
{
    public int IdDocument { get; set; }

    public string FileName { get; set; } = null!;

    public string FileType { get; set; } = null!;

    public long FileSize { get; set; }

    public string? FilePath { get; set; }

    public string? Description { get; set; }

    public DateTime UploadedAt { get; set; }

    public int IdTrip { get; set; }

    public int UploadedById { get; set; }

    public virtual Trip IdTripNavigation { get; set; } = null!;

    public virtual User UploadedBy { get; set; } = null!;
}
