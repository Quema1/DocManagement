namespace DocManagement.Application.Exceptions;

public class ConcurrencyConflictException(string message, Exception? innerException = null)
    : Exception(message, innerException);
