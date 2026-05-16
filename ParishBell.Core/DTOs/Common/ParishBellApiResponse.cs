namespace ParishBell.Core.DTOs.Common;

public class ParishBellApiResponse<T>
{
    public int Status { get; set; }
    public string MessageCode { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public T? ResponseData { get; set; }
}