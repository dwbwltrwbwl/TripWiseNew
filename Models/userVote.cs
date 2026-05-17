using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class UserVote
{
    public int IdUserVote { get; set; }

    public int IdVoteOption { get; set; }

    public int IdUser { get; set; }

    public DateTime VotedAt { get; set; }

    public virtual User IdUserNavigation { get; set; } = null!;

    public virtual VoteOption IdVoteOptionNavigation { get; set; } = null!;
}
