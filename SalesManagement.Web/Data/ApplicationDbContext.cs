using Microsoft.EntityFrameworkCore;
using SalesManagement.Web.Models;

namespace SalesManagement.Web.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();

        public DbSet<Store> Stores => Set<Store>();

        public DbSet<StoreMember> StoreMembers => Set<StoreMember>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.Property(user => user.FullName)
                    .HasMaxLength(100)
                    .IsRequired();

                entity.Property(user => user.Email)
                    .HasMaxLength(255)
                    .IsRequired();

                entity.HasIndex(user => user.Email)
                    .IsUnique();

                entity.Property(user => user.PasswordHash)
                    .IsRequired();
            });

            modelBuilder.Entity<Store>(entity =>
            {
                entity.Property(store => store.Name)
                    .HasMaxLength(150)
                    .IsRequired();

                entity.Property(store => store.Address)
                    .HasMaxLength(255);

                entity.Property(store => store.Phone)
                    .HasMaxLength(20);
            });

            modelBuilder.Entity<StoreMember>(entity =>
            {
                entity.Property(member => member.Role)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .IsRequired();

                entity.HasIndex(member => new { member.UserId, member.StoreId })
                    .IsUnique();

                entity.HasOne(member => member.User)
                    .WithMany(user => user.StoreMembers)
                    .HasForeignKey(member => member.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(member => member.Store)
                    .WithMany(store => store.StoreMembers)
                    .HasForeignKey(member => member.StoreId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
