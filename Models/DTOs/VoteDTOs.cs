// DTOs/VoteDTOs.cs
namespace TripWise.Models.DTOs
{
    public class CreateVoteRequest
    {
        public int ChatId { get; set; }
        public string Question { get; set; } = "";
        public List<string> Options { get; set; } = new();
        public DateTime? ExpiresAt { get; set; }
        public int? TripId { get; set; }
        public int? PointId { get; set; }
    }

    public class VoteDto
    {
        public int Id { get; set; }
        public string Question { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        // Исправляем - делаем обычное свойство с getter'ом
        public bool IsExpired
        {
            get => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
            set { } // Добавляем пустой setter для совместимости с сериализацией
        }

        public int CreatedById { get; set; }
        public string CreatedByName { get; set; } = "";
        public int? TripId { get; set; }
        public string? TripName { get; set; }
        public int? PointId { get; set; }
        public int? ChatId { get; set; }
        public List<VoteOptionDto> Options { get; set; } = new();
        public int TotalVotes { get; set; }
        public bool UserHasVoted { get; set; }
        public int? UserVoteOptionId { get; set; }
    }

    public class VoteOptionDto
    {
        public int Id { get; set; }
        public string Text { get; set; } = "";
        public int VotesCount { get; set; }
        public int TotalVotes { get; set; }

        // Вычисляемое свойство
        public int Percentage => TotalVotes > 0 ? (int)((VotesCount / (double)TotalVotes) * 100) : 0;

        public List<int> VoterIds { get; set; } = new();
    }

    public class VoteMessageDto
    {
        public int VoteId { get; set; }
        public string Question { get; set; } = "";
        public List<VoteOptionDto> Options { get; set; } = new();
        public DateTime? ExpiresAt { get; set; }

        // Вычисляемое свойство
        public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;

        public bool UserHasVoted { get; set; }
        public int? UserVoteOptionId { get; set; }
    }

    public class SubmitVoteRequest
    {
        public int VoteId { get; set; }
        public int OptionId { get; set; }
    }
}