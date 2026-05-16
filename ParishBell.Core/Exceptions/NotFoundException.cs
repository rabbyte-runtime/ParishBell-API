namespace ParishBell.Core.Exceptions;

public class NotFoundException(string messageCode) : ParishBellException(404, messageCode);