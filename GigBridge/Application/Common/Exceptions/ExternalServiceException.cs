namespace Application.Common.Exceptions;

public class ExternalServiceException : Exception
{
    public ExternalServiceException()
        : base()
    {
    }

    public ExternalServiceException(string message)
        : base(message)
    {
    }

    public ExternalServiceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
