using ParishBell.Core.Enums;

namespace ParishBell.Core.Exceptions;

public class UnauthorizedException(string messageCode, MessageType messageType, string message) : ParishBellException(401, messageCode, messageType, message) { }