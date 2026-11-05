using Microsoft.EntityFrameworkCore;
using ServerCSharp.Models;

namespace ServerCSharp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Genre> Genres { get; set; }
        public DbSet<Story> Stories { get; set; }
        public DbSet<StoryGenre> StoryGenres { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<Favorite> Favorites { get; set; }
        public DbSet<ReadingHistory> ReadingHistories { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<StoryRating> StoryRatings { get; set; }
        public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
        public DbSet<PlanStoryAccess> PlanStoryAccesses { get; set; }
        public DbSet<UserSubscription> UserSubscriptions { get; set; }
        public DbSet<StoryPurchase> StoryPurchases { get; set; }
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }
        public DbSet<WeeklyChapterRead> WeeklyChapterReads { get; set; }
        public DbSet<BookwormBadge> BookwormBadges { get; set; }
        public DbSet<ChapterReadCredit> ChapterReadCredits { get; set; }
        public DbSet<CoinChapterUnlock> CoinChapterUnlocks { get; set; }
        public DbSet<CoinLedgerEntry> CoinLedgerEntries { get; set; }
        public DbSet<ModerationLog> ModerationLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PlanStoryAccess>().HasKey(x => new { x.PlanId, x.StoryId });
            modelBuilder.Entity<PlanStoryAccess>()
                .HasOne(x => x.Plan).WithMany(x => x.IncludedStories)
                .HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<PlanStoryAccess>()
                .HasOne(x => x.Story).WithMany()
                .HasForeignKey(x => x.StoryId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<UserSubscription>().HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<UserSubscription>().HasOne(x => x.Plan).WithMany()
                .HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<UserSubscription>().HasIndex(x => new { x.UserId, x.ExpiresAt });
            modelBuilder.Entity<StoryPurchase>().HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StoryPurchase>().HasOne(x => x.Story).WithMany()
                .HasForeignKey(x => x.StoryId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<StoryPurchase>().HasIndex(x => new { x.UserId, x.StoryId }).IsUnique();
            modelBuilder.Entity<PaymentTransaction>().HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<PaymentTransaction>().HasIndex(x => x.OrderCode).IsUnique();
            modelBuilder.Entity<PaymentTransaction>().HasIndex(x => new { x.UserId, x.Status });

            modelBuilder.Entity<WeeklyChapterRead>().HasIndex(x => new { x.UserId, x.ChapterId, x.WeekStartDate }).IsUnique();
            modelBuilder.Entity<WeeklyChapterRead>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<WeeklyChapterRead>().HasOne(x => x.Chapter).WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<BookwormBadge>().HasIndex(x => new { x.UserId, x.WeekStartDate }).IsUnique();
            modelBuilder.Entity<BookwormBadge>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ChapterReadCredit>().HasIndex(x => new { x.UserId, x.ChapterId }).IsUnique();
            modelBuilder.Entity<ChapterReadCredit>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<ChapterReadCredit>().HasOne(x => x.Chapter).WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<CoinChapterUnlock>().HasIndex(x => new { x.UserId, x.ChapterId }).IsUnique();
            modelBuilder.Entity<CoinChapterUnlock>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<CoinChapterUnlock>().HasOne(x => x.Chapter).WithMany().HasForeignKey(x => x.ChapterId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<CoinLedgerEntry>().HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
            modelBuilder.Entity<CoinLedgerEntry>().HasIndex(x => new { x.UserId, x.CreatedAt });
            modelBuilder.Entity<ModerationLog>().HasIndex(m => new { m.TargetType, m.TargetId, m.CreatedAt });
            // Composite key for StoryGenre
            modelBuilder.Entity<StoryGenre>()
                .HasKey(sg => new { sg.StoryId, sg.GenreId });

            modelBuilder.Entity<StoryGenre>()
                .HasOne(sg => sg.Story)
                .WithMany(s => s.StoryGenres)
                .HasForeignKey(sg => sg.StoryId);

            modelBuilder.Entity<StoryGenre>()
                .HasOne(sg => sg.Genre)
                .WithMany(g => g.StoryGenres)
                .HasForeignKey(sg => sg.GenreId);

            // Unique constraint on User Email
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Configure DeleteBehavior for SQL Server cascade cycle prevention
            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.User)
                .WithMany(u => u.Favorites)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.Story)
                .WithMany(s => s.Favorites)
                .HasForeignKey(f => f.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Favorite>()
                .HasIndex(f => new { f.UserId, f.StoryId })
                .IsUnique();

            modelBuilder.Entity<ReadingHistory>()
                .HasOne(h => h.User)
                .WithMany(u => u.ReadingHistories)
                .HasForeignKey(h => h.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ReadingHistory>()
                .HasOne(h => h.Story)
                .WithMany(s => s.ReadingHistories)
                .HasForeignKey(h => h.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReadingHistory>()
                .HasOne(h => h.Chapter)
                .WithMany()
                .HasForeignKey(h => h.ChapterId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ReadingHistory>()
                .HasIndex(h => new { h.UserId, h.StoryId })
                .IsUnique();

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Story)
                .WithMany(s => s.Comments)
                .HasForeignKey(c => c.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Chapter)
                .WithMany()
                .HasForeignKey(c => c.ChapterId)
                .OnDelete(DeleteBehavior.NoAction);

            // Mỗi độc giả chỉ có 1 đánh giá cho mỗi truyện; được sửa lại đánh giá.
            modelBuilder.Entity<StoryRating>()
                .HasIndex(r => new { r.UserId, r.StoryId })
                .IsUnique();

            modelBuilder.Entity<StoryRating>()
                .HasIndex(r => r.StoryId);

            modelBuilder.Entity<StoryRating>()
                .HasOne(r => r.User)
                .WithMany(u => u.Ratings)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<StoryRating>()
                .HasOne(r => r.Story)
                .WithMany(s => s.Ratings)
                .HasForeignKey(r => r.StoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Report>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reports)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
