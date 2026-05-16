namespace ParishBell.Core.Exceptions;

public class ParishBellException : Exception
{
    public int StatusCode { get; }
    public string MessageCode { get; }

    // NOTE: The actual message is still passed to the base Exception for now
    protected ParishBellException(int statusCode, string messageCode)
        : base(messageCode)
    {
        StatusCode = statusCode;
        MessageCode = messageCode;
    }
}