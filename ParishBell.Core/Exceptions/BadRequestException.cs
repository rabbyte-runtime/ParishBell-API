using ParishBell.Core.Enums;

namespace ParishBell.Core.Exceptions;

public class BadRequestException(string messageCode) : ParishBellException(400, messageCode);