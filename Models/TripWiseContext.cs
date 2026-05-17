using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using TripWise.Controllers;
using TripWise.Models;

namespace TripWise.Models;

public partial class TripWiseContext : DbContext
{
    public TripWiseContext()
    {
    }

    public TripWiseContext(DbContextOptions<TripWiseContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<Document> Documents { get; set; }

    public virtual DbSet<Expense> Expenses { get; set; }

    public virtual DbSet<ExpenseCategory> ExpenseCategories { get; set; }

    public virtual DbSet<ExpenseShare> ExpenseShares { get; set; }

    public virtual DbSet<InterestCategory> InterestCategories { get; set; }

    public virtual DbSet<ParticipantRole> ParticipantRoles { get; set; }

    public virtual DbSet<PointsOfInterest> PointsOfInterests { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Trip> Trips { get; set; }

    public virtual DbSet<TripParticipant> TripParticipants { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserVote> UserVotes { get; set; }

    public virtual DbSet<VoteOption> VoteOptions { get; set; }

    public virtual DbSet<VotingSystem> VotingSystems { get; set; }
    public virtual DbSet<NewsletterSubscription> NewsletterSubscriptions { get; set; } = null!;
    public DbSet<UserAuthToken> UserAuthTokens { get; set; }
    public DbSet<DocumentFolder> DocumentFolders { get; set; }
    public DbSet<UserDocument> UserDocuments { get; set; }
    public virtual DbSet<Chat> Chats { get; set; }
    public virtual DbSet<ChatMember> ChatMembers { get; set; }
    public virtual DbSet<ChatMessageRead> ChatMessageReads { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public virtual DbSet<TrainOrder> TrainOrders { get; set; }
    public virtual DbSet<TrainPassenger> TrainPassengers { get; set; }
    public DbSet<HotelBooking> HotelBookings { get; set; }
    public DbSet<FlightBooking> FlightBookings { get; set; }
    public DbSet<Friend> Friends { get; set; }
    public DbSet<FriendRequest> FriendRequests { get; set; }
    public DbSet<PlannedActivity> PlannedActivities { get; set; }
    public DbSet<FavoriteFlight> FavoriteFlights { get; set; }
    public DbSet<FavoriteTrain> FavoriteTrains { get; set; }
    public DbSet<FavoriteHotel> FavoriteHotels { get; set; }
    public virtual DbSet<UserPinnedMessage> UserPinnedMessages { get; set; }
    public virtual DbSet<TripInvitation> TripInvitations { get; set; }
    public DbSet<Note> Notes { get; set; }
    public DbSet<ChecklistItem> ChecklistItems { get; set; }

    //    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    //#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
    //        => optionsBuilder.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=TripWise;Trusted_Connection=true;TrustServerCertificate=true;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Конфигурация для FavoriteFlight
        modelBuilder.Entity<FavoriteFlight>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.FlightId);
            entity.HasIndex(e => new { e.UserId, e.FlightId }).IsUnique();

            entity.Property(e => e.AddedDate).HasDefaultValueSql("GETDATE()");
        });

        modelBuilder.Entity<FavoriteTrain>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TrainGroupId);
            entity.HasIndex(e => new { e.UserId, e.TrainGroupId }).IsUnique();
            entity.Property(e => e.AddedDate).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.Currency).HasDefaultValue("RUB");
        });
        modelBuilder.Entity<FavoriteHotel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.HotelId);
            entity.HasIndex(e => new { e.UserId, e.HotelId }).IsUnique();
            entity.Property(e => e.AddedDate).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.Currency).HasDefaultValue("RUB");
        });
        modelBuilder.Entity<Chat>(entity =>
        {
            entity.HasKey(e => e.IdChat);
            entity.Property(e => e.IdChat).HasColumnName("idChat");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("name");

            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");

            entity.Property(e => e.Type)
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnName("type")
                .HasDefaultValue("private");

            entity.Property(e => e.IdTrip)
                .HasColumnName("idTrip");

            entity.Property(e => e.CreatedById)
                .IsRequired()
                .HasColumnName("createdById");

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasColumnName("createdAt")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.LastMessageAt)
                .HasColumnName("lastMessageAt");

            entity.HasOne(d => d.Trip)
                .WithMany()
                .HasForeignKey(d => d.IdTrip)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.Creator)
                .WithMany(p => p.CreatedChats)
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.IdTrip).HasDatabaseName("IX_Chats_idTrip");
            entity.HasIndex(e => e.CreatedById).HasDatabaseName("IX_Chats_createdById");
            entity.HasIndex(e => e.LastMessageAt).HasDatabaseName("IX_Chats_lastMessageAt");
        });

        // Конфигурация ChatMember
        modelBuilder.Entity<ChatMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("idChatMember");

            entity.Property(e => e.ChatId)
                .IsRequired()
                .HasColumnName("idChat");

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasColumnName("idUser");

            entity.Property(e => e.JoinedAt)
                .IsRequired()
                .HasColumnName("joinedAt")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.LastReadAt)
                .HasColumnName("lastReadAt");

            entity.Property(e => e.Role)
                .IsRequired()
                .HasMaxLength(20)
                .HasColumnName("role")
                .HasDefaultValue("member");

            entity.HasOne(d => d.Chat)
                .WithMany(p => p.Members)
                .HasForeignKey(d => d.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.User)
                .WithMany(p => p.ChatMemberships)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ChatId, e.UserId })
                .IsUnique()
                .HasDatabaseName("IX_ChatMembers_ChatId_UserId");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_ChatMembers_idUser");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.IdMessage);
            entity.Property(e => e.IdMessage)
                .HasColumnName("idMessage")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.ChatId)
                .IsRequired()
                .HasColumnName("idChat");

            entity.Property(e => e.SenderId)
                .IsRequired()
                .HasColumnName("idUser");

            entity.Property(e => e.Message)
                .IsRequired()
                .HasColumnName("message");

            entity.Property(e => e.SentAt)
                .IsRequired()
                .HasColumnName("sentAt")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.EditedAt)
                .HasColumnName("editedAt");

            entity.Property(e => e.ReplyToId)
                .HasColumnName("replyToId");
            entity.Property(e => e.AttachmentsJson)
    .HasColumnName("attachmentsJson")
    .HasColumnType("nvarchar(max)");

            entity.Property(e => e.AttachmentType)
                .HasMaxLength(50)
                .HasColumnName("attachmentType");

            entity.Property(e => e.AttachmentUrl)
                .HasMaxLength(500)
                .HasColumnName("attachmentUrl");

            entity.Property(e => e.AttachmentName)
                .HasMaxLength(255)
                .HasColumnName("attachmentName");

            entity.Property(e => e.AttachmentSize)
                .HasColumnName("attachmentSize");

            // Просто поля, без связей
            entity.Property(e => e.IdTrip)
                .HasColumnName("idTrip");

            entity.Property(e => e.IdPoint)
                .HasColumnName("idPoint");

            // ТОЛЬКО эти связи
            entity.HasOne(d => d.Chat)
                .WithMany(p => p.Messages)
                .HasForeignKey(d => d.ChatId)
                .HasConstraintName("FK_ChatMessages_Chats")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Sender)
                .WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.SenderId)
                .HasConstraintName("FK_ChatMessages_Users")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.ReplyTo)
                .WithMany(p => p.Replies)
                .HasForeignKey(d => d.ReplyToId)
                .HasConstraintName("FK_ChatMessages_ReplyTo")
                .OnDelete(DeleteBehavior.Restrict);

            // НЕ ДОЛЖНО БЫТЬ:
            // entity.HasOne(d => d.Trip)...
            // entity.HasOne(d => d.Point)...

            entity.HasMany(d => d.Reads)
                .WithOne(p => p.Message)
                .HasForeignKey(p => p.MessageId)
                .HasConstraintName("FK_ChatMessageReads_Messages")
                .OnDelete(DeleteBehavior.Cascade);

            // Индексы
            entity.HasIndex(e => e.ChatId).HasDatabaseName("IX_ChatMessages_idChat");
            entity.HasIndex(e => e.SentAt).HasDatabaseName("IX_ChatMessages_sentAt");
            entity.HasIndex(e => e.ReplyToId).HasDatabaseName("IX_ChatMessages_replyToId");
            entity.HasIndex(e => e.IdTrip).HasDatabaseName("IX_ChatMessages_idTrip");
        });

        modelBuilder.Entity<ChatMessageRead>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("idChatMessageRead");

            entity.Property(e => e.MessageId)
                .IsRequired()
                .HasColumnName("idMessage");

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasColumnName("idUser");

            entity.Property(e => e.ReadAt)
                .IsRequired()
                .HasColumnName("readAt")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(d => d.Message)
                .WithMany(p => p.Reads)
                .HasForeignKey(d => d.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.User)
                .WithMany(p => p.MessageReads)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.MessageId, e.UserId })
                .IsUnique()
                .HasDatabaseName("IX_ChatMessageReads_MessageId_UserId");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_ChatMessageReads_idUser");
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.IdDocument);

            entity.HasIndex(e => e.IdTrip, "IX_Documents_idTrip");

            entity.HasIndex(e => e.UploadedById, "IX_Documents_uploadedById");

            entity.Property(e => e.IdDocument).HasColumnName("idDocument");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.FileName)
                .HasMaxLength(255)
                .HasColumnName("fileName");
            entity.Property(e => e.FilePath)
                .HasMaxLength(500)
                .HasColumnName("filePath");
            entity.Property(e => e.FileType)
                .HasMaxLength(50)
                .HasColumnName("fileType");
            entity.Property(e => e.IdTrip).HasColumnName("idTrip");
            entity.Property(e => e.UploadedAt).HasColumnName("uploadedAt");
            entity.Property(e => e.UploadedById).HasColumnName("uploadedById");

            entity.HasOne(d => d.IdTripNavigation).WithMany(p => p.Documents).HasForeignKey(d => d.IdTrip);

            entity.HasOne(d => d.UploadedBy).WithMany(p => p.Documents)
                .HasForeignKey(d => d.UploadedById)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.IdExpense);

            entity.HasIndex(e => e.IdPoint, "IX_Expenses_idPoint");

            entity.HasIndex(e => e.IdTrip, "IX_Expenses_idTrip");

            entity.HasIndex(e => e.PaidById, "IX_Expenses_paidById");

            entity.Property(e => e.IdExpense).HasColumnName("idExpense");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("amount");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.ExpenseDate).HasColumnName("expenseDate");
            entity.Property(e => e.IdExpenseCategory).HasColumnName("idExpenseCategory");
            entity.Property(e => e.IdPoint).HasColumnName("idPoint");
            entity.Property(e => e.IdTrip).HasColumnName("idTrip");
            entity.Property(e => e.PaidById).HasColumnName("paidById");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");

            entity.HasOne(d => d.IdExpenseCategoryNavigation).WithMany(p => p.Expenses)
                .HasForeignKey(d => d.IdExpenseCategory)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Expenses_ExpenseCategories");

            entity.HasOne(d => d.IdPointNavigation).WithMany(p => p.Expenses).HasForeignKey(d => d.IdPoint);

            entity.HasOne(d => d.IdTripNavigation).WithMany(p => p.Expenses).HasForeignKey(d => d.IdTrip);

            entity.HasOne(d => d.PaidBy).WithMany(p => p.Expenses)
                .HasForeignKey(d => d.PaidById)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<ExpenseCategory>(entity =>
        {
            entity.HasKey(e => e.IdExpenseCategory);

            entity.Property(e => e.IdExpenseCategory).HasColumnName("idExpenseCategory");
            entity.Property(e => e.ExpenseCategoryName).HasMaxLength(100);
        });

        modelBuilder.Entity<ExpenseShare>(entity =>
        {
            entity.HasKey(e => e.IdExpenseShare);

            entity.HasIndex(e => e.IdExpense, "IX_ExpenseShares_idExpense");

            entity.HasIndex(e => e.IdUser, "IX_ExpenseShares_idUser");

            entity.Property(e => e.IdExpenseShare).HasColumnName("idExpenseShare");
            entity.Property(e => e.IdExpense).HasColumnName("idExpense");
            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.IsPaid).HasColumnName("isPaid");
            entity.Property(e => e.ShareAmount)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("shareAmount");

            entity.HasOne(d => d.IdExpenseNavigation).WithMany(p => p.ExpenseShares).HasForeignKey(d => d.IdExpense);

            entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.ExpenseShares)
                .HasForeignKey(d => d.IdUser)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<InterestCategory>(entity =>
        {
            entity.HasKey(e => e.IdInterestCategory);

            entity.Property(e => e.IdInterestCategory).HasColumnName("idInterestCategory");
            entity.Property(e => e.InterestCategory1)
                .HasMaxLength(100)
                .HasColumnName("InterestCategory");
        });

        modelBuilder.Entity<ParticipantRole>(entity =>
        {
            entity.HasKey(e => e.IdParticipantRole);

            entity.Property(e => e.IdParticipantRole).HasColumnName("idParticipantRole");
            entity.Property(e => e.ParticipantRole1)
                .HasMaxLength(50)
                .HasColumnName("ParticipantRole");
        });

        modelBuilder.Entity<PointsOfInterest>(entity =>
        {
            entity.HasKey(e => e.IdPoint);

            entity.ToTable("PointsOfInterest");

            entity.HasIndex(e => e.AddedById, "IX_PointsOfInterest_addedById");

            entity.HasIndex(e => e.IdTrip, "IX_PointsOfInterest_idTrip");

            entity.Property(e => e.IdPoint).HasColumnName("idPoint");
            entity.Property(e => e.AddedById).HasColumnName("addedById");
            entity.Property(e => e.Address)
                .HasMaxLength(255)
                .HasColumnName("address");
            entity.Property(e => e.BookingLink)
                .HasMaxLength(500)
                .HasColumnName("bookingLink");
            entity.Property(e => e.Cost)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("cost");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IdInterestCategory).HasColumnName("idInterestCategory");
            entity.Property(e => e.IdTrip).HasColumnName("idTrip");
            entity.Property(e => e.Latitude)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("longitude");
            entity.Property(e => e.Name)
                .HasMaxLength(200)
                .HasColumnName("name");
            entity.Property(e => e.Notes).HasColumnName("notes");
            entity.Property(e => e.PlannedDate).HasColumnName("plannedDate");
            entity.Property(e => e.PlannedTime).HasColumnName("plannedTime");

            entity.HasOne(d => d.AddedBy).WithMany(p => p.PointsOfInterests).HasForeignKey(d => d.AddedById);

            entity.HasOne(d => d.IdInterestCategoryNavigation).WithMany(p => p.PointsOfInterests)
                .HasForeignKey(d => d.IdInterestCategory)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PointsOfInterest_InterestCategories");

            entity.HasOne(d => d.IdTripNavigation).WithMany(p => p.PointsOfInterests).HasForeignKey(d => d.IdTrip);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.IdRole);

            entity.Property(e => e.IdRole).HasColumnName("idRole");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .HasColumnName("name");
        });

        modelBuilder.Entity<Trip>(entity =>
        {
            entity.HasKey(e => e.IdTrip);

            entity.HasIndex(e => e.CreatedById, "IX_Trips_createdById");

            entity.Property(e => e.IdTrip).HasColumnName("idTrip");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.CreatedById).HasColumnName("createdById");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.EndDate).HasColumnName("endDate");
            entity.Property(e => e.StartDate).HasColumnName("startDate");
            entity.Property(e => e.Title)
                .HasMaxLength(200)
                .HasColumnName("title");
            entity.Property(e => e.TotalBudget)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("totalBudget");

            entity.HasOne(d => d.CreatedBy).WithMany(p => p.Trips)
                .HasForeignKey(d => d.CreatedById)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<TripParticipant>(entity =>
        {
            entity.HasKey(e => e.IdTripParticipant);

            entity.HasIndex(e => e.IdTrip, "IX_TripParticipants_idTrip");

            entity.HasIndex(e => e.IdUser, "IX_TripParticipants_idUser");

            entity.Property(e => e.IdTripParticipant).HasColumnName("idTripParticipant");
            entity.Property(e => e.IdParticipantRole).HasColumnName("idParticipantRole");
            entity.Property(e => e.IdTrip).HasColumnName("idTrip");
            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.JoinedAt).HasColumnName("joinedAt");

            entity.HasOne(d => d.IdParticipantRoleNavigation).WithMany(p => p.TripParticipants)
                .HasForeignKey(d => d.IdParticipantRole)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_TripParticipants_ParticipantRoles");

            entity.HasOne(d => d.IdTripNavigation).WithMany(p => p.TripParticipants).HasForeignKey(d => d.IdTrip);

            entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.TripParticipants).HasForeignKey(d => d.IdUser);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.IdUser);

            entity.HasIndex(e => e.IdRole, "IX_Users_idRole");

            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.Age).HasColumnName("age");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.IdRole).HasColumnName("idRole");

            // Заменяем Name на три поля
            entity.Property(e => e.LastName)           // ← добавлено
                .HasMaxLength(50)
                .HasColumnName("last_name")
                .IsRequired();

            entity.Property(e => e.FirstName)          // ← добавлено
                .HasMaxLength(50)
                .HasColumnName("first_name")
                .IsRequired();

            entity.Property(e => e.MiddleName)         // ← добавлено
                .HasMaxLength(50)
                .HasColumnName("middle_name")
                .IsRequired(false); // Отчество может быть null

            entity.Property(e => e.PasswordHash).HasColumnName("passwordHash");

            entity.HasOne(d => d.IdRoleNavigation).WithMany(p => p.Users).HasForeignKey(d => d.IdRole);
        });

        modelBuilder.Entity<UserVote>(entity =>
        {
            entity.HasKey(e => e.IdUserVote);

            entity.HasIndex(e => e.IdUser, "IX_UserVotes_idUser");

            entity.HasIndex(e => e.IdVoteOption, "IX_UserVotes_idVoteOption");

            entity.Property(e => e.IdUserVote).HasColumnName("idUserVote");
            entity.Property(e => e.IdUser).HasColumnName("idUser");
            entity.Property(e => e.IdVoteOption).HasColumnName("idVoteOption");
            entity.Property(e => e.VotedAt).HasColumnName("votedAt");

            entity.HasOne(d => d.IdUserNavigation).WithMany(p => p.UserVotes)
                .HasForeignKey(d => d.IdUser)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.IdVoteOptionNavigation).WithMany(p => p.UserVotes).HasForeignKey(d => d.IdVoteOption);
        });

        modelBuilder.Entity<VoteOption>(entity =>
        {
            entity.HasKey(e => e.IdVoteOption);

            entity.HasIndex(e => e.IdVote, "IX_VoteOptions_idVote");

            entity.Property(e => e.IdVoteOption).HasColumnName("idVoteOption");
            entity.Property(e => e.IdVote).HasColumnName("idVote");
            entity.Property(e => e.OptionText)
                .HasMaxLength(200)
                .HasColumnName("optionText");

            entity.HasOne(d => d.IdVoteNavigation).WithMany(p => p.VoteOptions).HasForeignKey(d => d.IdVote);
        });

        modelBuilder.Entity<VotingSystem>(entity =>
        {
            entity.HasKey(e => e.IdVote);
            entity.Property(e => e.IdVote).HasColumnName("IdVote").ValueGeneratedOnAdd();

            entity.Property(e => e.Question).HasColumnName("question");
            entity.Property(e => e.CreatedAt).HasColumnName("createdAt");
            entity.Property(e => e.ExpiresAt).HasColumnName("expiresAt");
            entity.Property(e => e.IdTrip).HasColumnName("idTrip");
            entity.Property(e => e.CreatedById).HasColumnName("createdById");
            entity.Property(e => e.IdPoint).HasColumnName("idPoint");
            entity.Property(e => e.IdChat).HasColumnName("idChat");

            // Исправленные связи - Указываем правильные имена внешних ключей
            entity.HasOne(e => e.CreatedBy)
                .WithMany()
                .HasForeignKey(e => e.CreatedById)
                .HasConstraintName("FK_votingSystems_Users_createdById")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.IdTripNavigation)
                .WithMany()
                .HasForeignKey(e => e.IdTrip)
                .HasConstraintName("FK_votingSystems_Trips_idTrip")
                .OnDelete(DeleteBehavior.SetNull); // SetNull вместо Cascade

            entity.HasOne(e => e.IdPointNavigation)
                .WithMany()
                .HasForeignKey(e => e.IdPoint)
                .HasConstraintName("FK_votingSystems_PointsOfInterest_idPoint")
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.IdChatNavigation)
                .WithMany()
                .HasForeignKey(e => e.IdChat)
                .HasConstraintName("FK_votingSystems_Chats_idChat")
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<UserAuthToken>().ToTable("UserAuthTokens", t => t.ExcludeFromMigrations());
        modelBuilder.Entity<UserAuthToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Token).IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.ExpiresAt).IsRequired();

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<DocumentFolder>(entity =>
        {
            entity.HasKey(e => e.IdFolder);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(d => d.User)
                .WithMany(p => p.DocumentFolders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Настройка UserDocuments
        modelBuilder.Entity<UserDocument>(entity =>
        {
            entity.HasKey(e => e.IdDocument);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.FileType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(500);
            entity.Property(e => e.DocumentType).HasMaxLength(100);
            entity.Property(e => e.DocumentNumber).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(d => d.Folder)
                .WithMany(p => p.Documents)
                .HasForeignKey(d => d.FolderId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(d => d.User)
                .WithMany(p => p.UserDocuments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.NoAction); // ВАЖНО: NoAction вместо Cascade!
        });

        modelBuilder.Entity<TrainOrder>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.OrderNumber)
                .HasMaxLength(50)
                .IsRequired();

            entity.HasIndex(e => e.OrderNumber)
                .IsUnique()
                .HasDatabaseName("IX_TrainOrders_OrderNumber");

            entity.Property(e => e.TrainNumber)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.ReturnTrainNumber)
                .HasMaxLength(20);

            entity.Property(e => e.DepartureStationId)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.DepartureStationName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.ArrivalStationId)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.ArrivalStationName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.Currency)
                .HasMaxLength(10)
                .HasDefaultValue("RUB");

            entity.Property(e => e.CarType)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.CarClass)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.SeatNumbers)
                .HasMaxLength(200);

            entity.Property(e => e.CarNumber)
                .HasMaxLength(20);

            entity.Property(e => e.ContactEmail)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.ContactPhone)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.PassengerFullName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(e => e.PassengerDocumentType)
                .HasMaxLength(50);

            entity.Property(e => e.PassengerDocumentNumber)
                .HasMaxLength(50);

            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(50);

            entity.Property(e => e.TransactionId)
                .HasMaxLength(100);

            entity.Property(e => e.BookingReference)
                .HasMaxLength(50);

            entity.Property(e => e.TicketNumber)
                .HasMaxLength(50);

            entity.Property(e => e.ElectronicTicketUrl)
                .HasMaxLength(500);

            entity.Property(e => e.TotalPrice)
                .HasColumnType("decimal(18,2)");

            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_TrainOrders_UserId");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_TrainOrders_CreatedAt");
        });

        // Конфигурация для TrainPassenger
        modelBuilder.Entity<TrainPassenger>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.FirstName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.LastName)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(e => e.MiddleName)
                .HasMaxLength(100);

            entity.Property(e => e.Gender)
                .HasMaxLength(1)
                .IsRequired();

            entity.Property(e => e.DocumentType)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.DocumentNumber)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.Citizenship)
                .HasMaxLength(50);

            entity.Property(e => e.SeatNumber)
                .HasMaxLength(20);

            entity.Property(e => e.CarNumber)
                .HasMaxLength(20);

            entity.Property(e => e.Price)
                .HasColumnType("decimal(18,2)");

            entity.HasOne(d => d.Order)
                .WithMany()
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.OrderId)
                .HasDatabaseName("IX_TrainPassengers_OrderId");
        });
        modelBuilder.Entity<HotelBooking>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.BookingNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.BookingNumber).IsUnique();

            entity.Property(e => e.HotelId).HasMaxLength(50).IsRequired();
            entity.Property(e => e.HotelName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.HotelAddress).HasMaxLength(500);
            entity.Property(e => e.HotelPhone).HasMaxLength(50);
            entity.Property(e => e.HotelWebsite).HasMaxLength(500);
            entity.Property(e => e.AccommodationType).HasMaxLength(50);

            entity.Property(e => e.Currency).HasMaxLength(10).HasDefaultValue("RUB");
            entity.Property(e => e.ContactName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ContactEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContactPhone).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SpecialRequests).HasMaxLength(1000);

            entity.Property(e => e.PaymentMethod).HasMaxLength(50);
            entity.Property(e => e.TransactionId).HasMaxLength(100);
            entity.Property(e => e.CancellationReason).HasMaxLength(500);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CheckInDate);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<FlightBooking>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).HasMaxLength(50);
            entity.Property(e => e.BookingNumber).HasMaxLength(50).IsRequired();
            entity.HasIndex(e => e.BookingNumber).IsUnique();

            entity.Property(e => e.FlightId).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Airline).HasMaxLength(200).IsRequired();
            entity.Property(e => e.AirlineCode).HasMaxLength(20);
            entity.Property(e => e.FlightNumber).HasMaxLength(20).IsRequired();
            entity.Property(e => e.DepartureCity).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ArrivalCity).HasMaxLength(200).IsRequired();
            entity.Property(e => e.DepartureAirport).HasMaxLength(10).IsRequired();
            entity.Property(e => e.ArrivalAirport).HasMaxLength(10).IsRequired();

            entity.Property(e => e.ReturnFlightId).HasMaxLength(100);
            entity.Property(e => e.ReturnAirline).HasMaxLength(200);
            entity.Property(e => e.ReturnFlightNumber).HasMaxLength(20);

            entity.Property(e => e.Currency).HasMaxLength(10).HasDefaultValue("RUB");
            entity.Property(e => e.FlightClass).HasMaxLength(20).HasDefaultValue("economy");

            entity.Property(e => e.Baggage).HasMaxLength(100);
            entity.Property(e => e.HandLuggage).HasMaxLength(100);
            entity.Property(e => e.Meal).HasMaxLength(100);

            entity.Property(e => e.ContactName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ContactEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ContactPhone).HasMaxLength(20).IsRequired();

            entity.Property(e => e.SeatNumbers).HasMaxLength(500);

            entity.Property(e => e.PaymentMethod).HasMaxLength(50);
            entity.Property(e => e.TransactionId).HasMaxLength(100);

            entity.Property(e => e.BookingReference).HasMaxLength(20);
            entity.Property(e => e.TicketNumber).HasMaxLength(50);

            entity.Property(e => e.CancellationReason).HasMaxLength(500);

            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.BookingReference);
            entity.HasIndex(e => e.TicketNumber);
            entity.HasIndex(e => e.DepartureDateTime);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<Friend>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.UserId, e.FriendId }).IsUnique();

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.FriendUser)
                .WithMany()
                .HasForeignKey(e => e.FriendId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FriendRequest>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => new { e.SenderId, e.ReceiverId }).IsUnique();

            entity.HasOne(e => e.Sender)
                .WithMany()
                .HasForeignKey(e => e.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Receiver)
                .WithMany()
                .HasForeignKey(e => e.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<PlannedActivity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.UserId, "IX_PlannedActivities_UserId");
            entity.HasIndex(e => e.Date, "IX_PlannedActivities_Date");
            entity.HasIndex(e => e.Category, "IX_PlannedActivities_Category");

            entity.Property(e => e.Id).HasColumnName("Id");

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasColumnName("UserId");

            entity.Property(e => e.ActivityId)
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnName("ActivityId");

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnName("Name");

            entity.Property(e => e.Date)
                .IsRequired()
                .HasColumnName("Date");

            entity.Property(e => e.Time)
                .IsRequired()
                .HasColumnName("Time");

            entity.Property(e => e.Description)
                .HasColumnName("Description")
                .HasColumnType("nvarchar(max)");

            entity.Property(e => e.Category)
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnName("Category");

            entity.Property(e => e.Tags)
                .HasColumnName("Tags")
                .HasColumnType("nvarchar(max)");

            entity.Property(e => e.Latitude)
                .HasColumnName("Latitude");

            entity.Property(e => e.Longitude)
                .HasColumnName("Longitude");

            entity.Property(e => e.Address)
                .HasMaxLength(500)
                .HasColumnName("Address");

            entity.Property(e => e.CreatedAt)
                .IsRequired()
                .HasColumnName("CreatedAt")
                .HasDefaultValueSql("GETUTCDATE()");

            // Связь с пользователем
            entity.HasOne(e => e.User)
                .WithMany(u => u.PlannedActivities) // Предполагая, что у User есть коллекция PlannedActivities
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<UserPinnedMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.UserId)
                .IsRequired()
                .HasColumnName("userId");

            entity.Property(e => e.ChatId)
                .IsRequired()
                .HasColumnName("chatId");

            entity.Property(e => e.MessageId)
                .IsRequired()
                .HasColumnName("messageId");

            entity.Property(e => e.PinnedAt)
                .IsRequired()
                .HasColumnName("pinnedAt")
                .HasDefaultValueSql("GETUTCDATE()");

            // Индексы и уникальные ограничения
            entity.HasIndex(e => e.ChatId)
                .HasDatabaseName("IX_UserPinnedMessages_chatId");

            entity.HasIndex(e => e.MessageId)
                .HasDatabaseName("IX_UserPinnedMessages_messageId");

            entity.HasIndex(e => new { e.UserId, e.ChatId })
                .IsUnique()
                .HasDatabaseName("IX_UserPinnedMessages_userId_chatId");

            // Связи
            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_UserPinnedMessages_Users_userId")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Chat)
                .WithMany()
                .HasForeignKey(d => d.ChatId)
                .HasConstraintName("FK_UserPinnedMessages_Chats_chatId")
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(d => d.Message)
                .WithMany()
                .HasForeignKey(d => d.MessageId)
                .HasConstraintName("FK_UserPinnedMessages_ChatMessages_messageId")
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TripInvitation>(entity =>
        {
            entity.ToTable("TripInvitations");

            entity.HasKey(e => e.IdInvitation);

            entity.Property(e => e.IdInvitation)
                .HasColumnName("idInvitation")
                .ValueGeneratedOnAdd();

            entity.Property(e => e.IdTrip)
                .HasColumnName("idTrip")
                .IsRequired();

            entity.Property(e => e.InviterId)
                .HasColumnName("inviterId")
                .IsRequired();

            entity.Property(e => e.InvitedId)
                .HasColumnName("invitedId")
                .IsRequired();

            entity.Property(e => e.Message)
                .HasColumnName("message")
                .HasMaxLength(500);

            entity.Property(e => e.InvitedAt)
                .HasColumnName("invitedAt")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.RespondedAt)
                .HasColumnName("respondedAt");

            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasDefaultValue("pending");

            // Связи
            entity.HasOne(e => e.Trip)
                .WithMany()
                .HasForeignKey(e => e.IdTrip)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Inviter)
                .WithMany()
                .HasForeignKey(e => e.InviterId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Invited)
                .WithMany()
                .HasForeignKey(e => e.InvitedId)
                .OnDelete(DeleteBehavior.Restrict);

            // Индексы
            entity.HasIndex(e => new { e.IdTrip, e.InvitedId, e.Status })
                .HasDatabaseName("IX_TripInvitations_Trip_Invited_Status");

            entity.HasIndex(e => e.InvitedId)
                .HasDatabaseName("IX_TripInvitations_InvitedId");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_TripInvitations_Status");
        });
        modelBuilder.Entity<Note>(entity =>
        {
            entity.ToTable("Notes");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Content)
                .HasMaxLength(5000);

            entity.Property(e => e.Color)
                .HasMaxLength(50);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}