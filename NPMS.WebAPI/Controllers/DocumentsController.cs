using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NPMS.Core.Data;
using NPMS.Core.Services;
using System.IO;
using System.Linq;

namespace NPMS.WebAPI.Controllers
{
    [ApiController]
    [Authorize(AuthenticationSchemes = "NPMSApiKey")]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IProductService _service;
        private readonly NpmsDbContext _db;

        public DocumentsController(IProductService service, NpmsDbContext db)
        {
            _service = service;
            _db = db;
        }

        [HttpGet("{id:int}/download")]
        public IActionResult DownloadDocument(int id)
        {
            var doc = _db.Documents.FirstOrDefault(d => d.DocumentId == id);
            if (doc == null) return NotFound(new { message = "Document metadata not found." });

            if (System.IO.File.Exists(doc.FilePath))
            {
                var bytes = System.IO.File.ReadAllBytes(doc.FilePath);
                return File(bytes, "application/octet-stream", doc.FileName);
            }

            // Fallback generated file for demonstration
            var mockContent = System.Text.Encoding.UTF8.GetBytes($"--- NPMS DOCUMENT ---\nDocument: {doc.DocumentName}\nType: {doc.DocumentType}\nRevision: {doc.Revision}\nFile Name: {doc.FileName}\nUploaded By: {doc.UploadedBy}\nDate: {doc.UploadedAt}\n\nContent specification placeholder.");
            return File(mockContent, "application/octet-stream", doc.FileName);
        }

        [HttpDelete("{id:int}")]
        public IActionResult DeleteDocument(int id)
        {
            var success = _service.DeleteDocument(id);
            if (!success) return NotFound(new { message = "Document not found." });
            return Ok(new { message = "Document deleted successfully." });
        }
    }
}
