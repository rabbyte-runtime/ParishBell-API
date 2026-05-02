using ParishBell.Core.Enums;

namespace ParishBell.Core.Exceptions;

public class ConflictException(string messageCode, MessageType messageType, string message) : ParishBellException(409, messageCode, messageType, message) { }