using Microsoft.EntityFrameworkCore;

namespace AiCompanion.Api.Data;

internal static class DbSchemaUpgrader
{
    public static void EnsureMemorySchema(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS memory_consents (
                UserId TEXT NOT NULL PRIMARY KEY,
                IsEnabled INTEGER NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS memory_entries (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                Scope TEXT NOT NULL,
                Content TEXT NOT NULL,
                IsApproved INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE INDEX IF NOT EXISTS ix_memory_entries_user_approved_scope
            ON memory_entries (UserId, IsApproved, Scope);
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS memory_audit_events (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                UserId TEXT NOT NULL,
                Action TEXT NOT NULL,
                MemoryEntryId INTEGER NULL,
                Details TEXT NULL,
                CreatedAt TEXT NOT NULL
            );
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE INDEX IF NOT EXISTS ix_memory_audit_events_user_id
            ON memory_audit_events (UserId);
            """);

        db.Database.ExecuteSqlRaw("""
            CREATE INDEX IF NOT EXISTS ix_memory_audit_events_created_at
            ON memory_audit_events (CreatedAt);
            """);
    }
}
