using ParishBell.Core.Enums;

namespace ParishBell.Core.Exceptions;

public class NotFoundException(string messageCode, MessageType messageType, string message) : ParishBellException(404, messageCode, messageType, message) { }