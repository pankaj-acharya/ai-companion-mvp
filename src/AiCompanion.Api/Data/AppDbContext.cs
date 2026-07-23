using AiCompanion.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiCompanion.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<MemoryConsent> MemoryConsents => Set<MemoryConsent>();

    public DbSet<MemoryEntry> MemoryEntries => Set<MemoryEntry>();

    public DbSet<MemoryAuditEvent> MemoryAuditEvents => Set<MemoryAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.ToTable("conversations");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasMaxLength(128);
            entity.Property(item => item.UserId).HasMaxLength(128).IsRequired();
            entity.HasIndex(item => item.UserId);
            entity.Property(item => item.CreatedAt).IsRequired();
            entity.Property(item => item.UpdatedAt).IsRequired();
            entity.HasMany(item => item.Messages)
                .WithOne(item => item.Conversation)
                .HasForeignKey(item => item.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.ToTable("messages");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Role).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Content).IsRequired();
            entity.Property(item => item.CreatedAt).IsRequired();
            entity.HasIndex(item => item.ConversationId);
        });

        modelBuilder.Entity<MemoryConsent>(entity =>
        {
            entity.ToTable("memory_consents");
            entity.HasKey(item => item.UserId);
            entity.Property(item => item.UserId).HasMaxLength(128);
            entity.Property(item => item.IsEnabled).IsRequired();
            entity.Property(item => item.UpdatedAt).IsRequired();
        });

        modelBuilder.Entity<MemoryEntry>(entity =>
        {
            entity.ToTable("memory_entries");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UserId).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Scope).HasMaxLength(32).IsRequired();
            entity.Property(item => item.Content).HasMaxLength(1000).IsRequired();
            entity.Property(item => item.IsApproved).IsRequired();
            entity.Property(item => item.CreatedAt).IsRequired();
            entity.Property(item => item.UpdatedAt).IsRequired();
            entity.HasIndex(item => new { item.UserId, item.IsApproved, item.Scope });
        });

        modelBuilder.Entity<MemoryAuditEvent>(entity =>
        {
            entity.ToTable("memory_audit_events");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.UserId).HasMaxLength(128).IsRequired();
            entity.Property(item => item.Action).HasMaxLength(64).IsRequired();
            entity.Property(item => item.Details).HasMaxLength(1024);
            entity.Property(item => item.CreatedAt).IsRequired();
            entity.HasIndex(item => item.UserId);
            entity.HasIndex(item => item.CreatedAt);
        });
    }
}