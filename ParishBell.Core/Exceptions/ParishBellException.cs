using ParishBell.Core.Enums;

namespace ParishBell.Core.Exceptions;

public class ParishBellException : Exception
{
    public int StatusCode { get; }
    public string MessageCode { get; }
    public MessageType MessageType { get; }

    // NOTE: The actual message is still passed to the base Exception for now
    protected ParishBellException(int statusCode, string messageCode, MessageType messageType, string message)
        : base(message)
    {
        StatusCode = statusCode;
        MessageCode = messageCode;
        MessageType = messageType;
    }
}