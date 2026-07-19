using System.ComponentModel.DataAnnotations;
using ParishBell.Core.Enums;

namespace ParishBell.Core.DTOs.User;

public class RegisterDeviceTokenRequestDto
{
    // NOTE: The push token (FCM for Android, APNs for iOS). Globally unique — drives the idempotent upsert.
    [Required(ErrorMessage = "PB-58")]
    public string Token { get; set; } = string.Empty;

    // NOTE: Numeric platform value — 1=iOS, 2=Android. Default 0 is undefined and fails EnumDataType.
    [EnumDataType(typeof(DevicePlatform), ErrorMessage = "PB-59")]
    public DevicePlatform Platform { get; set; }

    // NOTE: Optional client app version, e.g. "1.0.0"
    [StringLength(20, ErrorMessage = "PB-60")]
    public string? AppVersion { get; set; }
}
