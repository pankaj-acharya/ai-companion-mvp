using AiCompanion.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AiCompanion.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<Message> Messages => Set<Message>();

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
    }
}