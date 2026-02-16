using Microsoft.EntityFrameworkCore;
using LibraryMPT.Models;

namespace LibraryMPT.Data
{
    public class LibraryContext : DbContext
    {
        public LibraryContext(DbContextOptions<LibraryContext> options)
            : base(options)
        {
        }

        public DbSet<LoginUserDto> LoginUsers { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<Book> Books { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Publisher> Publisher { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<Faculty> Faculty { get; set; }
        public DbSet<AuditLog> AuditLog { get; set; }
        public DbSet<BookLog> BookLogs { get; set; }
        public DbSet<AuthorBook> AuthorBook { get; set; }
        public DbSet<AuditSummaryDto> AuditSummaries { get; set; }
        public DbSet<LibrarianStatsDto> LibrarianStats { get; set; }
        public DbSet<CategoryStatDto> CategoryStats { get; set; }
        public DbSet<LastBookDto> LastBooks { get; set; }
        public DbSet<Subscription> Subscriptions { get; set; }
        public DbSet<SubscriptionRequestDto> SubscriptionRequests { get; set; }
        public DbSet<Bookmark> Bookmarks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<LoginUserDto>().HasNoKey();
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BookLog>()
                .HasKey(bl => bl.LogID);

            modelBuilder.Entity<UserAdminDto>().HasNoKey();
            modelBuilder.Entity<AuditSummaryDto>().HasNoKey();

            modelBuilder.Entity<LibrarianStatsDto>().HasNoKey();
            modelBuilder.Entity<CategoryStatDto>().HasNoKey();
            modelBuilder.Entity<LastBookDto>().HasNoKey();
            modelBuilder.Entity<StudentStatsDto>().HasNoKey();
            modelBuilder.Entity<BookStatisticsDto>().HasNoKey();
            modelBuilder.Entity<SubscriptionRequestDto>().HasNoKey();


            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique()
                .HasDatabaseName("IX_Users_Username");

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .HasDatabaseName("IX_Users_Email");

            modelBuilder.Entity<User>()
                .HasIndex(u => u.RoleID)
                .HasDatabaseName("IX_Users_RoleID");

            modelBuilder.Entity<User>()
                .HasIndex(u => u.FacultyID)
                .HasDatabaseName("IX_Users_FacultyID");

            modelBuilder.Entity<Book>()
                .HasIndex(b => b.Title)
                .HasDatabaseName("IX_Books_Title");

            modelBuilder.Entity<Book>()
                .HasIndex(b => b.CategoryID)
                .HasDatabaseName("IX_Books_CategoryID");

            modelBuilder.Entity<Book>()
                .HasIndex(b => b.PublisherID)
                .HasDatabaseName("IX_Books_PublisherID");

            modelBuilder.Entity<AuthorBook>()
                .HasIndex(ab => new { ab.AuthorID, ab.BookID })
                .HasDatabaseName("IX_AuthorBook_AuthorID_BookID");

            modelBuilder.Entity<BookLog>()
                .HasIndex(bl => bl.UserID)
                .HasDatabaseName("IX_BookLogs_UserID");

            modelBuilder.Entity<BookLog>()
                .HasIndex(bl => bl.BookID)
                .HasDatabaseName("IX_BookLogs_BookID");

            modelBuilder.Entity<BookLog>()
                .HasIndex(bl => bl.ActionType)
                .HasDatabaseName("IX_BookLogs_ActionType");

            modelBuilder.Entity<BookLog>()
                .HasIndex(bl => bl.ActionAt)
                .HasDatabaseName("IX_BookLogs_ActionAt");

            modelBuilder.Entity<Review>()
                .HasIndex(r => r.BookID)
                .HasDatabaseName("IX_Reviews_BookID");

            modelBuilder.Entity<Review>()
                .HasIndex(r => r.UserID)
                .HasDatabaseName("IX_Reviews_UserID");

            modelBuilder.Entity<Subscription>()
                .HasIndex(s => s.FacultyID)
                .HasDatabaseName("IX_Subscriptions_FacultyID");

            modelBuilder.Entity<Subscription>()
                .HasIndex(s => new { s.FacultyID, s.Status })
                .HasDatabaseName("IX_Subscriptions_FacultyID_Status");

            modelBuilder.Entity<Bookmark>()
                .HasIndex(b => new { b.UserID, b.BookID })
                .HasDatabaseName("IX_Bookmarks_UserID_BookID");

            modelBuilder.Entity<Bookmark>()
                .HasIndex(b => b.UserID)
                .HasDatabaseName("IX_Bookmarks_UserID");

        }
    }
}
