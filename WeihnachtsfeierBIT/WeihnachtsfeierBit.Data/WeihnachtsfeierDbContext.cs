using Microsoft.EntityFrameworkCore;

namespace WeihnachtsfeierBit.Data;

public sealed class WeihnachtsfeierDbContext(DbContextOptions<WeihnachtsfeierDbContext> options) : DbContext(options)
{
    public DbSet<TestEntry> TestEntries => Set<TestEntry>();
    public DbSet<Projekt> Projekte => Set<Projekt>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Teilnehmer> Teilnehmer => Set<Teilnehmer>();
    public DbSet<Show> Shows => Set<Show>();
    public DbSet<ShowProjektZuo> ShowProjektZuos => Set<ShowProjektZuo>();
    public DbSet<Slide> Slides => Set<Slide>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        UpdateSlideParameters();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        UpdateSlideParameters();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var slides = modelBuilder.Entity<Slide>();
        slides.ToTable("Slides");
        slides.Property(slide => slide.ParametersJson)
            .HasColumnName("Parameters")
            .HasColumnType("jsonb")
            .IsRequired();
        slides.HasDiscriminator<string>("SlideType")
            .HasValue<PollSlide>("poll")
            .HasValue<AnnouncementSlide>("announcement")
            .HasValue<QuizSlide>("quiz")
            .HasValue<RankingSlide>("ranking");
        slides.HasIndex(slide => new { slide.ShowId, slide.Position });

        modelBuilder.Entity<Show>()
            .HasMany(show => show.Slides)
            .WithOne(slide => slide.Show)
            .HasForeignKey(slide => slide.ShowId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private void UpdateSlideParameters()
    {
        foreach (var entry in ChangeTracker.Entries<Slide>())
        {
            entry.Entity.UpdateParametersJson();
        }
    }
}