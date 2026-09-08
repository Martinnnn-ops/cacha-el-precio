namespace Product_Service.Exceptions;


public class PriceValueException : Exception
{
    public PriceValueException() { }

    public PriceValueException(string message) : base(message) { }

    public PriceValueException(string message, Exception innerException) : base(message, innerException) { }
}
