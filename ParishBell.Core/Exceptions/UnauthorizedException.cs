using ParishBell.Core.Enums;

namespace ParishBell.Core.Exceptions;

public class UnauthorizedException(string messageCode) : ParishBellException(401, messageCode);