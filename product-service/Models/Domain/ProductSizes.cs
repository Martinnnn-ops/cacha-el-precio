namespace Product_Service.Models.Domain;


public class ProductSizes
{
    public bool XS { get; set; }
    public bool S { get; set; }
    public bool M { get; set; }
    public bool L { get; set; }
    public bool XL { get; set; }
    public bool XXL { get; set; }


    public bool IsAvailable(Size size)
    {
        return size switch
        {
            Size.XS => XS,
            Size.S => S,
            Size.M => M,
            Size.L => L,
            Size.XL => XL,
            Size.XXL => XXL,
            _ => throw new NotImplementedException()
        };
    }

}
