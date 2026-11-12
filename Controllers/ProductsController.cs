using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;


namespace Trpo8.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class ProductsController : ControllerBase 
    {
        [HttpGet]
        [Authorize(Roles = "Admin")]

        public IActionResult Index()
        {
            var products = new List<Product>
            {
                new Product { Id = 1, Name = "Product 1", Price = 100 },
                new Product { Id = 2, Name = "Product 2", Price = 110 },
                new Product { Id = 3, Name = "Product 3", Price = 120 }
            };
            return Ok(products);
        }
    }

    public class Product
    {
        public required int Id { get; set; }
        public required string Name { get; set; }
        public double Price { get; set; }


    }
}
