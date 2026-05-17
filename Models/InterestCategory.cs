using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class InterestCategory
{
    public int IdInterestCategory { get; set; }

    public string? InterestCategory1 { get; set; }

    public virtual ICollection<PointsOfInterest> PointsOfInterests { get; set; } = new List<PointsOfInterest>();
}
