using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class TripParticipant
{
    public int IdTripParticipant { get; set; }

    public int IdTrip { get; set; }

    public int IdUser { get; set; }

    public int IdParticipantRole { get; set; }

    public DateTime JoinedAt { get; set; }

    public virtual ParticipantRole IdParticipantRoleNavigation { get; set; } = null!;

    public virtual Trip IdTripNavigation { get; set; } = null!;

    public virtual User IdUserNavigation { get; set; } = null!;
}
