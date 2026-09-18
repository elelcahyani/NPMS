using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NPMS.Core.DTOs;
using NPMS.Core.Services;
using System.IO;

namespace NPMS.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _service;

        public ProductsController(IProductService service)
        {
            _service = service;
        }

        [HttpGet]
        public IActionResult GetProducts([FromQuery] string? query, [FromQuery] string? status, [FromQuery] string? factory)
        {
            var list = _service.SearchProducts(query, status, factory);
            return Ok(list);
        }

        [HttpGet("search")]
        public IActionResult SearchProducts([FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? factory)
        {
            var list = _service.SearchProducts(q, status, factory);
            return Ok(list);
        }

        [HttpGet("{id:int}")]
        public IActionResult GetProductById(int id)
        {
            var prod = _service.GetProductById(id);
            if (prod == null) return NotFound(new { message = "Product not found." });
            return Ok(prod);
        }

        [HttpPost]
        public IActionResult CreateProduct([FromBody] ProductSaveDto dto)
        {
            var user = Request.Headers["X-User-Name"].ToString();
            if (string.IsNullOrEmpty(user)) user = "R&D User";
            var created = _service.CreateProduct(dto, user);
            return CreatedAtAction(nameof(GetProductById), new { id = created.ProductId }, created);
        }

        [HttpPut("{id:int}")]
        public IActionResult UpdateProduct(int id, [FromBody] ProductSaveDto dto)
        {
            var user = Request.Headers["X-User-Name"].ToString();
            if (string.IsNullOrEmpty(user)) user = "R&D User";
            var updated = _service.UpdateProduct(id, dto, user);
            if (updated == null) return NotFound(new { message = "Product not found." });
            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public IActionResult DeleteProduct(int id)
        {
            var success = _service.DeleteProduct(id);
            if (!success) return NotFound(new { message = "Product not found." });
            return Ok(new { message = "Product deleted successfully." });
        }

        [HttpGet("{id:int}/processes")]
        public IActionResult GetProductProcesses(int id)
        {
            var processes = _service.GetProductProcesses(id);
            return Ok(processes);
        }

        [HttpGet("{id:int}/documents")]
        public IActionResult GetProductDocuments(int id)
        {
            var docs = _service.GetProductDocuments(id);
            return Ok(docs);
        }

        [HttpPost("{id:int}/documents")]
        public IActionResult UploadDocument(int id, [FromForm] string documentName, [FromForm] string documentType, [FromForm] string revision, [FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest(new { message = "File is required." });

            var user = Request.Headers["X-User-Name"].ToString();
            if (string.IsNullOrEmpty(user)) user = "R&D User";

            using var ms = new MemoryStream();
            file.CopyTo(ms);
            var bytes = ms.ToArray();

            var doc = _service.AddDocument(id, documentName, documentType, revision, file.FileName, bytes, user);
            return Ok(doc);
        }
    }
}
