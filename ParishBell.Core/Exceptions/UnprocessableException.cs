using ParishBell.Core.Enums;

namespace ParishBell.Core.Exceptions;

public class UnprocessableException(string messageCode, MessageType messageType, string message) : ParishBellException(422, messageCode, messageType, message) { }