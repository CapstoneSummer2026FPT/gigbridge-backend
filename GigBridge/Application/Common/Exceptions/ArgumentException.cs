namespace Application.Common.Exceptions;

public class ArgumentException : Exception
{
    public ArgumentException()
        : base()
    {
    }

    public ArgumentException(string message)
        : base(message)
    {
    }

    public ArgumentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ArgumentException(string message, string paramName)
        : base($"{message} (Parameter '{paramName}')")
    {
        ParamName = paramName;
    }

    public string? ParamName { get; }
}
