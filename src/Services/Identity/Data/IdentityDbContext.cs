using LandaDoc.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace LandaDoc.Identity.Data;

public class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options)
{
    public DbSet<User>         Users         => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<FamilyLink>   FamilyLinks   => Set<FamilyLink>();
    public DbSet<Dependent>    Dependents    => Set<Dependent>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // ── User ─────────────────────────────────────────────────────────
        mb.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email").IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
            e.Property(x => x.Role).HasColumnName("role")
                .HasConversion<string>();
            e.Property(x => x.FirstName).HasColumnName("first_name");
            e.Property(x => x.LastName).HasColumnName("last_name");
            e.Property(x => x.Phone).HasColumnName("phone");
            e.Property(x => x.AvatarUrl).HasColumnName("avatar_url");
            e.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
            e.Property(x => x.Gender).HasColumnName("gender").HasConversion<string>();
            e.Property(x => x.IsActive).HasColumnName("is_active")
                .HasDefaultValue(true);
            e.Property(x => x.IsApproved).HasColumnName("is_approved")
                .HasDefaultValue(true);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => x.Email).IsUnique();
        });

        // ── RefreshToken ─────────────────────────────────────────────────
        mb.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.TokenHash).HasColumnName("token_hash").IsRequired();
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.IsRevoked).HasColumnName("is_revoked")
                .HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasOne(x => x.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.TokenHash);  // fast lookup on token hash
            e.HasIndex(x => x.UserId);
        });

        // ── FamilyLink ───────────────────────────────────────────────────
        mb.Entity<FamilyLink>(e =>
        {
            e.ToTable("family_links");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.RequesterUserId).HasColumnName("requester_user_id");
            e.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id");
            e.Property(x => x.RelationType).HasColumnName("relation_type")
                .HasConversion<string>();
            e.Property(x => x.RequesterIsParent).HasColumnName("requester_is_parent");
            e.Property(x => x.Status).HasColumnName("status")
                .HasConversion<string>();
            e.Property(x => x.RespondedByUserId).HasColumnName("responded_by_user_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.RespondedAt).HasColumnName("responded_at");

            // fast lookup both directions for the duplicate-link check and "my family"
            e.HasIndex(x => new { x.RequesterUserId, x.Status });
            e.HasIndex(x => new { x.RecipientUserId, x.Status });
        });

        // ── Dependent ────────────────────────────────────────────────────
        mb.Entity<Dependent>(e =>
        {
            e.ToTable("dependents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.GuardianUserId).HasColumnName("guardian_user_id");
            e.Property(x => x.FirstName).HasColumnName("first_name").IsRequired();
            e.Property(x => x.LastName).HasColumnName("last_name").IsRequired();
            e.Property(x => x.DateOfBirth).HasColumnName("date_of_birth");
            e.Property(x => x.Gender).HasColumnName("gender").HasConversion<string>();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasIndex(x => x.GuardianUserId);
        });
    }
}