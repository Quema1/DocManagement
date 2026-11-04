namespace DocManagement.Application.Exceptions;

public class PreviewQueueUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
