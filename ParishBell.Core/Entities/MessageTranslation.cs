using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace ParishBell.Core.Entities;

[Table("message_translations")]
[Index("LanguageId", Name = "idx_message_translations_language")]
[Index("MessageId", "LanguageId", Name = "unique_message_language", IsUnique = true)]
public partial class MessageTranslation
{
    [Key]
    [Column("translation_id")]
    public Guid TranslationId { get; set; }

    [Column("message_id")]
    public Guid MessageId { get; set; }

    [Column("language_id")]
    public Guid LanguageId { get; set; }

    [Column("message")]
    public string Message { get; set; } = null!;

    [ForeignKey("MessageId")]
    [InverseProperty("MessageTranslations")]
    public virtual Message MessageNavigation { get; set; } = null!;

    [ForeignKey("LanguageId")]
    public virtual Language? Language { get; set; }
}
