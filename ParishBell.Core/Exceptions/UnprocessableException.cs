using ParishBell.Core.Enums;

namespace ParishBell.Core.Exceptions;

public class UnprocessableException(string messageCode) : ParishBellException(422, messageCode);