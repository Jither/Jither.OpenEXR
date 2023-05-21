namespace Jither.OpenEXR;

public abstract class EXRException : Exception
{
    public EXRException(string message) : base(message)
    {
    }

    protected EXRException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class EXRFormatException : EXRException
{
    public EXRFormatException(string message) : base(message)
    {
    }

    public EXRFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class CompressionException : EXRException
{
    public CompressionException(string message) : base(message)
    {

    }
}