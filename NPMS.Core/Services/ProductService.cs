using Microsoft.EntityFrameworkCore;
using NPMS.Core.Data;
using NPMS.Core.DTOs;
using NPMS.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace NPMS.Core.Services
{
    public interface IProductService
    {
        LoginResponse Authenticate(string username, string password);
        bool ChangePassword(int userId, string currentPassword, string newPassword);
        List<ProductListDto> SearchProducts(string? query, string? status, string? factory);
        ProductDetailDto? GetProductById(int id);
        ProductDetailDto? GetProductByPartNumber(string partNumber);
        ProductDetailDto CreateProduct(ProductSaveDto dto, string user);
        ProductDetailDto? UpdateProduct(int id, ProductSaveDto dto, string user);
        bool DeleteProduct(int id);
        List<ProcessStepDto> GetProductProcesses(int productId);
        List<DocumentDto> GetProductDocuments(int productId);
        DocumentDto AddDocument(int productId, string documentName, string documentType, string revision, string fileName, byte[] fileBytes, string uploadedBy);
        bool DeleteDocument(int documentId);
        ProductMaterialDto AddMaterial(int productId, string partNumber, string partName);
        bool DeleteMaterial(int materialId);
    }

    public class ProductService : IProductService
    {
        private readonly NpmsDbContext _db;
        private readonly string _storagePath;

        public ProductService(NpmsDbContext db, string? storagePath = null)
        {
            _db = db;
            _storagePath = storagePath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Storage");
            if (!Directory.Exists(_storagePath))
                Directory.CreateDirectory(_storagePath);
        }

        public static string HashPassword(string password)
        {
            // PBKDF2 with per-user salt stored as "salt:hash"
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var saltBytes = new byte[16];
            rng.GetBytes(saltBytes);
            var salt = Convert.ToBase64String(saltBytes);
            var hash = ComputePbkdf2(password, salt);
            return $"{salt}:{hash}";
        }

        public static bool VerifyPassword(string password, string storedHash)
        {
            // Support legacy SHA256 hashes (no colon separator)
            if (!storedHash.Contains(':'))
            {
                var legacyHash = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(password))).ToLower();
                return legacyHash == storedHash;
            }

            var parts = storedHash.Split(':', 2);
            if (parts.Length != 2) return false;
            var expected = ComputePbkdf2(password, parts[0]);
            return expected == parts[1];
        }

        private static string ComputePbkdf2(string password, string salt)
        {
            var saltBytes = Convert.FromBase64String(salt);
            var hashBytes = System.Security.Cryptography.Rfc2898DeriveBytes.Pbkdf2(
                password, saltBytes, 100_000,
                System.Security.Cryptography.HashAlgorithmName.SHA256, 32);
            return Convert.ToBase64String(hashBytes);
        }

        // ─── AUTH ─────────────────────────────────────────────────────────────

        public LoginResponse Authenticate(string username, string password)
        {
            var user = _db.Users.Include(u => u.Role)
                .FirstOrDefault(u => u.Username.ToLower() == username.ToLower());

            if (user == null || !VerifyPassword(password, user.PasswordHash))
                return new LoginResponse { Success = false, Message = "Username atau password salah." };

            if (!user.IsActive)
                return new LoginResponse { Success = false, Message = "Akun ini telah dinonaktifkan." };

            var role = user.Role?.RoleName ?? "";
            return new LoginResponse
            {
                Success          = true,
                Message          = "Login berhasil.",
                UserId           = user.UserId,
                Username         = user.Username,
                RoleName         = role,
                IsRdTeam         = role == UserRole.RdTeam,
                CanEditProduct   = role == UserRole.RdTeam,
                CanManageUsers   = role == UserRole.SuperAdmin,
                CanAccessStore   = role == UserRole.SuperAdmin || role == UserRole.StoreManager,
                CanEditStore     = role == UserRole.StoreManager,
                CanAccessSettings= role == UserRole.SuperAdmin,
            };
        }

        public bool ChangePassword(int userId, string currentPassword, string newPassword)
        {
            var user = _db.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null) return false;
            if (!VerifyPassword(currentPassword, user.PasswordHash)) return false;

            user.PasswordHash = HashPassword(newPassword);
            _db.SaveChanges();
            return true;
        }

        // ─── SEARCH ───────────────────────────────────────────────────────────

        public List<ProductListDto> SearchProducts(string? query, string? status, string? factory)
        {
            var q = _db.Products
                .Include(p => p.ManufacturingInfo)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim().ToLower();
                q = q.Where(p => p.PartNumber.ToLower().Contains(term) ||
                                 p.ProductName.ToLower().Contains(term) ||
                                 p.ProductType.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(status) && status != "All Statuses" && status != "Status")
                q = q.Where(p => p.Status.ToLower() == status.Trim().ToLower());

            if (!string.IsNullOrWhiteSpace(factory) && factory != "All Factories" && factory != "Factory")
                q = q.Where(p => p.ManufacturingInfo != null &&
                                 p.ManufacturingInfo.Factory.ToLower().Contains(factory.Trim().ToLower()));

            return q.Select(p => new ProductListDto
            {
                ProductId = p.ProductId,
                PartNumber = p.PartNumber,
                ProductName = p.ProductName,
                ProductFamily = p.ProductFamily,
                ProductGroup = p.ProductGroup,
                ProductType = p.ProductType,
                Status = p.Status,
                CurrentRevision = p.CurrentRevision,
                Factory = p.ManufacturingInfo != null ? p.ManufacturingInfo.Factory : "-",
                ProductionLine = p.ManufacturingInfo != null ? p.ManufacturingInfo.ProductionLine : "-",
                LastModified = p.UpdatedAt
            }).ToList();
        }

        // ─── GET ──────────────────────────────────────────────────────────────

        public ProductDetailDto? GetProductById(int id)
        {
            var p = _db.Products
                .Include(x => x.Specification)
                .Include(x => x.ManufacturingInfo)
                .Include(x => x.Processes).ThenInclude(pr => pr.Machine)
                .Include(x => x.Processes).ThenInclude(pr => pr.Tooling)
                .Include(x => x.Processes).ThenInclude(pr => pr.Parameters)
                .Include(x => x.Materials)
                .Include(x => x.Documents)
                .FirstOrDefault(x => x.ProductId == id);

            return p == null ? null : MapToDetailDto(p);
        }

        public ProductDetailDto? GetProductByPartNumber(string partNumber)
        {
            var p = _db.Products
                .Include(x => x.Specification)
                .Include(x => x.ManufacturingInfo)
                .Include(x => x.Processes).ThenInclude(pr => pr.Machine)
                .Include(x => x.Processes).ThenInclude(pr => pr.Tooling)
                .Include(x => x.Processes).ThenInclude(pr => pr.Parameters)
                .Include(x => x.Materials)
                .Include(x => x.Documents)
                .FirstOrDefault(x => x.PartNumber.ToLower() == partNumber.ToLower());

            return p == null ? null : MapToDetailDto(p);
        }

        // ─── CREATE ───────────────────────────────────────────────────────────

        public ProductDetailDto CreateProduct(ProductSaveDto dto, string user)
        {
            if (_db.Products.Any(p => p.PartNumber.ToLower() == dto.PartNumber.Trim().ToLower()))
                throw new InvalidOperationException($"Part Number '{dto.PartNumber}' sudah digunakan.");

            var p = new Product
            {
                PartNumber = dto.PartNumber,
                ProductName = dto.ProductName,
                ProductFamily = dto.ProductFamily,
                ProductGroup = dto.ProductGroup,
                ProductType = dto.ProductType,
                Status = string.IsNullOrWhiteSpace(dto.Status) ? ProductStatus.Draft : dto.Status,
                CurrentRevision = string.IsNullOrWhiteSpace(dto.CurrentRevision) ? "Revision A" : dto.CurrentRevision,
                Description = dto.Description,
                ImagePath = dto.ImagePath,
                CreatedBy = user,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            p.Specification = MapToSpecEntity(dto.Specification);
            p.ManufacturingInfo = MapToMfgEntity(dto.ManufacturingInfo);

            if (dto.Processes != null)
                foreach (var prDto in dto.Processes)
                    p.Processes.Add(BuildProcessStep(prDto, p.Processes.Count + 1));

            if (dto.Materials != null)
                foreach (var m in dto.Materials.Where(x => !string.IsNullOrWhiteSpace(x.PartNumber)))
                    p.Materials.Add(new ProductMaterial
                    {
                        PartNumber = m.PartNumber,
                        PartName = m.PartName
                    });

            // Single SaveChanges — atomic: product + spec + mfg + processes + materials all together
            _db.Products.Add(p);
            _db.SaveChanges();

            return GetProductById(p.ProductId)!;
        }

        // ─── UPDATE ───────────────────────────────────────────────────────────

        public ProductDetailDto? UpdateProduct(int id, ProductSaveDto dto, string user)
        {
            var p = _db.Products
                .Include(x => x.Specification)
                .Include(x => x.ManufacturingInfo)
                .Include(x => x.Processes).ThenInclude(pr => pr.Parameters)
                .Include(x => x.Materials)
                .Include(x => x.Documents)
                .FirstOrDefault(x => x.ProductId == id);

            if (p == null) return null;

            if (_db.Products.Any(x => x.PartNumber.ToLower() == dto.PartNumber.Trim().ToLower() && x.ProductId != id))
                throw new InvalidOperationException($"Part Number '{dto.PartNumber}' sudah digunakan.");

            p.PartNumber = dto.PartNumber;
            p.ProductName = dto.ProductName;
            p.ProductFamily = dto.ProductFamily;
            p.ProductGroup = dto.ProductGroup;
            p.ProductType = dto.ProductType;
            p.Status = dto.Status;
            p.CurrentRevision = dto.CurrentRevision;
            p.Description = dto.Description;
            p.ImagePath = string.IsNullOrWhiteSpace(dto.ImagePath) ? p.ImagePath : dto.ImagePath;
            p.UpdatedAt = DateTime.UtcNow;

            if (p.Specification == null) p.Specification = new ProductSpecification();
            ApplySpecDto(p.Specification, dto.Specification);

            if (p.ManufacturingInfo == null) p.ManufacturingInfo = new ManufacturingInformation();
            ApplyMfgDto(p.ManufacturingInfo, dto.ManufacturingInfo);

            // Rebuild processes
            _db.Processes.RemoveRange(p.Processes);
            p.Processes.Clear();
            if (dto.Processes != null)
                foreach (var prDto in dto.Processes)
                    p.Processes.Add(BuildProcessStep(prDto, p.Processes.Count + 1));

            // Rebuild materials
            _db.ProductMaterials.RemoveRange(p.Materials);
            p.Materials.Clear();
            if (dto.Materials != null)
                foreach (var m in dto.Materials.Where(x => !string.IsNullOrWhiteSpace(x.PartNumber)))
                    p.Materials.Add(new ProductMaterial
                    {
                        PartNumber = m.PartNumber,
                        PartName = m.PartName
                    });

            // Single SaveChanges — all changes atomic
            _db.SaveChanges();
            return GetProductById(p.ProductId);
        }

        // ─── DELETE ───────────────────────────────────────────────────────────

        public bool DeleteProduct(int id)
        {
            var p = _db.Products
                .Include(x => x.Documents)
                .FirstOrDefault(x => x.ProductId == id);
            if (p == null) return false;

            // Delete all physical document files belonging to this product
            DeleteProductStorageDirectory(id);

            _db.Products.Remove(p);
            _db.SaveChanges();
            return true;
        }

        // ─── PROCESSES ────────────────────────────────────────────────────────

        public List<ProcessStepDto> GetProductProcesses(int productId)
        {
            return _db.Processes
                .Include(p => p.Machine)
                .Include(p => p.Tooling)
                .Include(p => p.Parameters)
                .Where(p => p.ProductId == productId)
                .OrderBy(p => p.ProcessOrder)
                .Select(p => new ProcessStepDto
                {
                    ProcessId = p.ProcessId,
                    ProcessOrder = p.ProcessOrder,
                    ProcessName = p.ProcessName,
                    MachineName = p.Machine != null ? p.Machine.MachineName : "",
                    ToolingName = p.Tooling != null ? p.Tooling.ToolingName : "",
                    ProcessDescription = p.ProcessDescription,
                    Parameters = p.Parameters.Select(pr => new ProcessParameterDto
                    {
                        ParameterId = pr.ParameterId,
                        ParameterName = pr.ParameterName,
                        ParameterValue = pr.ParameterValue,
                        Unit = pr.Unit
                    }).ToList()
                }).ToList();
        }

        // ─── DOCUMENTS ────────────────────────────────────────────────────────

        public List<DocumentDto> GetProductDocuments(int productId)
        {
            return _db.Documents
                .Where(d => d.ProductId == productId)
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new DocumentDto
                {
                    DocumentId = d.DocumentId,
                    ProductId = d.ProductId,
                    DocumentName = d.DocumentName,
                    DocumentType = d.DocumentType,
                    Revision = d.Revision,
                    FileName = d.FileName,
                    FilePath = d.FilePath,
                    FileSize = d.FileSize,
                    UploadedBy = d.UploadedBy,
                    UploadedAt = d.UploadedAt
                }).ToList();
        }

        public DocumentDto AddDocument(int productId, string documentName, string documentType,
            string revision, string fileName, byte[] fileBytes, string uploadedBy)
        {
            // CRITICAL #4: Sanitize filename to prevent path traversal
            var safeFileName = SanitizeFileName(fileName);
            if (string.IsNullOrWhiteSpace(safeFileName))
                throw new ArgumentException("Nama file tidak valid.");

            var prodDir = Path.Combine(_storagePath, productId.ToString());
            if (!Directory.Exists(prodDir)) Directory.CreateDirectory(prodDir);

            // Handle duplicate filenames by appending counter
            var destPath = Path.Combine(prodDir, safeFileName);
            if (File.Exists(destPath))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(safeFileName);
                var ext = Path.GetExtension(safeFileName);
                var counter = 1;
                do
                {
                    safeFileName = $"{nameWithoutExt}_{counter}{ext}";
                    destPath = Path.Combine(prodDir, safeFileName);
                    counter++;
                } while (File.Exists(destPath));
            }

            File.WriteAllBytes(destPath, fileBytes);

            var doc = new DocumentMetadata
            {
                ProductId = productId,
                DocumentName = documentName,
                DocumentType = documentType,
                Revision = revision,
                FileName = safeFileName,
                FilePath = destPath,
                FileSize = fileBytes.Length,
                UploadedBy = uploadedBy,
                UploadedAt = DateTime.UtcNow
            };

            _db.Documents.Add(doc);
            _db.SaveChanges();

            return new DocumentDto
            {
                DocumentId = doc.DocumentId,
                ProductId = doc.ProductId,
                DocumentName = doc.DocumentName,
                DocumentType = doc.DocumentType,
                Revision = doc.Revision,
                FileName = doc.FileName,
                FilePath = doc.FilePath,
                FileSize = doc.FileSize,
                UploadedBy = doc.UploadedBy,
                UploadedAt = doc.UploadedAt
            };
        }

        public bool DeleteDocument(int documentId)
        {
            var doc = _db.Documents.FirstOrDefault(d => d.DocumentId == documentId);
            if (doc == null) return false;

            if (File.Exists(doc.FilePath))
            {
                try { File.Delete(doc.FilePath); } catch { /* log in real app */ }
            }

            _db.Documents.Remove(doc);
            _db.SaveChanges();
            return true;
        }

        // ─── MATERIALS ────────────────────────────────────────────────────────

        public ProductMaterialDto AddMaterial(int productId, string partNumber, string partName)
        {
            var mat = new ProductMaterial
            {
                ProductId = productId,
                PartNumber = partNumber,
                PartName = partName
            };
            _db.ProductMaterials.Add(mat);
            _db.SaveChanges();
            return new ProductMaterialDto
            {
                MaterialId = mat.MaterialId,
                ProductId = mat.ProductId,
                PartNumber = mat.PartNumber,
                PartName = mat.PartName
            };
        }

        public bool DeleteMaterial(int materialId)
        {
            var mat = _db.ProductMaterials.FirstOrDefault(m => m.MaterialId == materialId);
            if (mat == null) return false;
            _db.ProductMaterials.Remove(mat);
            _db.SaveChanges();
            return true;
        }

        // ─── PRIVATE HELPERS ──────────────────────────────────────────────────

        /// <summary>
        /// CRITICAL #4: Remove path separators and dangerous characters from filenames.
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
            // Extract just the filename, stripping any directory components
            var name = Path.GetFileName(fileName);

            // Remove any remaining path separator characters
            var invalid = Path.GetInvalidFileNameChars();
            foreach (var c in invalid)
                name = name.Replace(c, '_');

            // Extra guard: reject names that try to navigate up
            name = name.Replace("..", "_");

            return name.Trim();
        }

        /// <summary>
        /// CRITICAL #1: Delete the storage directory for a product and all its files.
        /// </summary>
        private void DeleteProductStorageDirectory(int productId)
        {
            var prodDir = Path.Combine(_storagePath, productId.ToString());
            if (Directory.Exists(prodDir))
            {
                try { Directory.Delete(prodDir, recursive: true); }
                catch { /* best-effort — files may be locked */ }
            }
        }

        private ProcessStep BuildProcessStep(ProcessStepDto prDto, int order)
        {
            int? machineId = null;
            int? toolingId = null;

            if (!string.IsNullOrWhiteSpace(prDto.MachineName) &&
                prDto.MachineName != "Selected Machine")
            {
                var machine = _db.Machines.FirstOrDefault(m =>
                    m.MachineName.ToLower() == prDto.MachineName.Trim().ToLower());
                machineId = machine?.MachineId;
            }

            if (!string.IsNullOrWhiteSpace(prDto.ToolingName) &&
                prDto.ToolingName != "Selected Tooling")
            {
                var tooling = _db.Toolings.FirstOrDefault(t =>
                    t.ToolingName.ToLower() == prDto.ToolingName.Trim().ToLower());
                toolingId = tooling?.ToolingId;
            }

            var pr = new ProcessStep
            {
                ProcessOrder = prDto.ProcessOrder > 0 ? prDto.ProcessOrder : order,
                ProcessName = prDto.ProcessName,
                ProcessDescription = prDto.ProcessDescription,
                MachineId = machineId,
                ToolingId = toolingId
            };

            if (prDto.Parameters != null)
                foreach (var paramDto in prDto.Parameters)
                    pr.Parameters.Add(new ProcessParameter
                    {
                        ParameterName = paramDto.ParameterName,
                        ParameterValue = paramDto.ParameterValue,
                        Unit = paramDto.Unit
                    });

            return pr;
        }

        private static ProductSpecification MapToSpecEntity(ProductSpecificationDto dto) => new()
        {
            Length = dto.Length,
            LengthTolerance = dto.LengthTolerance,
            Width = dto.Width,
            WidthTolerance = dto.WidthTolerance,
            Height = dto.Height,
            HeightTolerance = dto.HeightTolerance,
            Inductance = dto.Inductance,
            InductanceTolerance = dto.InductanceTolerance,
            Isaat = dto.Isaat,
            IsaatTolerance = dto.IsaatTolerance,
            Dcr = dto.Dcr,
            DcrTolerance = dto.DcrTolerance,
        };

        private static void ApplySpecDto(ProductSpecification spec, ProductSpecificationDto dto)
        {
            spec.Length = dto.Length;
            spec.LengthTolerance = dto.LengthTolerance;
            spec.Width = dto.Width;
            spec.WidthTolerance = dto.WidthTolerance;
            spec.Height = dto.Height;
            spec.HeightTolerance = dto.HeightTolerance;
            spec.Inductance = dto.Inductance;
            spec.InductanceTolerance = dto.InductanceTolerance;
            spec.Isaat = dto.Isaat;
            spec.IsaatTolerance = dto.IsaatTolerance;
            spec.Dcr = dto.Dcr;
            spec.DcrTolerance = dto.DcrTolerance;
        }

        private static ManufacturingInformation MapToMfgEntity(ManufacturingInfoDto dto) => new()
        {
            Factory = dto.Factory,
            ProductionLine = dto.ProductionLine,
            ProductionType = dto.ProductionType,
            EquipmentGroup = dto.EquipmentGroup,
            ProcessOwner = dto.ProcessOwner,
            ManufacturingNotes = dto.ManufacturingNotes
        };

        private static void ApplyMfgDto(ManufacturingInformation mfg, ManufacturingInfoDto dto)
        {
            mfg.Factory = dto.Factory;
            mfg.ProductionLine = dto.ProductionLine;
            mfg.ProductionType = dto.ProductionType;
            mfg.EquipmentGroup = dto.EquipmentGroup;
            mfg.ProcessOwner = dto.ProcessOwner;
            mfg.ManufacturingNotes = dto.ManufacturingNotes;
        }

        private static ProductDetailDto MapToDetailDto(Product p) => new()
        {
            ProductId = p.ProductId,
            PartNumber = p.PartNumber,
            ProductName = p.ProductName,
            ProductFamily = p.ProductFamily,
            ProductGroup = p.ProductGroup,
            ProductType = p.ProductType,
            Status = p.Status,
            CurrentRevision = p.CurrentRevision,
            Description = p.Description,
            ImagePath = p.ImagePath,
            CreatedBy = p.CreatedBy,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            Specification = p.Specification == null ? new ProductSpecificationDto() : new ProductSpecificationDto
            {
                SpecificationId = p.Specification.SpecificationId,
                Length = p.Specification.Length,
                LengthTolerance = p.Specification.LengthTolerance,
                Width = p.Specification.Width,
                WidthTolerance = p.Specification.WidthTolerance,
                Height = p.Specification.Height,
                HeightTolerance = p.Specification.HeightTolerance,
                Inductance = p.Specification.Inductance,
                InductanceTolerance = p.Specification.InductanceTolerance,
                Isaat = p.Specification.Isaat,
                IsaatTolerance = p.Specification.IsaatTolerance,
                Dcr = p.Specification.Dcr,
                DcrTolerance = p.Specification.DcrTolerance,
            },
            ManufacturingInfo = p.ManufacturingInfo == null ? new ManufacturingInfoDto() : new ManufacturingInfoDto
            {
                ManufacturingId = p.ManufacturingInfo.ManufacturingId,
                Factory = p.ManufacturingInfo.Factory,
                ProductionLine = p.ManufacturingInfo.ProductionLine,
                ProductionType = p.ManufacturingInfo.ProductionType,
                EquipmentGroup = p.ManufacturingInfo.EquipmentGroup,
                ProcessOwner = p.ManufacturingInfo.ProcessOwner,
                ManufacturingNotes = p.ManufacturingInfo.ManufacturingNotes
            },
            Processes = p.Processes.OrderBy(pr => pr.ProcessOrder).Select(pr => new ProcessStepDto
            {
                ProcessId = pr.ProcessId,
                ProcessOrder = pr.ProcessOrder,
                ProcessName = pr.ProcessName,
                MachineName = pr.Machine != null ? pr.Machine.MachineName : "",
                ToolingName = pr.Tooling != null ? pr.Tooling.ToolingName : "",
                ProcessDescription = pr.ProcessDescription,
                Parameters = pr.Parameters.Select(param => new ProcessParameterDto
                {
                    ParameterId = param.ParameterId,
                    ParameterName = param.ParameterName,
                    ParameterValue = param.ParameterValue,
                    Unit = param.Unit
                }).ToList()
            }).ToList(),
            Documents = p.Documents.OrderByDescending(d => d.UploadedAt).Select(d => new DocumentDto
            {
                DocumentId = d.DocumentId,
                ProductId = d.ProductId,
                DocumentName = d.DocumentName,
                DocumentType = d.DocumentType,
                Revision = d.Revision,
                FileName = d.FileName,
                FilePath = d.FilePath,
                FileSize = d.FileSize,
                UploadedBy = d.UploadedBy,
                UploadedAt = d.UploadedAt
            }).ToList(),
            Materials = p.Materials.Select(m => new ProductMaterialDto
            {
                MaterialId = m.MaterialId,
                ProductId = m.ProductId,
                PartNumber = m.PartNumber,
                PartName = m.PartName
            }).ToList()
        };
    }
}
