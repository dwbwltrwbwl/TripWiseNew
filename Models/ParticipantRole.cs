using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class ParticipantRole
{
    public int IdParticipantRole { get; set; }

    public string? ParticipantRole1 { get; set; }

    public virtual ICollection<TripParticipant> TripParticipants { get; set; } = new List<TripParticipant>();
}
