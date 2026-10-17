using Microsoft.EntityFrameworkCore;

namespace WeihnachtsfeierBit.Data;

public sealed class WeihnachtsfeierDbContext(DbContextOptions<WeihnachtsfeierDbContext> options) : DbContext(options)
{
    public DbSet<TestEntry> TestEntries => Set<TestEntry>();
    public DbSet<Projekt> Projekte => Set<Projekt>();
    public DbSet<ProjektAdmin> ProjektAdmins => Set<ProjektAdmin>();
    public DbSet<ProjektAdminProjekt> ProjektAdminProjekte => Set<ProjektAdminProjekt>();
    public DbSet<ProjektAdminSitzung> ProjektAdminSitzungen => Set<ProjektAdminSitzung>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Teilnehmer> Teilnehmer => Set<Teilnehmer>();
    public DbSet<Sitzung> Sitzungen => Set<Sitzung>();
    public DbSet<GameInstance> GameInstances => Set<GameInstance>();
    public DbSet<TeamInstance> TeamInstances => Set<TeamInstance>();
    public DbSet<TeilnehmerInstance> TeilnehmerInstances => Set<TeilnehmerInstance>();
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

        var shows = modelBuilder.Entity<Show>();
        shows.Property(show => show.Name)
            .HasMaxLength(120)
            .HasDefaultValue("Neue Show")
            .IsRequired();
        shows
            .HasMany(show => show.Slides)
            .WithOne(slide => slide.Show)
            .HasForeignKey(slide => slide.ShowId)
            .OnDelete(DeleteBehavior.Cascade);

        var sitzungen = modelBuilder.Entity<Sitzung>();
        sitzungen.Property(sitzung => sitzung.TokenHash)
            .HasMaxLength(64)
            .IsRequired();
        sitzungen.Property(sitzung => sitzung.UserAgent)
            .HasMaxLength(512);
        sitzungen.HasIndex(sitzung => sitzung.TokenHash)
            .IsUnique();
        sitzungen.HasIndex(sitzung => new
        {
            sitzung.TeilnehmerId,
            sitzung.WiderrufenAmUtc,
            sitzung.LaeuftAbAmUtc
        });
        sitzungen.HasOne(sitzung => sitzung.Teilnehmer)
            .WithMany(teilnehmer => teilnehmer.Sitzungen)
            .HasForeignKey(sitzung => sitzung.TeilnehmerId)
            .OnDelete(DeleteBehavior.Cascade);
        sitzungen.HasOne(sitzung => sitzung.GameInstance)
            .WithMany(gi => gi.Sitzungen)
            .HasForeignKey(sitzung => sitzung.GameInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        var gameInstances = modelBuilder.Entity<GameInstance>();
        gameInstances.Property(gi => gi.AccessCode).HasMaxLength(6);
        gameInstances.HasIndex(gi => gi.AccessCode);
        gameInstances.HasIndex(gi => new { gi.ProjektId, gi.IsActive });
        gameInstances.HasOne(gi => gi.Projekt)
            .WithMany()
            .HasForeignKey(gi => gi.ProjektId)
            .OnDelete(DeleteBehavior.Cascade);

        var teamInstances = modelBuilder.Entity<TeamInstance>();
        teamInstances.HasIndex(ti => new { ti.GameInstanceId, ti.TeamId }).IsUnique();
        teamInstances.HasOne(ti => ti.GameInstance)
            .WithMany(gi => gi.TeamInstances)
            .HasForeignKey(ti => ti.GameInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
        teamInstances.HasOne(ti => ti.Team)
            .WithMany()
            .HasForeignKey(ti => ti.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        var teilnehmerInstances = modelBuilder.Entity<TeilnehmerInstance>();
        teilnehmerInstances.HasIndex(tni => new { tni.GameInstanceId, tni.TeilnehmerId }).IsUnique();
        teilnehmerInstances.HasOne(tni => tni.GameInstance)
            .WithMany(gi => gi.TeilnehmerInstances)
            .HasForeignKey(tni => tni.GameInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
        teilnehmerInstances.HasOne(tni => tni.Teilnehmer)
            .WithMany()
            .HasForeignKey(tni => tni.TeilnehmerId)
            .OnDelete(DeleteBehavior.Cascade);
        teilnehmerInstances.HasOne(tni => tni.Team)
            .WithMany()
            .HasForeignKey(tni => tni.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Teilnehmer>()
            .HasIndex(teilnehmer => new { teilnehmer.ProjektId, teilnehmer.Nummer })
            .IsUnique();

        var admins = modelBuilder.Entity<ProjektAdmin>();
        admins.Property(admin => admin.Benutzername)
            .HasMaxLength(64)
            .IsRequired();
        admins.Property(admin => admin.BenutzernameNormalisiert)
            .HasMaxLength(64)
            .IsRequired();
        admins.Property(admin => admin.PasswortHash)
            .HasMaxLength(256)
            .IsRequired();
        admins.HasIndex(admin => admin.BenutzernameNormalisiert)
            .IsUnique();

        var adminProjekte = modelBuilder.Entity<ProjektAdminProjekt>();
        adminProjekte.HasKey(membership => new { membership.ProjektAdminId, membership.ProjektId });
        adminProjekte.HasIndex(membership => membership.ProjektId);
        adminProjekte.HasOne(membership => membership.ProjektAdmin)
            .WithMany(admin => admin.Projekte)
            .HasForeignKey(membership => membership.ProjektAdminId)
            .OnDelete(DeleteBehavior.Cascade);
        adminProjekte.HasOne(membership => membership.Projekt)
            .WithMany(project => project.Admins)
            .HasForeignKey(membership => membership.ProjektId)
            .OnDelete(DeleteBehavior.Cascade);

        var adminSitzungen = modelBuilder.Entity<ProjektAdminSitzung>();
        adminSitzungen.Property(sitzung => sitzung.TokenHash)
            .HasMaxLength(64)
            .IsRequired();
        adminSitzungen.Property(sitzung => sitzung.UserAgent)
            .HasMaxLength(512);
        adminSitzungen.HasIndex(sitzung => sitzung.TokenHash)
            .IsUnique();
        adminSitzungen.HasIndex(sitzung => new
        {
            sitzung.ProjektAdminId,
            sitzung.WiderrufenAmUtc,
            sitzung.LaeuftAbAmUtc
        });
        adminSitzungen.HasOne(sitzung => sitzung.ProjektAdmin)
            .WithMany(admin => admin.Sitzungen)
            .HasForeignKey(sitzung => sitzung.ProjektAdminId)
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