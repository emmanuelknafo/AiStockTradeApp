using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AiStockTradeApp.Entities.Models;

namespace AiStockTradeApp.DataAccess.Data
{
    /// <summary>
    /// Application Identity DbContext for user authentication and authorization
    /// Separate from StockDataContext to maintain separation of concerns
    /// </summary>
    public class ApplicationIdentityContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationIdentityContext(DbContextOptions<ApplicationIdentityContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure ApplicationUser entity
            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.FirstName)
                    .HasMaxLength(50)
                    .IsUnicode(true);

                entity.Property(e => e.LastName)
                    .HasMaxLength(50)
                    .IsUnicode(true);

                entity.Property(e => e.PreferredCulture)
                    .HasMaxLength(10)
                    .HasDefaultValue("en");

                entity.Property(e => e.EnablePriceAlerts)
                    .HasDefaultValue(true);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("GETUTCDATE()");

                // Add index on email for faster lookups
                entity.HasIndex(e => e.Email)
                    .IsUnique()
                    .HasDatabaseName("IX_ApplicationUser_Email");

                // Add index on preferred culture for filtering
                entity.HasIndex(e => e.PreferredCulture)
                    .HasDatabaseName("IX_ApplicationUser_PreferredCulture");
            });

            // Customize Identity table names if needed (optional)
            builder.Entity<ApplicationUser>().ToTable("Users");
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>().ToTable("Roles");
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("UserRoles");
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("UserClaims");
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("UserLogins");
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("RoleClaims");
            builder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("UserTokens");
        }
    }
}
