using System;
using System.Collections.Generic;

namespace TripWise.Models;

public partial class Role
{
    public int IdRole { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
