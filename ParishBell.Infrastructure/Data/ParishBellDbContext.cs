using Microsoft.EntityFrameworkCore;
using ParishBell.Core.Entities;

namespace ParishBell.Infrastructure.Data;

public partial class ParishBellDbContext : DbContext
{
    public ParishBellDbContext(DbContextOptions<ParishBellDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdminUser> AdminUsers { get; set; }

    public virtual DbSet<Announcement> Announcements { get; set; }

    public virtual DbSet<AnnouncementTranslation> AnnouncementTranslations { get; set; }

    public virtual DbSet<AppUser> AppUsers { get; set; }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<EventImage> EventImages { get; set; }

    public virtual DbSet<EventTranslation> EventTranslations { get; set; }

    public virtual DbSet<Language> Languages { get; set; }

    public virtual DbSet<LiturgicalCalendar> LiturgicalCalendars { get; set; }

    public virtual DbSet<LiturgicalCalendarTranslation> LiturgicalCalendarTranslations { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<LocationFeastDay> LocationFeastDays { get; set; }

    public virtual DbSet<LocationImage> LocationImages { get; set; }

    public virtual DbSet<LocationTranslation> LocationTranslations { get; set; }

    public virtual DbSet<LocationType> LocationTypes { get; set; }

    public virtual DbSet<LocationTypeTranslation> LocationTypeTranslations { get; set; }

    public virtual DbSet<MassSchedule> MassSchedules { get; set; }

    public virtual DbSet<MassScheduleTranslation> MassScheduleTranslations { get; set; }

    public virtual DbSet<NotificationsLog> NotificationsLogs { get; set; }

    public virtual DbSet<OnboardingRequest> OnboardingRequests { get; set; }

    public virtual DbSet<UserDevice> UserDevices { get; set; }

    public virtual DbSet<UserFollowedLocation> UserFollowedLocations { get; set; }

    public virtual DbSet<UserMassReminder> UserMassReminders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");

        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.HasKey(e => e.AdminId).HasName("pk_admin_users");

            entity.ToTable("admin_users", tb => tb.HasComment("Parish admins and Super Admin. Separate from app users."));

            entity.Property(e => e.AdminId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasComment("BCrypt hashed. Raw password never stored.");
            entity.Property(e => e.Role)
                .HasConversion<short>()
                .HasDefaultValue((short)2)
                .HasComment("1=SuperAdmin, 2=Admin. SuperAdmin has NULL location_id.");

            entity.HasOne(d => d.Location).WithMany(p => p.AdminUsers)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_au_location");
        });

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.HasKey(e => e.AnnouncementId).HasName("pk_announcements");

            entity.ToTable("announcements", tb => tb.HasComment("Time-limited audio/video parish channel announcements for joined members only."));

            entity.Property(e => e.AnnouncementId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.ExpiresAt).HasComment("Between created_at+1hr and created_at+7days. Background job sets is_active=FALSE on expiry.");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MediaType).HasConversion<short>().HasComment("1=Audio, 2=Video.");
            entity.Property(e => e.MediaUrl).HasComment("Azure Blob Storage SAS URL for the audio/video file.");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Announcements)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_ann_created_by");

            entity.HasOne(d => d.Location).WithMany(p => p.Announcements).HasConstraintName("fk_ann_location");
        });

        modelBuilder.Entity<AnnouncementTranslation>(entity =>
        {
            entity.HasKey(e => e.TranslationId).HasName("pk_announcement_translations");

            entity.ToTable("announcement_translations", tb => tb.HasComment("Optional multilingual captions for announcements."));

            entity.Property(e => e.TranslationId).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.Announcement).WithMany(p => p.AnnouncementTranslations).HasConstraintName("fk_at_announcement");

            entity.HasOne(d => d.Language).WithMany(p => p.AnnouncementTranslations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_at_language");
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("pk_app_users");

            entity.ToTable("app_users", tb => tb.HasComment("Registered ParishBell mobile app users."));

            entity.Property(e => e.UserId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.AuthProvider)
                .HasConversion<short>()
                .HasDefaultValue((short)1)
                .HasComment("1=Email, 2=Google, 3=Apple.");
            entity.Property(e => e.AuthProviderId).HasComment("Google/Apple subject ID. NULL for email auth users.");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasComment("BCrypt hashed. NULL for social auth users.");

            entity.HasOne(d => d.PreferredLanguageNavigation).WithMany(p => p.AppUsers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_appuser_language");
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.EventId).HasName("pk_events");

            entity.ToTable("events", tb => tb.HasComment("Parish events created and published by admins."));

            entity.Property(e => e.EventId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasComment("Soft delete. FALSE hides the event without deleting data.");
            entity.Property(e => e.IsPublished).HasComment("FALSE=draft. TRUE=visible to users. Publishing triggers push notifications.");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.Events)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_ev_created_by");

            entity.HasOne(d => d.Location).WithMany(p => p.Events).HasConstraintName("fk_ev_location");
        });

        modelBuilder.Entity<EventImage>(entity =>
        {
            entity.HasKey(e => e.EventImageId).HasName("pk_event_images");

            entity.ToTable("event_images", tb => tb.HasComment("Photos for past events only. API enforces event_date must be in the past."));

            entity.Property(e => e.EventImageId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ImageUrl).HasComment("Azure Blob Storage URL. Compressed JPEG, max 1200x1200px.");
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Event).WithMany(p => p.EventImages).HasConstraintName("fk_ei_event");

            entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.EventImages)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_ei_uploaded_by");
        });

        modelBuilder.Entity<EventTranslation>(entity =>
        {
            entity.HasKey(e => e.TranslationId).HasName("pk_event_translations");

            entity.ToTable("event_translations", tb => tb.HasComment("Multilingual titles and descriptions for each event."));

            entity.Property(e => e.TranslationId).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.Event).WithMany(p => p.EventTranslations).HasConstraintName("fk_et_event");

            entity.HasOne(d => d.Language).WithMany(p => p.EventTranslations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_et_language");
        });

        modelBuilder.Entity<Language>(entity =>
        {
            entity.HasKey(e => e.LanguageId).HasName("pk_languages");

            entity.ToTable("languages", tb => tb.HasComment("Supported UI and content languages."));

            entity.Property(e => e.LanguageId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LanguageCode).HasComment("ISO 639-1 code: en, si, ta");
            entity.Property(e => e.NativeName).HasComment("Name in the language itself - e.g. සිංහල, தமிழ்");
        });

        modelBuilder.Entity<LiturgicalCalendar>(entity =>
        {
            entity.HasKey(e => e.CalendarId).HasName("pk_liturgical_calendar");

            entity.ToTable("liturgical_calendar", tb => tb.HasComment("Shared Catholic calendar of feast days, Holy Days, seasons, novenas."));

            entity.Property(e => e.CalendarId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsHolyDay).HasComment("TRUE for Holy Days of Obligation.");
            entity.Property(e => e.IsRecurringAnnually)
                .HasDefaultValue(true)
                .HasComment("TRUE=fixed annual (month+day). FALSE=one-off (specific_date).");
        });

        modelBuilder.Entity<LiturgicalCalendarTranslation>(entity =>
        {
            entity.HasKey(e => e.TranslationId).HasName("pk_liturgical_translations");

            entity.ToTable("liturgical_calendar_translations", tb => tb.HasComment("Multilingual titles and descriptions for each liturgical entry."));

            entity.Property(e => e.TranslationId).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.Calendar).WithMany(p => p.LiturgicalCalendarTranslations).HasConstraintName("fk_lct_calendar");

            entity.HasOne(d => d.Language).WithMany(p => p.LiturgicalCalendarTranslations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_lct_language");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.LocationId).HasName("pk_locations");

            entity.ToTable("locations", tb => tb.HasComment("All Catholic places of worship registered in ParishBell."));

            entity.Property(e => e.LocationId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsApproved).HasComment("Set TRUE by Super Admin. Location visible in app only when TRUE.");
            entity.Property(e => e.IsRejected).HasComment("Set TRUE on rejection. Location never shown in app.");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.LocationApprovedByNavigations)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_locations_approved_by");

            entity.HasOne(d => d.LocationType).WithMany(p => p.Locations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_locations_type");

            entity.HasOne(d => d.RejectedByNavigation).WithMany(p => p.LocationRejectedByNavigations)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_locations_rejected_by");
        });

        modelBuilder.Entity<LocationFeastDay>(entity =>
        {
            entity.HasKey(e => e.LocationFeastDayId).HasName("pk_location_feast_days");

            entity.ToTable("location_feast_days", tb => tb.HasComment("Parish-pinned feast days - e.g. patron saint day with procession."));

            entity.Property(e => e.LocationFeastDayId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsHighlighted)
                .HasDefaultValue(true)
                .HasComment("Shown as a special occasion in the app for this parish.");

            entity.HasOne(d => d.Calendar).WithMany(p => p.LocationFeastDays).HasConstraintName("fk_lfd_calendar");

            entity.HasOne(d => d.Location).WithMany(p => p.LocationFeastDays).HasConstraintName("fk_lfd_location");
        });

        modelBuilder.Entity<LocationImage>(entity =>
        {
            entity.HasKey(e => e.ImageId).HasName("pk_location_images");

            entity.ToTable("location_images", tb => tb.HasComment("Profile and gallery images. Stored in Azure Blob Storage."));

            entity.Property(e => e.ImageId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ImageUrl).HasComment("Azure Blob Storage public URL. Compressed JPEG, max 1920x1080px.");
            entity.Property(e => e.IsPrimary).HasComment("Only one image per location should be TRUE.");
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Location).WithMany(p => p.LocationImages).HasConstraintName("fk_li_location");

            entity.HasOne(d => d.UploadedByNavigation).WithMany(p => p.LocationImages)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_li_uploaded_by");
        });

        modelBuilder.Entity<LocationTranslation>(entity =>
        {
            entity.HasKey(e => e.TranslationId).HasName("pk_location_translations");

            entity.ToTable("location_translations", tb => tb.HasComment("Multilingual names, descriptions, and addresses for each location."));

            entity.Property(e => e.TranslationId).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.Language).WithMany(p => p.LocationTranslations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_lt_language");

            entity.HasOne(d => d.Location).WithMany(p => p.LocationTranslations).HasConstraintName("fk_lt_location");
        });

        modelBuilder.Entity<LocationType>(entity =>
        {
            entity.HasKey(e => e.LocationTypeId).HasName("pk_location_types");

            entity.ToTable("location_types", tb => tb.HasComment("Categories of Catholic locations - Church, Cathedral, Shrine, etc."));

            entity.Property(e => e.LocationTypeId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<LocationTypeTranslation>(entity =>
        {
            entity.HasKey(e => e.TranslationId).HasName("pk_location_type_translations");

            entity.ToTable("location_type_translations", tb => tb.HasComment("Translated names for each location type per language."));

            entity.Property(e => e.TranslationId).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.Language).WithMany(p => p.LocationTypeTranslations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_ltt_language");

            entity.HasOne(d => d.LocationType).WithMany(p => p.LocationTypeTranslations).HasConstraintName("fk_ltt_location_type");
        });

        modelBuilder.Entity<MassSchedule>(entity =>
        {
            entity.HasKey(e => e.ScheduleId).HasName("pk_mass_schedules");

            entity.ToTable("mass_schedules", tb => tb.HasComment("Weekly recurring and special one-off mass times per location."));

            entity.Property(e => e.ScheduleId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.DayOfWeek).HasComment("0=Sunday, 1=Monday, 2=Tuesday, 3=Wednesday, 4=Thursday, 5=Friday, 6=Saturday.");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsSpecial).HasComment("TRUE for seasonal/one-off masses. Requires valid_from and valid_to.");

            entity.HasOne(d => d.Location).WithMany(p => p.MassSchedules).HasConstraintName("fk_ms_location");
        });

        modelBuilder.Entity<MassScheduleTranslation>(entity =>
        {
            entity.HasKey(e => e.TranslationId).HasName("pk_mass_schedule_translations");

            entity.ToTable("mass_schedule_translations", tb => tb.HasComment("Multilingual labels for mass schedule entries."));

            entity.Property(e => e.TranslationId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Label).HasComment("e.g. Sunday Family Mass, Sinhala Mass, Confession.");

            entity.HasOne(d => d.Language).WithMany(p => p.MassScheduleTranslations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_mst_language");

            entity.HasOne(d => d.Schedule).WithMany(p => p.MassScheduleTranslations).HasConstraintName("fk_mst_schedule");
        });

        modelBuilder.Entity<NotificationsLog>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("pk_notifications_log");

            entity.ToTable("notifications_log", tb => tb.HasComment("Log of all push notifications sent. Used for debugging and retry logic."));

            entity.Property(e => e.NotificationId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.IsSent).HasComment("FALSE if push delivery failed. Retry logic queries is_sent=FALSE.");
            entity.Property(e => e.ReferenceId).HasComment("ID of related entity — event_id, announcement_id, calendar_id, etc.");
            entity.Property(e => e.Type).HasConversion<short>().HasComment("1=Event, 2=Announcement, 3=MassReminder, 4=FeastDay, 5=System.");

            entity.HasOne(d => d.User).WithMany(p => p.NotificationsLogs).HasConstraintName("fk_nl_user");
        });

        modelBuilder.Entity<OnboardingRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("pk_onboarding_requests");

            entity.ToTable("onboarding_requests", tb => tb.HasComment("Parish self-registration requests reviewed by Super Admin."));

            entity.Property(e => e.RequestId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.RejectionReason).HasComment("Required when status=3. Emailed to requesting admin.");
            entity.Property(e => e.Status)
                .HasConversion<short>()
                .HasDefaultValue((short)1)
                .HasComment("1=Pending, 2=Approved, 3=Rejected. Rejected requests never shown again.");
            entity.Property(e => e.SubmittedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Location).WithMany(p => p.OnboardingRequests).HasConstraintName("fk_or_location");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.OnboardingRequests)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_or_reviewed_by");
        });

        modelBuilder.Entity<UserDevice>(entity =>
        {
            entity.HasKey(e => e.DeviceId).HasName("pk_user_devices");

            entity.ToTable("user_devices", tb => tb.HasComment("Push notification tokens per user. One user may have multiple devices."));

            entity.Property(e => e.DeviceId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.LastActiveAt)
                .HasDefaultValueSql("now()")
                .HasComment("Tokens inactive > 90 days are pruned by background job.");
            entity.Property(e => e.Platform).HasConversion<short>().HasComment("1=iOS (APNs), 2=Android (FCM).");
            entity.Property(e => e.RegisteredAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.User).WithMany(p => p.UserDevices).HasConstraintName("fk_ud_user");
        });

        modelBuilder.Entity<UserFollowedLocation>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LocationId }).HasName("pk_user_followed_locations");

            entity.ToTable("user_followed_locations", tb => tb.HasComment("User joining a church. Composite PK. Drives push notification targeting."));

            entity.Property(e => e.FollowedAt).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Location).WithMany(p => p.UserFollowedLocations).HasConstraintName("fk_ufl_location");

            entity.HasOne(d => d.User).WithMany(p => p.UserFollowedLocations).HasConstraintName("fk_ufl_user");
        });

        modelBuilder.Entity<UserMassReminder>(entity =>
        {
            entity.HasKey(e => e.ReminderId).HasName("pk_user_mass_reminders");

            entity.ToTable("user_mass_reminders", tb => tb.HasComment("Personal push reminders set by users for specific mass times."));

            entity.Property(e => e.ReminderId).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.MinutesBefore)
                .HasDefaultValue(30)
                .HasComment("e.g. 15, 30, 60. Background job triggers push at (mass_time - minutes_before).");

            entity.HasOne(d => d.Schedule).WithMany(p => p.UserMassReminders).HasConstraintName("fk_umr_schedule");

            entity.HasOne(d => d.User).WithMany(p => p.UserMassReminders).HasConstraintName("fk_umr_user");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
