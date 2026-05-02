using ParishBell.Core.Enums;

namespace ParishBell.Core.Exceptions;

public class ForbiddenException(string messageCode) : ParishBellException(403, messageCode);