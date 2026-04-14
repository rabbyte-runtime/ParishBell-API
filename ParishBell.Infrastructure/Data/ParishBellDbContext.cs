using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using ParishBell.Infrastructure;

namespace ParishBell.Infrastructure.Data;

public partial class ParishBellDbContext : DbContext
{
    public ParishBellDbContext(DbContextOptions<ParishBellDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Adminuser> Adminusers { get; set; }

    public virtual DbSet<Announcement> Announcements { get; set; }

    public virtual DbSet<Announcementtranslation> Announcementtranslations { get; set; }

    public virtual DbSet<Appuser> Appusers { get; set; }

    public virtual DbSet<Event> Events { get; set; }

    public virtual DbSet<Eventimage> Eventimages { get; set; }

    public virtual DbSet<Eventtranslation> Eventtranslations { get; set; }

    public virtual DbSet<Language> Languages { get; set; }

    public virtual DbSet<Liturgicalcalendar> Liturgicalcalendars { get; set; }

    public virtual DbSet<Liturgicalcalendartranslation> Liturgicalcalendartranslations { get; set; }

    public virtual DbSet<Location> Locations { get; set; }

    public virtual DbSet<Locationfeastday> Locationfeastdays { get; set; }

    public virtual DbSet<Locationimage> Locationimages { get; set; }

    public virtual DbSet<Locationtranslation> Locationtranslations { get; set; }

    public virtual DbSet<Locationtype> Locationtypes { get; set; }

    public virtual DbSet<Locationtypetranslation> Locationtypetranslations { get; set; }

    public virtual DbSet<Massschedule> Massschedules { get; set; }

    public virtual DbSet<Massscheduletranslation> Massscheduletranslations { get; set; }

    public virtual DbSet<Notificationslog> Notificationslogs { get; set; }

    public virtual DbSet<Onboardingrequest> Onboardingrequests { get; set; }

    public virtual DbSet<Userdevice> Userdevices { get; set; }

    public virtual DbSet<Userfollowedlocation> Userfollowedlocations { get; set; }

    public virtual DbSet<Usermassreminder> Usermassreminders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasPostgresEnum("adminrole", new[] { "SuperAdmin", "Admin" })
            .HasPostgresEnum("authprovider", new[] { "Email", "Google", "Apple" })
            .HasPostgresEnum("deviceplatform", new[] { "iOS", "Android" })
            .HasPostgresEnum("mediatype", new[] { "Audio", "Video" })
            .HasPostgresEnum("notificationtype", new[] { "Event", "Announcement", "MassReminder", "FeastDay", "System" })
            .HasPostgresEnum("onboardingstatus", new[] { "Pending", "Approved", "Rejected" });

        modelBuilder.Entity<Adminuser>(entity =>
        {
            entity.HasKey(e => e.Adminid).HasName("pk_admin_users");

            entity.ToTable("adminusers", tb => tb.HasComment("Parish admins and Super Admin. Separate from app users."));

            entity.Property(e => e.Adminid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Createdat).HasDefaultValueSql("now()");
            entity.Property(e => e.Isactive).HasDefaultValue(true);
            entity.Property(e => e.Locationid).HasComment("Null for SuperAdmin. Required for parish Admin role.");
            entity.Property(e => e.Passwordhash).HasComment("BCrypt hashed. Raw password never stored.");

            entity.HasOne(d => d.Location).WithMany(p => p.Adminusers)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_au_location");
        });

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.HasKey(e => e.Announcementid).HasName("pk_announcements");

            entity.ToTable("announcements", tb => tb.HasComment("Time-limited audio/video parish channel announcements for joined members only."));

            entity.Property(e => e.Announcementid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Createdat).HasDefaultValueSql("now()");
            entity.Property(e => e.Expiresat).HasComment("Between CreatedAt+1hr and CreatedAt+7days. Background job sets IsActive=FALSE on expiry.");
            entity.Property(e => e.Isactive).HasDefaultValue(true);
            entity.Property(e => e.Mediaurl).HasComment("Azure Blob Storage SAS URL for the audio/video file.");

            entity.HasOne(d => d.CreatedbyNavigation).WithMany(p => p.Announcements)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_ann_created_by");

            entity.HasOne(d => d.Location).WithMany(p => p.Announcements).HasConstraintName("fk_ann_location");
        });

        modelBuilder.Entity<Announcementtranslation>(entity =>
        {
            entity.HasKey(e => e.Translationid).HasName("pk_announcement_translations");

            entity.ToTable("announcementtranslations", tb => tb.HasComment("Optional multilingual captions for announcements."));

            entity.Property(e => e.Translationid).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.Announcement).WithMany(p => p.Announcementtranslations).HasConstraintName("fk_at_announcement");

            entity.HasOne(d => d.Language).WithMany(p => p.Announcementtranslations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_at_language");
        });

        modelBuilder.Entity<Appuser>(entity =>
        {
            entity.HasKey(e => e.Userid).HasName("pk_app_users");

            entity.ToTable("appusers", tb => tb.HasComment("Registered ParishBell mobile app users."));

            entity.Property(e => e.Userid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Authproviderid).HasComment("Google/Apple subject ID. Null for email auth users.");
            entity.Property(e => e.Createdat).HasDefaultValueSql("now()");
            entity.Property(e => e.Isactive).HasDefaultValue(true);
            entity.Property(e => e.Passwordhash).HasComment("BCrypt hashed. Null for Google/Apple social auth users.");

            entity.HasOne(d => d.PreferredlanguageNavigation).WithMany(p => p.Appusers)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_appuser_language");
        });

        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Eventid).HasName("pk_events");

            entity.ToTable("events", tb => tb.HasComment("Parish events created and published by admins."));

            entity.Property(e => e.Eventid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Createdat).HasDefaultValueSql("now()");
            entity.Property(e => e.Isactive)
                .HasDefaultValue(true)
                .HasComment("Soft delete. False hides the event without deleting data.");
            entity.Property(e => e.Ispublished).HasComment("False = draft. True = visible to users. Publishing triggers push notifications.");

            entity.HasOne(d => d.CreatedbyNavigation).WithMany(p => p.Events)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_ev_created_by");

            entity.HasOne(d => d.Location).WithMany(p => p.Events).HasConstraintName("fk_ev_location");
        });

        modelBuilder.Entity<Eventimage>(entity =>
        {
            entity.HasKey(e => e.Eventimageid).HasName("pk_event_images");

            entity.ToTable("eventimages", tb => tb.HasComment("Photos for past events only. API enforces EventDate must be in the past."));

            entity.Property(e => e.Eventimageid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Imageurl).HasComment("Azure Blob Storage URL. Compressed JPEG, max 1200x1200px.");
            entity.Property(e => e.Uploadedat).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Event).WithMany(p => p.Eventimages).HasConstraintName("fk_ei_event");

            entity.HasOne(d => d.UploadedbyNavigation).WithMany(p => p.Eventimages)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_ei_uploaded_by");
        });

        modelBuilder.Entity<Eventtranslation>(entity =>
        {
            entity.HasKey(e => e.Translationid).HasName("pk_event_translations");

            entity.ToTable("eventtranslations", tb => tb.HasComment("Multilingual titles and descriptions for each event."));

            entity.Property(e => e.Translationid).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.Event).WithMany(p => p.Eventtranslations).HasConstraintName("fk_et_event");

            entity.HasOne(d => d.Language).WithMany(p => p.Eventtranslations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_et_language");
        });

        modelBuilder.Entity<Language>(entity =>
        {
            entity.HasKey(e => e.Languageid).HasName("pk_languages");

            entity.ToTable("languages", tb => tb.HasComment("Supported UI and content languages."));

            entity.Property(e => e.Languageid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Isactive).HasDefaultValue(true);
            entity.Property(e => e.Languagecode).HasComment("ISO 639-1 code: en, si, ta");
            entity.Property(e => e.Nativename).HasComment("Name in the language itself.");
        });

        modelBuilder.Entity<Liturgicalcalendar>(entity =>
        {
            entity.HasKey(e => e.Calendarid).HasName("pk_liturgical_calendar");

            entity.ToTable("liturgicalcalendar", tb => tb.HasComment("Shared calendar of feast days, Holy Days, seasons, novenas."));

            entity.Property(e => e.Calendarid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Createdat).HasDefaultValueSql("now()");
            entity.Property(e => e.Isholyday).HasComment("True for Holy Days of Obligation.");
            entity.Property(e => e.Isrecurringannually)
                .HasDefaultValue(true)
                .HasComment("True = fixed annual (Month + Day). False = one-off (SpecificDate).");
        });

        modelBuilder.Entity<Liturgicalcalendartranslation>(entity =>
        {
            entity.HasKey(e => e.Translationid).HasName("pk_liturgical_translations");

            entity.ToTable("liturgicalcalendartranslations", tb => tb.HasComment("Multilingual titles and descriptions for each liturgical entry."));

            entity.Property(e => e.Translationid).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.Calendar).WithMany(p => p.Liturgicalcalendartranslations).HasConstraintName("fk_lct_calendar");

            entity.HasOne(d => d.Language).WithMany(p => p.Liturgicalcalendartranslations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_lct_language");
        });

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasKey(e => e.Locationid).HasName("pk_locations");

            entity.ToTable("locations", tb => tb.HasComment("All places of worship registered in ParishBell."));

            entity.Property(e => e.Locationid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Createdat).HasDefaultValueSql("now()");
            entity.Property(e => e.Isapproved).HasComment("Set TRUE by Super Admin. Location visible in app only when TRUE.");
            entity.Property(e => e.Isrejected).HasComment("Set true on rejection. Location never shown in app.");

            entity.HasOne(d => d.ApprovedbyNavigation).WithMany(p => p.LocationApprovedbyNavigations)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_locations_approved_by");

            entity.HasOne(d => d.Locationtype).WithMany(p => p.Locations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_locations_type");

            entity.HasOne(d => d.RejectedbyNavigation).WithMany(p => p.LocationRejectedbyNavigations)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_locations_rejected_by");
        });

        modelBuilder.Entity<Locationfeastday>(entity =>
        {
            entity.HasKey(e => e.Locationfeastdayid).HasName("pk_location_feast_days");

            entity.ToTable("locationfeastdays", tb => tb.HasComment("Parish-pinned feast days - ex: patron saint day with procession."));

            entity.Property(e => e.Locationfeastdayid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Createdat).HasDefaultValueSql("now()");
            entity.Property(e => e.Ishighlighted)
                .HasDefaultValue(true)
                .HasComment("Shown as a special occasion in the app for this parish.");

            entity.HasOne(d => d.Calendar).WithMany(p => p.Locationfeastdays).HasConstraintName("fk_lfd_calendar");

            entity.HasOne(d => d.Location).WithMany(p => p.Locationfeastdays).HasConstraintName("fk_lfd_location");
        });

        modelBuilder.Entity<Locationimage>(entity =>
        {
            entity.HasKey(e => e.Imageid).HasName("pk_location_images");

            entity.ToTable("locationimages", tb => tb.HasComment("Profile and gallery images for each location. Stored in Azure Blob Storage."));

            entity.Property(e => e.Imageid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Imageurl).HasComment("Azure Blob Storage public URL. Compressed JPEG, max 1920x1080px.");
            entity.Property(e => e.Isprimary).HasComment("Only one image per location should be true.");
            entity.Property(e => e.Uploadedat).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Location).WithMany(p => p.Locationimages).HasConstraintName("fk_li_location");

            entity.HasOne(d => d.UploadedbyNavigation).WithMany(p => p.Locationimages)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_li_uploaded_by");
        });

        modelBuilder.Entity<Locationtranslation>(entity =>
        {
            entity.HasKey(e => e.Translationid).HasName("pk_location_translations");

            entity.ToTable("locationtranslations", tb => tb.HasComment("Multilingual names, descriptions, and addresses for each location."));

            entity.Property(e => e.Translationid).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.Language).WithMany(p => p.Locationtranslations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_lt_language");

            entity.HasOne(d => d.Location).WithMany(p => p.Locationtranslations).HasConstraintName("fk_lt_location");
        });

        modelBuilder.Entity<Locationtype>(entity =>
        {
            entity.HasKey(e => e.Locationtypeid).HasName("pk_location_types");

            entity.ToTable("locationtypes", tb => tb.HasComment("Categories of locations — Church, Cathedral, Shrine, etc."));

            entity.Property(e => e.Locationtypeid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Createdat).HasDefaultValueSql("now()");
            entity.Property(e => e.Isactive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Locationtypetranslation>(entity =>
        {
            entity.HasKey(e => e.Translationid).HasName("pk_location_type_translations");

            entity.ToTable("locationtypetranslations", tb => tb.HasComment("Translated names for each location type per language."));

            entity.Property(e => e.Translationid).HasDefaultValueSql("gen_random_uuid()");

            entity.HasOne(d => d.Language).WithMany(p => p.Locationtypetranslations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_ltt_language");

            entity.HasOne(d => d.Locationtype).WithMany(p => p.Locationtypetranslations).HasConstraintName("fk_ltt_location_type");
        });

        modelBuilder.Entity<Massschedule>(entity =>
        {
            entity.HasKey(e => e.Scheduleid).HasName("pk_mass_schedules");

            entity.ToTable("massschedules", tb => tb.HasComment("Weekly recurring and special one-off mass times per location."));

            entity.Property(e => e.Scheduleid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Createdat).HasDefaultValueSql("now()");
            entity.Property(e => e.Dayofweek).HasComment("0=Sunday, 1=Monday, 2=Tuesday, 3=Wednesday, 4=Thursday, 5=Friday, 6=Saturday.");
            entity.Property(e => e.Isactive).HasDefaultValue(true);
            entity.Property(e => e.Isspecial).HasComment("True for seasonal/one-off masses. Requires ValidFrom and ValidTo.");

            entity.HasOne(d => d.Location).WithMany(p => p.Massschedules).HasConstraintName("fk_ms_location");
        });

        modelBuilder.Entity<Massscheduletranslation>(entity =>
        {
            entity.HasKey(e => e.Translationid).HasName("pk_mass_schedule_translations");

            entity.ToTable("massscheduletranslations", tb => tb.HasComment("Multilingual labels for mass schedule entries."));

            entity.Property(e => e.Translationid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Label).HasComment("Ex: Sunday Family Mass, Sinhala Mass, Confession.");

            entity.HasOne(d => d.Language).WithMany(p => p.Massscheduletranslations)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_mst_language");

            entity.HasOne(d => d.Schedule).WithMany(p => p.Massscheduletranslations).HasConstraintName("fk_mst_schedule");
        });

        modelBuilder.Entity<Notificationslog>(entity =>
        {
            entity.HasKey(e => e.Notificationid).HasName("pk_notifications_log");

            entity.ToTable("notificationslog", tb => tb.HasComment("Log of all push notifications sent. Used for debugging and retry logic."));

            entity.Property(e => e.Notificationid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Issent).HasComment("False if push delivery failed. Retry logic queries IsSent = False.");
            entity.Property(e => e.Referenceid).HasComment("ID of related entity - EventId, AnnouncementId, CalendarId, etc.");

            entity.HasOne(d => d.User).WithMany(p => p.Notificationslogs).HasConstraintName("fk_nl_user");
        });

        modelBuilder.Entity<Onboardingrequest>(entity =>
        {
            entity.HasKey(e => e.Requestid).HasName("pk_onboarding_requests");

            entity.ToTable("onboardingrequests", tb => tb.HasComment("Parish self-registration requests reviewed by Super Admin."));

            entity.Property(e => e.Requestid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Rejectionreason).HasComment("Required when Status = Rejected. Emailed to requesting admin.");
            entity.Property(e => e.Submittedat).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Location).WithMany(p => p.Onboardingrequests).HasConstraintName("fk_or_location");

            entity.HasOne(d => d.ReviewedbyNavigation).WithMany(p => p.Onboardingrequests)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_or_reviewed_by");
        });

        modelBuilder.Entity<Userdevice>(entity =>
        {
            entity.HasKey(e => e.Deviceid).HasName("pk_user_devices");

            entity.ToTable("userdevices", tb => tb.HasComment("Push notification tokens per user. One user may have multiple devices."));

            entity.Property(e => e.Deviceid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Devicetoken).HasComment("APNs token (iOS) or FCM registration token (Android).");
            entity.Property(e => e.Lastactiveat)
                .HasDefaultValueSql("now()")
                .HasComment("Tokens with LastActiveAt > 90 days are pruned by background job.");
            entity.Property(e => e.Registeredat).HasDefaultValueSql("now()");

            entity.HasOne(d => d.User).WithMany(p => p.Userdevices).HasConstraintName("fk_ud_user");
        });

        modelBuilder.Entity<Userfollowedlocation>(entity =>
        {
            entity.HasKey(e => new { e.Userid, e.Locationid }).HasName("pk_user_followed_locations");

            entity.ToTable("userfollowedlocations", tb => tb.HasComment("User joining a church. Composite PK. Drives push notification targeting."));

            entity.Property(e => e.Followedat).HasDefaultValueSql("now()");

            entity.HasOne(d => d.Location).WithMany(p => p.Userfollowedlocations).HasConstraintName("fk_ufl_location");

            entity.HasOne(d => d.User).WithMany(p => p.Userfollowedlocations).HasConstraintName("fk_ufl_user");
        });

        modelBuilder.Entity<Usermassreminder>(entity =>
        {
            entity.HasKey(e => e.Reminderid).HasName("pk_user_mass_reminders");

            entity.ToTable("usermassreminders", tb => tb.HasComment("Personal push notification reminders set by users for specific mass times."));

            entity.Property(e => e.Reminderid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.Createdat).HasDefaultValueSql("now()");
            entity.Property(e => e.Isactive).HasDefaultValue(true);
            entity.Property(e => e.Minutesbefore)
                .HasDefaultValue(30)
                .HasComment("Ex: 15, 30, 60. Background job triggers push at (MassTime - MinutesBefore).");

            entity.HasOne(d => d.Schedule).WithMany(p => p.Usermassreminders).HasConstraintName("fk_umr_schedule");

            entity.HasOne(d => d.User).WithMany(p => p.Usermassreminders).HasConstraintName("fk_umr_user");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
