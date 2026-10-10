using Microsoft.EntityFrameworkCore;

namespace WeihnachtsfeierBit.Data;

public sealed class WeihnachtsfeierDbContext(DbContextOptions<WeihnachtsfeierDbContext> options) : DbContext(options)
{
    public DbSet<TestEntry> TestEntries => Set<TestEntry>();
}