using System;
using System.Collections.Generic;

namespace TripWise.Models
{
    public class DocumentFolder
    {
        public int IdFolder { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Color { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Навигационные свойства
        public virtual User User { get; set; }
        public virtual ICollection<UserDocument> Documents { get; set; }
    }
}