using Product_Service.Exceptions;

namespace Product_Service.Models.Domain;


public class Price
{
    private int _priceValue;
    public int PriceValue
    {
        get => _priceValue;
        set => _priceValue = ValueValidator(value);
    }

    public Price(int value)
    {
        PriceValue = ValueValidator(value);
    }

    private static int ValueValidator(int value)
    {
        if (value <= 0)
        {
            throw new PriceValueException("El precio no puede ser cero ni negativo.");
        }

        return value;
    }
}
