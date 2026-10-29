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
    public DbSet<GameInstanceEvent> GameInstanceEvents => Set<GameInstanceEvent>();
    public DbSet<GameInstanceEventResponse> GameInstanceEventResponses => Set<GameInstanceEventResponse>();
    public DbSet<ChatGroup> ChatGroups => Set<ChatGroup>();
    public DbSet<ChatGroupMember> ChatGroupMembers => Set<ChatGroupMember>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AdminChatRead> AdminChatReads => Set<AdminChatRead>();
    public DbSet<ChatUserRead> ChatUserReads => Set<ChatUserRead>();
    public DbSet<ProjectInfoPage> ProjectInfoPages => Set<ProjectInfoPage>();
    public DbSet<TeamNameSuggestion> TeamNameSuggestions => Set<TeamNameSuggestion>();
    public DbSet<TeamNameVote> TeamNameVotes => Set<TeamNameVote>();
    public DbSet<SongWish> SongWishes => Set<SongWish>();
    public DbSet<SongWishVote> SongWishVotes => Set<SongWishVote>();
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
            .HasValue<RankingSlide>("ranking")
            .HasValue<EstimationSlide>("estimation")
            .HasValue<BuzzerSlide>("buzzer");
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
        gameInstances.Property(gi => gi.AccessCode).HasMaxLength(6).IsRequired();
        gameInstances.HasIndex(gi => gi.AccessCode);
        gameInstances.HasIndex(gi => gi.ProjektId).IsUnique(); // At most ONE session per project
        gameInstances.HasOne(gi => gi.Projekt)
            .WithMany()
            .HasForeignKey(gi => gi.ProjektId)
            .OnDelete(DeleteBehavior.Cascade);

        var gameInstanceEvents = modelBuilder.Entity<GameInstanceEvent>();
        gameInstanceEvents.Property(gie => gie.Mode).HasMaxLength(16).HasDefaultValue("active");
        gameInstanceEvents.Property(gie => gie.Status).HasMaxLength(16).HasDefaultValue("active");
        gameInstanceEvents.Property(gie => gie.NavigationMode).HasMaxLength(32).HasDefaultValue("host_controlled");
        gameInstanceEvents.Property(gie => gie.SlidesJson).HasColumnType("jsonb").IsRequired();
        gameInstanceEvents.HasOne(gie => gie.GameInstance)
            .WithMany(gi => gi.Events)
            .HasForeignKey(gie => gie.GameInstanceId)
            .OnDelete(DeleteBehavior.Cascade);

        var gameInstanceResponses = modelBuilder.Entity<GameInstanceEventResponse>();
        gameInstanceResponses.Property(gir => gir.ResponseJson).HasColumnType("jsonb").IsRequired();
        gameInstanceResponses.HasIndex(gir => new { gir.GameInstanceEventId, gir.SlideId, gir.ParticipantId }).IsUnique();
        gameInstanceResponses.HasOne(gir => gir.GameInstanceEvent)
            .WithMany(gie => gie.Responses)
            .HasForeignKey(gir => gir.GameInstanceEventId)
            .OnDelete(DeleteBehavior.Cascade);
        gameInstanceResponses.HasOne(gir => gir.Participant)
            .WithMany()
            .HasForeignKey(gir => gir.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        var chatGroups = modelBuilder.Entity<ChatGroup>();
        chatGroups.Property(cg => cg.Type).HasMaxLength(32).HasDefaultValue("global");
        chatGroups.HasOne(cg => cg.Projekt)
            .WithMany()
            .HasForeignKey(cg => cg.ProjektId)
            .OnDelete(DeleteBehavior.Cascade);

        var chatGroupMembers = modelBuilder.Entity<ChatGroupMember>();
        chatGroupMembers.HasIndex(cgm => new { cgm.ChatGroupId, cgm.ParticipantId }).IsUnique();
        chatGroupMembers.HasOne(cgm => cgm.ChatGroup)
            .WithMany(cg => cg.Members)
            .HasForeignKey(cgm => cgm.ChatGroupId)
            .OnDelete(DeleteBehavior.Cascade);
        chatGroupMembers.HasOne(cgm => cgm.Participant)
            .WithMany()
            .HasForeignKey(cgm => cgm.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        var chatMessages = modelBuilder.Entity<ChatMessage>();
        chatMessages.Property(cm => cm.MessageText).HasMaxLength(1000).IsRequired();
        chatMessages.HasOne(cm => cm.ChatGroup)
            .WithMany(cg => cg.Messages)
            .HasForeignKey(cm => cm.ChatGroupId)
            .OnDelete(DeleteBehavior.Cascade);
        chatMessages.HasOne(cm => cm.SenderParticipant)
            .WithMany()
            .HasForeignKey(cm => cm.SenderParticipantId)
            .OnDelete(DeleteBehavior.SetNull);
        chatMessages.HasOne(cm => cm.SenderAdmin)
            .WithMany()
            .HasForeignKey(cm => cm.SenderAdminId)
            .OnDelete(DeleteBehavior.SetNull);

        var adminChatReads = modelBuilder.Entity<AdminChatRead>();
        adminChatReads.HasIndex(acr => new { acr.ChatGroupId, acr.ProjektAdminId }).IsUnique();
        adminChatReads.HasOne(acr => acr.ChatGroup)
            .WithMany()
            .HasForeignKey(acr => acr.ChatGroupId)
            .OnDelete(DeleteBehavior.Cascade);
        adminChatReads.HasOne(acr => acr.ProjektAdmin)
            .WithMany()
            .HasForeignKey(acr => acr.ProjektAdminId)
            .OnDelete(DeleteBehavior.Cascade);

        var chatUserReads = modelBuilder.Entity<ChatUserRead>();
        chatUserReads.HasIndex(cur => new { cur.ChatGroupId, cur.ParticipantId });
        chatUserReads.HasIndex(cur => new { cur.ChatGroupId, cur.ProjektAdminId });
        chatUserReads.HasOne(cur => cur.ChatGroup)
            .WithMany()
            .HasForeignKey(cur => cur.ChatGroupId)
            .OnDelete(DeleteBehavior.Cascade);
        chatUserReads.HasOne(cur => cur.Participant)
            .WithMany()
            .HasForeignKey(cur => cur.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
        chatUserReads.HasOne(cur => cur.ProjektAdmin)
            .WithMany()
            .HasForeignKey(cur => cur.ProjektAdminId)
            .OnDelete(DeleteBehavior.Cascade);

        var infoPages = modelBuilder.Entity<ProjectInfoPage>();
        infoPages.HasOne(ip => ip.Projekt)
            .WithMany()
            .HasForeignKey(ip => ip.ProjektId)
            .OnDelete(DeleteBehavior.Cascade);

        var suggestions = modelBuilder.Entity<TeamNameSuggestion>();
        suggestions.HasOne(s => s.Team)
            .WithMany()
            .HasForeignKey(s => s.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
        suggestions.HasOne(s => s.Participant)
            .WithMany()
            .HasForeignKey(s => s.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        var teamVotes = modelBuilder.Entity<TeamNameVote>();
        teamVotes.HasIndex(v => new { v.SuggestionId, v.ParticipantId }).IsUnique();
        teamVotes.HasOne(v => v.Suggestion)
            .WithMany(s => s.Votes)
            .HasForeignKey(v => v.SuggestionId)
            .OnDelete(DeleteBehavior.Cascade);

        var songWishes = modelBuilder.Entity<SongWish>();
        songWishes.HasOne(sw => sw.Projekt)
            .WithMany()
            .HasForeignKey(sw => sw.ProjektId)
            .OnDelete(DeleteBehavior.Cascade);
        songWishes.HasOne(sw => sw.Participant)
            .WithMany()
            .HasForeignKey(sw => sw.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        var songVotes = modelBuilder.Entity<SongWishVote>();
        songVotes.HasIndex(v => new { v.SongWishId, v.ParticipantId }).IsUnique();
        songVotes.HasOne(v => v.SongWish)
            .WithMany(sw => sw.Votes)
            .HasForeignKey(v => v.SongWishId)
            .OnDelete(DeleteBehavior.Cascade);

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
