using ParishBell.Core.Enums;

namespace ParishBell.Core.Exceptions;

public class BadRequestException(string messageCode, MessageType messageType, string message) : ParishBellException(400, messageCode, messageType, message) { }