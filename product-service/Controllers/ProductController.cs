using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Product_Service.Models.Domain;
using Product_Service.Models.DTOs;
using Product_Service.Models.Entities;
using Product_Service.Service;

namespace Product_Service.Controllers;

[ApiController]
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
        ProductResponse product = await _service.CreateProductAsync(request);
        return CreatedAtAction(nameof(GetProductById), new { id = product.Id }, product);
    }

    [HttpPut("{id:int}")]
    [MapToApiVersion("1.0")]
    public async Task<ActionResult> UpdateProduct(int id, ProductRequest request)
    {
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
