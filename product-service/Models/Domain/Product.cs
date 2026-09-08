namespace Product_Service.Models.Domain;


public class Product
{
    private string _productName = null!;
    private string _productBrand = null!;
    private Category _productCategory = null!;
    private Price _productPrice = null!;
    private ProductSizes _productSizes = null!;

    // get and set
    public string ProductName
    {
        get => _productName;
        set => _productName = StringValidator(value);
    }
    public string ProductBrand
    {
        get => _productBrand;
        set => _productBrand = StringValidator(value);
    }
    public string ProductCategory
    {
        get => _productCategory.Type.ToString();
        set => _productCategory = Category.From(value);
    }
    public int ProductPrice
    {
        get => _productPrice.PriceValue;
        set => _productPrice = new(value);
    }
    public ProductSizes ProductSizes
    {
        get => _productSizes;
        set => _productSizes = value;
    }

    // Constructor

    public Product(string name, string brand, string category, int price, ProductSizes sizes)
    {
        ProductName = name;
        ProductBrand = brand;
        ProductCategory = category;
        ProductPrice = price;
        ProductSizes = sizes;
    }


    private static string StringValidator(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("EL valor no puede ser null o espacios en blanco.");
        }

        return value.Trim();
    }

}
