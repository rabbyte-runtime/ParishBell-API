namespace ParishBell.Core.Exceptions;

public class ConflictException(string messageCode) : ParishBellException(409, messageCode);