using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Product_Service.Models.Domain;
using Product_Service.Models.DTOs;
using Product_Service.Models.Entities;
using Product_Service.Service;

namespace Product_Service.Controllers;

[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/products")]
public class ProductController : ControllerBase
{
    private readonly ProductService _service;

    public ProductController(ProductService service)
    {
        _service = service;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<ProductResponse>> GetAllProducts()
    {
        return Ok(await _service.GetAllProductsAsync());
    }

    [HttpGet("{id:int}")]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<ProductResponse>> GetProductById(int id)
    {
        ProductResponse? response = await _service.GetProductByIdAsync(id);
        return response switch
        {
            null => NotFound(),
            _ => Ok(response)
        };
    }

    /// <summary>
    /// Conserva la entrada histórica por ID en v2, pero dirige al cliente a
    /// la URL canónica legible del producto.
    /// </summary>
    [HttpGet("{id:int}")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult> GetProductByIdV2(int id)
    {
        string? slug = await _service.GetProductSlugByIdAsync(id);

        return slug switch
        {
            null => NotFound(),
            _ => RedirectToAction(nameof(GetProductBySlug), new { slug })
        };
    }

    // En rutas por atributo, los corchetes simples se interpretan como tokens
    // de reemplazo (por ejemplo, [controller]). Se duplican para que lleguen
    // como literales a la expresión regular del constraint.
    [HttpGet("{slug:regex(^(?![[0-9]]+$)[[a-z0-9]]+(?:-[[a-z0-9]]+)*$)}")]
    [MapToApiVersion("2.0")]
    public async Task<ActionResult<ProductResponse>> GetProductBySlug(string slug)
    {
        ProductResponse? response = await _service.GetProductBySlugAsync(slug);
        return response switch
        {
            null => NotFound(),
            _ => Ok(response)
        };
    }

    [HttpGet("by-category/{category}")]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<ProductResponse>> GetProductsByCategory(string category)
    {
        var products = await _service.GetProductsByCategoryAsync(category);

        return products switch
        {
            null => NotFound(),
            _ => Ok(products)
        };
    }

    [HttpGet("by-price/{price:int}")]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<ProductResponse>> GetProductsByPrice(int price)
    {
        var products = await _service.GetProductsByPriceAsync(price);

        return products switch
        {
            null => NotFound(),
            _ => Ok(products)
        };
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<ProductResponse>> CreateProduct(ProductRequest request)
    {
        if (CatalogPolicy.IsChild(request.Name ?? "", $"{request.Category} {request.Gender}"))
            return BadRequest(new { mensaje = "El catálogo es para adolescentes y adultos." });
        ProductResponse product = await _service.UpsertProductAsync(request);
        return Ok(product);
    }

    [HttpPut("{id:int}")]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult> UpdateProduct(int id, ProductRequest request)
    {
        if (CatalogPolicy.IsChild(request.Name ?? "", $"{request.Category} {request.Gender}"))
            return BadRequest(new { mensaje = "El catálogo es para adolescentes y adultos." });
        bool isSuccess = await _service.UpdateProductAsync(id, request);
        return isSuccess switch
        {
            false => NotFound(),
            true => NoContent()
        };
    }

    [HttpGet("by-size/{size}")]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetProductsBySize(string size)
    {
        return Ok(await _service.GetProductsBySizeAsync(size));
    }

    /// <summary>
    /// Suma una visita a la ficha. POST y no GET porque cambia el estado del
    /// servidor: un GET puede repetirlo cualquier proxy o precarga del
    /// navegador, y el contador se inflaría solo.
    /// </summary>
    [HttpPost("{id:int}/visits")]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult> RegisterVisit(int id)
    {
        bool registered = await _service.RegisterVisitAsync(id);

        return registered switch
        {
            false => NotFound(),
            true => NoContent()
        };
    }

    [HttpDelete("{id:int}")]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult> DeleteProductById(int id)
    {
        bool isDeleted = await _service.DeleteProductById(id);
        return isDeleted switch
        {
            false => NotFound(),
            true => Ok(isDeleted)
        };
    }

}
