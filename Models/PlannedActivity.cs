using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace TripWise.Models
{
    [Table("PlannedActivities")]
    public class PlannedActivity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("Id")]
        public int Id { get; set; }

        [Required]
        [Column("UserId")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(100)]
        [Column("ActivityId")]
        public string ActivityId { get; set; }

        [Required]
        [MaxLength(200)]
        [Column("Name")]
        public string Name { get; set; }

        [Required]
        [Column("Date")]
        public DateTime Date { get; set; }

        [Required]
        [Column("Time")]
        public TimeSpan Time { get; set; }

        [Column("Description")]
        public string Description { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("Category")]
        public string Category { get; set; }

        [Column("Tags")]
        public string Tags { get; set; }

        [Column("Latitude")]
        public double? Latitude { get; set; }

        [Column("Longitude")]
        public double? Longitude { get; set; }

        [MaxLength(500)]
        [Column("Address")]
        public string Address { get; set; }

        // ДОБАВЬТЕ ЭТО ПОЛЕ - ГОРОД
        [MaxLength(200)]
        [Column("City")]
        public string? City { get; set; }

        [Required]
        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; }

        // Навигационные свойства
        [ForeignKey("UserId")]
        [JsonIgnore]
        public virtual User User { get; set; }
    }
}