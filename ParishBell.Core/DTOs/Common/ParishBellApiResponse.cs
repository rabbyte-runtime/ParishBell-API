using ParishBell.Core.Enums;

namespace ParishBell.Core.DTOs.Common;

public class ParishBellApiResponse<T>
{
    public int Status { get; set; }
    public string MessageCode { get; set; } = string.Empty;
    public MessageType MessageType { get; set; }
    public string Message { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public T? ResponseData { get; set; }
}