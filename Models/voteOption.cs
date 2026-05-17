using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class VoteOption
{
    public int IdVoteOption { get; set; }

    public string OptionText { get; set; } = null!;

    public int IdVote { get; set; }

    public virtual VotingSystem IdVoteNavigation { get; set; } = null!;

    public virtual ICollection<UserVote> UserVotes { get; set; } = new List<UserVote>();
}
