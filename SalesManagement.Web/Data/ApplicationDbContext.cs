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
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
        public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

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

            modelBuilder.Entity<Category>(entity =>
            {
                entity.Property(category => category.Name).HasMaxLength(100).IsRequired();
                entity.HasIndex(category => new { category.StoreId, category.Name }).IsUnique();
                entity.HasOne(category => category.Store)
                    .WithMany(store => store.Categories)
                    .HasForeignKey(category => category.StoreId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Product>(entity =>
            {
                entity.Property(product => product.Name).HasMaxLength(150).IsRequired();
                entity.Property(product => product.Sku).HasMaxLength(50).IsRequired();
                entity.Property(product => product.Barcode).HasMaxLength(100);
                entity.Property(product => product.Unit).HasMaxLength(30).IsRequired();
                entity.Property(product => product.CostPrice).HasPrecision(18, 2);
                entity.Property(product => product.SalePrice).HasPrecision(18, 2);
                entity.HasIndex(product => new { product.StoreId, product.Sku }).IsUnique();
                entity.HasOne(product => product.Store)
                    .WithMany(store => store.Products)
                    .HasForeignKey(product => product.StoreId)
                    .OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(product => product.Category)
                    .WithMany(category => category.Products)
                    .HasForeignKey(product => product.CategoryId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<InventoryItem>(entity =>
            {
                entity.HasIndex(item => item.ProductId).IsUnique();
                entity.Property(item => item.RowVersion).IsRowVersion();
                entity.HasOne(item => item.Product)
                    .WithOne(product => product.Inventory)
                    .HasForeignKey<InventoryItem>(item => item.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(item => item.Store)
                    .WithMany()
                    .HasForeignKey(item => item.StoreId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            modelBuilder.Entity<InventoryTransaction>(entity =>
            {
                entity.Property(transaction => transaction.Type)
                    .HasConversion<string>()
                    .HasMaxLength(30);
                entity.Property(transaction => transaction.Note).HasMaxLength(255);
                entity.HasIndex(transaction => new { transaction.StoreId, transaction.CreatedAt });
                entity.HasOne(transaction => transaction.Product)
                    .WithMany(product => product.InventoryTransactions)
                    .HasForeignKey(transaction => transaction.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(transaction => transaction.Store)
                    .WithMany()
                    .HasForeignKey(transaction => transaction.StoreId)
                    .OnDelete(DeleteBehavior.NoAction);
                entity.HasOne(transaction => transaction.User)
                    .WithMany(user => user.InventoryTransactions)
                    .HasForeignKey(transaction => transaction.UserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
