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
        List<ToolingDto> GetToolings();
        ToolingDto CreateTooling(ToolingSaveDto dto);
        ToolingDto? UpdateTooling(int id, ToolingSaveDto dto);
        bool DeleteTooling(int id);
        List<UsageRequestDto> GetUsageRequests();
        int GetPendingUsageRequestCount();
        int GetIssuedUsageRequestCount();
        UsageRequestDto CreateUsageRequest(string itemType, int inventoryId, double quantity, string reason, string username);
        UsageRequestDto? ConfirmUsageRequest(int requestId, string username);
        UsageRequestDto? CompleteUsageRequest(int requestId, double returnedQuantity, string username);
        List<DocumentDto> GetProductDocuments(int productId);
        DocumentDto AddDocument(int productId, string documentName, string documentType, string revision, string fileName, byte[] fileBytes, string uploadedBy, string? processName = null);
        bool DeleteDocument(int documentId);

        // Store Material Management
        List<StoreMaterialDto> GetStoreMaterials(
            string? query = null, string? partNumber = null, string? lot = null,
            string? productPartNumber = null, string? productFamily = null);
        StoreMaterialDto? GetStoreMaterialById(int id);
        StoreMaterialDto CreateStoreMaterial(StoreMaterialSaveDto dto, string user);
        StoreMaterialDto? UpdateStoreMaterial(int id, StoreMaterialSaveDto dto, string user);
        bool DeleteStoreMaterial(int id);
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
                CanAccessStore   = role == UserRole.SuperAdmin || role == UserRole.StoreManager || role == UserRole.RdTeam,
                CanEditStore     = role == UserRole.StoreManager,
                CanAccessSettings= role == UserRole.SuperAdmin,
            };
        }

        public bool ChangePassword(int userId, string currentPassword, string newPassword)
        {
            var user = _db.Users.FirstOrDefault(u => u.UserId == userId);
            if (user == null) return false;
            if (!VerifyPassword(currentPassword, user.PasswordHash)) return false;
            if (string.Equals(user.Username, newPassword, StringComparison.Ordinal)) return false;

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
                .Include(x => x.Documents)
                .FirstOrDefault(x => x.PartNumber.ToLower() == partNumber.ToLower());

            return p == null ? null : MapToDetailDto(p);
        }

        // ─── CREATE ───────────────────────────────────────────────────────────

        public ProductDetailDto CreateProduct(ProductSaveDto dto, string user)
        {
            var partNumber = dto.PartNumber.Trim();
            EnsurePartNumberAvailable(partNumber, "Product");

            var p = new Product
            {
                PartNumber = partNumber,
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

            // Single SaveChanges — atomic: product + spec + mfg + processes together
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
                .Include(x => x.Documents)
                .FirstOrDefault(x => x.ProductId == id);

            if (p == null) return null;

            var partNumber = dto.PartNumber.Trim();
            if (!string.Equals(p.PartNumber, partNumber, StringComparison.OrdinalIgnoreCase))
            {
                EnsurePartNumberAvailable(partNumber, "Product");
                RemoveRegisteredPartNumber(p.PartNumber, "Product");
            }

            p.PartNumber = partNumber;
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
            if (_db.StoreMaterials.Any(material => material.ProductPartNumber == p.PartNumber) ||
                _db.Toolings.Any(tooling => tooling.ProductPartNumber == p.PartNumber))
                throw new InvalidOperationException(
                    "Product ini masih terhubung dengan Material atau Tooling. Ubah hubungan inventaris sebelum menghapus product.");

            // Delete all physical document files belonging to this product
            DeleteProductStorageDirectory(id);

            RemoveRegisteredPartNumber(p.PartNumber, "Product");
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

        public List<ToolingDto> GetToolings()
        {
            return _db.Toolings
                .OrderBy(tooling => tooling.ToolingCode)
                .Select(tooling => new ToolingDto
                {
                    ToolingId = tooling.ToolingId,
                    ToolingCode = tooling.ToolingCode,
                    ToolingName = tooling.ToolingName,
                    EntryDate = tooling.EntryDate,
                    ItemCode = tooling.ItemCode,
                    UoM = tooling.UoM,
                    Lot = tooling.Lot,
                    Location = tooling.Location,
                    Qty = tooling.Qty,
                    Package = tooling.Package,
                    Remarks = tooling.Remarks,
                    ProductPartNumber = tooling.ProductPartNumber,
                    ProductFamily = tooling.ProductFamily
                })
                .ToList();
        }

        public ToolingDto CreateTooling(ToolingSaveDto dto)
        {
            ValidateToolingSaveDto(dto);
            var productFamily = GetProductFamily(dto.ProductPartNumber);
            var code = EnsurePartNumberAvailable(dto.ToolingCode.Trim(), "Tooling").PartNumber;
            var tooling = new Tooling
            {
                ToolingCode = code,
                ToolingName = dto.ToolingName.Trim(),
                EntryDate = dto.EntryDate,
                ItemCode = dto.ItemCode.Trim(),
                UoM = string.IsNullOrWhiteSpace(dto.UoM) ? "PCS" : dto.UoM.Trim(),
                Lot = dto.Lot.Trim(),
                Location = dto.Location.Trim(),
                Qty = dto.Qty,
                Package = dto.Package.Trim(),
                Remarks = dto.Remarks.Trim(),
                ProductPartNumber = dto.ProductPartNumber.Trim(),
                ProductFamily = productFamily
            };
            _db.Toolings.Add(tooling);
            _db.SaveChanges();
            return GetToolings().First(item => item.ToolingId == tooling.ToolingId);
        }

        public ToolingDto? UpdateTooling(int id, ToolingSaveDto dto)
        {
            ValidateToolingSaveDto(dto);
            var tooling = _db.Toolings.FirstOrDefault(item => item.ToolingId == id);
            if (tooling == null) return null;
            var previousCode = tooling.ToolingCode;
            var updatedCode = string.Equals(previousCode, dto.ToolingCode.Trim(), StringComparison.OrdinalIgnoreCase)
                ? previousCode
                : EnsurePartNumberAvailable(dto.ToolingCode.Trim(), "Tooling").PartNumber;
            var productFamily = GetProductFamily(dto.ProductPartNumber);
            if (!string.Equals(previousCode, updatedCode, StringComparison.OrdinalIgnoreCase))
                RemoveRegisteredPartNumber(previousCode, "Tooling");
            tooling.ToolingCode = updatedCode;
            tooling.ToolingName = dto.ToolingName.Trim();
            tooling.EntryDate = dto.EntryDate;
            tooling.ItemCode = dto.ItemCode.Trim();
            tooling.UoM = string.IsNullOrWhiteSpace(dto.UoM) ? "PCS" : dto.UoM.Trim();
            tooling.Lot = dto.Lot.Trim();
            tooling.Location = dto.Location.Trim();
            tooling.Qty = dto.Qty;
            tooling.Package = dto.Package.Trim();
            tooling.Remarks = dto.Remarks.Trim();
            tooling.ProductPartNumber = dto.ProductPartNumber.Trim();
            tooling.ProductFamily = productFamily;
            _db.SaveChanges();
            return GetToolings().First(item => item.ToolingId == id);
        }

        private static void ValidateToolingSaveDto(ToolingSaveDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ToolingCode) ||
                string.IsNullOrWhiteSpace(dto.ToolingName) ||
            string.IsNullOrWhiteSpace(dto.Location) || dto.EntryDate == default ||
            string.IsNullOrWhiteSpace(dto.ProductPartNumber))
            throw new InvalidOperationException("Part Number, tooling name, entry date, location, and linked product are required.");
            if (dto.Qty < 0 || double.IsNaN(dto.Qty) || double.IsInfinity(dto.Qty))
                throw new InvalidOperationException("Tooling quantity must be a valid non-negative number.");
        }

        public bool DeleteTooling(int id)
        {
            var tooling = _db.Toolings.FirstOrDefault(item => item.ToolingId == id);
            if (tooling == null) return false;
            if (_db.Processes.Any(process => process.ToolingId == id))
                throw new InvalidOperationException("Tooling ini masih digunakan oleh proses produk dan tidak dapat dihapus.");
            if (_db.UsageRequests.Any(request => request.ItemType == "Tooling" &&
                    request.InventoryId == id && (request.Status == "Pending" || request.Status == "Issued")))
                throw new InvalidOperationException("Tooling tidak dapat dihapus karena masih memiliki request aktif.");
            RemoveRegisteredPartNumber(tooling.ToolingCode, "Tooling");
            _db.Toolings.Remove(tooling);
            _db.SaveChanges();
            return true;
        }

        public List<UsageRequestDto> GetUsageRequests()
        {
            return _db.UsageRequests.OrderByDescending(request => request.RequestedAt)
                .ToList()
                .Select(ToUsageRequestDto)
                .ToList();
        }

        public int GetPendingUsageRequestCount() =>
            _db.UsageRequests.Count(request => request.Status == "Pending");

        public int GetIssuedUsageRequestCount() =>
            _db.UsageRequests.Count(request => request.Status == "Issued");

        public UsageRequestDto CreateUsageRequest(
            string itemType, int inventoryId, double quantity, string reason, string username)
        {
            if (quantity <= 0 || double.IsNaN(quantity) || double.IsInfinity(quantity))
                throw new InvalidOperationException("Jumlah request harus berupa angka positif.");
            if (string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Keterangan pemakaian dan username wajib diisi.");

            var request = new UsageRequest
            {
                ItemType = itemType,
                InventoryId = inventoryId,
                RequestedQty = quantity,
                Reason = reason.Trim(),
                RequestedBy = username,
                RequestedAt = DateTime.UtcNow,
                Status = "Pending"
            };

            if (itemType == "Material")
            {
                var material = _db.StoreMaterials.FirstOrDefault(item => item.MaterialId == inventoryId)
                    ?? throw new InvalidOperationException("Material tidak ditemukan.");
                request.PartNumber = material.PartNumber;
                request.ItemDescription = material.ItemDescription;
                request.Lot = material.Lot;
                request.UoM = material.UoM;
                var pendingQuantity = _db.UsageRequests
                    .Where(existing => existing.ItemType == itemType &&
                                       existing.InventoryId == inventoryId &&
                                       existing.Status == "Pending")
                    .Sum(existing => (double?)existing.RequestedQty) ?? 0;
                if (quantity > material.Qty - pendingQuantity)
                    throw new InvalidOperationException("Jumlah request melebihi stok material yang tersedia.");
            }
            else if (itemType == "Tooling")
            {
                var tooling = _db.Toolings.FirstOrDefault(item => item.ToolingId == inventoryId)
                    ?? throw new InvalidOperationException("Tooling tidak ditemukan.");
                request.PartNumber = tooling.ToolingCode;
                request.ItemDescription = tooling.ToolingName;
                request.UoM = tooling.UoM;
                var pendingQuantity = _db.UsageRequests
                    .Where(existing => existing.ItemType == itemType &&
                                       existing.InventoryId == inventoryId &&
                                       existing.Status == "Pending")
                    .Sum(existing => (double?)existing.RequestedQty) ?? 0;
                if (quantity > tooling.Qty - pendingQuantity)
                    throw new InvalidOperationException("Jumlah request melebihi stok tooling yang tersedia.");
            }
            else
            {
                throw new InvalidOperationException("Jenis inventaris tidak valid.");
            }

            _db.UsageRequests.Add(request);
            _db.SaveChanges();
            return ToUsageRequestDto(request);
        }

        public UsageRequestDto? ConfirmUsageRequest(int requestId, string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Username Store wajib tersedia untuk konfirmasi.");
            var request = _db.UsageRequests.FirstOrDefault(item => item.RequestId == requestId);
            if (request == null) return null;
            if (request.Status != "Pending")
                throw new InvalidOperationException("Request ini sudah diproses.");

            if (request.ItemType == "Material")
            {
                var material = _db.StoreMaterials.FirstOrDefault(item => item.MaterialId == request.InventoryId)
                    ?? throw new InvalidOperationException("Material yang diminta sudah tidak tersedia.");
                if (material.Qty < request.RequestedQty)
                    throw new InvalidOperationException("Stok tidak cukup untuk memenuhi request ini.");
                material.Qty -= request.RequestedQty;
            }
            else
            {
                var tooling = _db.Toolings.FirstOrDefault(item => item.ToolingId == request.InventoryId)
                    ?? throw new InvalidOperationException("Tooling yang diminta sudah tidak tersedia.");
                if (tooling.Qty < request.RequestedQty)
                    throw new InvalidOperationException("Stok tidak cukup untuk memenuhi request ini.");
                tooling.Qty -= request.RequestedQty;
            }

            request.ConfirmedBy = username;
            request.ConfirmedAt = DateTime.UtcNow;
            request.Status = "Issued";
            _db.SaveChanges();
            return ToUsageRequestDto(request);
        }

        public UsageRequestDto? CompleteUsageRequest(int requestId, double returnedQuantity, string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new InvalidOperationException("Username R&D wajib tersedia untuk menyelesaikan request.");
            var request = _db.UsageRequests.FirstOrDefault(item => item.RequestId == requestId);
            if (request == null) return null;
            if (request.Status != "Issued")
                throw new InvalidOperationException("Hanya request yang sudah diterima Store yang dapat diselesaikan.");
            if (returnedQuantity < 0 || double.IsNaN(returnedQuantity) ||
                double.IsInfinity(returnedQuantity) || returnedQuantity > request.RequestedQty)
                throw new InvalidOperationException("Jumlah sisa harus antara 0 dan jumlah yang diminta.");

            if (request.ItemType == "Material")
            {
                var material = _db.StoreMaterials.FirstOrDefault(item => item.MaterialId == request.InventoryId)
                    ?? throw new InvalidOperationException("Material tidak ditemukan untuk pengembalian.");
                material.Qty += returnedQuantity;
            }
            else
            {
                var tooling = _db.Toolings.FirstOrDefault(item => item.ToolingId == request.InventoryId)
                    ?? throw new InvalidOperationException("Tooling tidak ditemukan untuk pengembalian.");
                tooling.Qty += returnedQuantity;
            }

            request.ReturnedQty = returnedQuantity;
            request.ReturnedBy = username;
            request.ReturnedAt = DateTime.UtcNow;
            request.Status = "Completed";
            _db.SaveChanges();
            return ToUsageRequestDto(request);
        }

        private static UsageRequestDto ToUsageRequestDto(UsageRequest request) => new()
        {
            RequestId = request.RequestId,
            ItemType = request.ItemType,
            InventoryId = request.InventoryId,
            PartNumber = request.PartNumber,
            ItemDescription = request.ItemDescription,
            Lot = request.Lot,
            UoM = request.UoM,
            RequestedQty = request.RequestedQty,
            ReturnedQty = request.ReturnedQty,
            Reason = request.Reason,
            RequestedBy = request.RequestedBy,
            RequestedAt = request.RequestedAt,
            ConfirmedBy = request.ConfirmedBy,
            ConfirmedAt = request.ConfirmedAt,
            ReturnedBy = request.ReturnedBy,
            ReturnedAt = request.ReturnedAt,
            Status = request.Status
        };

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
                    ProcessName = d.ProcessName,
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
            string revision, string fileName, byte[] fileBytes, string uploadedBy, string? processName = null)
        {
            var selectedProcessName = string.IsNullOrWhiteSpace(processName) ? null : processName.Trim();
            if (selectedProcessName != null && !_db.Processes.Any(process =>
                    process.ProductId == productId && process.ProcessName == selectedProcessName))
                throw new InvalidOperationException("Proses yang dipilih tidak ditemukan pada produk ini.");

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
                ProcessName = selectedProcessName,
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
                ProcessName = doc.ProcessName,
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
                ProcessName = d.ProcessName,
                DocumentName = d.DocumentName,
                DocumentType = d.DocumentType,
                Revision = d.Revision,
                FileName = d.FileName,
                FilePath = d.FilePath,
                FileSize = d.FileSize,
                UploadedBy = d.UploadedBy,
                UploadedAt = d.UploadedAt
            }).ToList()
        };

        // ─── STORE MATERIAL MANAGEMENT ────────────────────────────────────────

        public List<StoreMaterialDto> GetStoreMaterials(
            string? query = null, string? partNumber = null, string? lot = null,
            string? productPartNumber = null, string? productFamily = null)
        {
            var q = _db.StoreMaterials.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim().ToLower();
                q = q.Where(m => m.PartNumber.ToLower().Contains(term) ||
                                 m.ItemDescription.ToLower().Contains(term) ||
                                 m.ItemCode.ToLower().Contains(term) ||
                                 m.Lot.ToLower().Contains(term) ||
                                 m.Location.ToLower().Contains(term) ||
                                 m.ProductPartNumber.ToLower().Contains(term) ||
                                 m.ProductFamily.ToLower().Contains(term) ||
                                 m.Remarks.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(partNumber) && partNumber != "All Part Numbers")
            {
                q = q.Where(m => m.PartNumber.ToLower() == partNumber.Trim().ToLower());
            }

            if (!string.IsNullOrWhiteSpace(lot) && lot != "All Lots")
            {
                q = q.Where(m => m.Lot.ToLower() == lot.Trim().ToLower());
            }
            if (!string.IsNullOrWhiteSpace(productPartNumber) && productPartNumber != "All Products")
                q = q.Where(m => m.ProductPartNumber.ToLower() == productPartNumber.Trim().ToLower());
            if (!string.IsNullOrWhiteSpace(productFamily) && productFamily != "All Product Families")
                q = q.Where(m => m.ProductFamily.ToLower() == productFamily.Trim().ToLower());

            return q.OrderByDescending(m => m.EntryDate)
                .ThenByDescending(m => m.MaterialId)
                .Select(m => new StoreMaterialDto
                {
                    MaterialId = m.MaterialId,
                    PartNumber = m.PartNumber,
                    EntryDate = m.EntryDate,
                    ItemDescription = m.ItemDescription,
                    ItemCode = m.ItemCode,
                    UoM = m.UoM,
                    ProductPartNumber = m.ProductPartNumber,
                    ProductFamily = m.ProductFamily,
                    Lot = m.Lot,
                    Location = m.Location,
                    Qty = m.Qty,
                    Package = m.Package,
                    Remarks = m.Remarks,
                    CreatedBy = m.CreatedBy,
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt
                })
                .ToList();
        }

        public StoreMaterialDto? GetStoreMaterialById(int id)
        {
            var m = _db.StoreMaterials.FirstOrDefault(sm => sm.MaterialId == id);
            if (m == null) return null;
            return new StoreMaterialDto
            {
                MaterialId = m.MaterialId,
                PartNumber = m.PartNumber,
                EntryDate = m.EntryDate,
                ItemDescription = m.ItemDescription,
                ItemCode = m.ItemCode,
                UoM = m.UoM,
                ProductPartNumber = m.ProductPartNumber,
                ProductFamily = m.ProductFamily,
                Lot = m.Lot,
                Location = m.Location,
                Qty = m.Qty,
                Package = m.Package,
                Remarks = m.Remarks,
                CreatedBy = m.CreatedBy,
                CreatedAt = m.CreatedAt,
                UpdatedAt = m.UpdatedAt
            };
        }

        public StoreMaterialDto CreateStoreMaterial(StoreMaterialSaveDto dto, string user)
        {
            var productFamily = GetProductFamily(dto.ProductPartNumber);
            ValidateMaterialProductLink(dto.PartNumber, dto.ProductPartNumber);
            var partNumber = EnsurePartNumberAvailable(dto.PartNumber.Trim(), "Material", allowExistingType: true).PartNumber;
            var lot = dto.Lot.Trim();
            ValidateMaterialLot(partNumber, lot);
            var entity = new StoreMaterial
            {
                PartNumber = partNumber,
                EntryDate = dto.EntryDate,
                ItemDescription = dto.ItemDescription.Trim(),
                ItemCode = dto.ItemCode.Trim(),
                UoM = string.IsNullOrWhiteSpace(dto.UoM) ? "PCS" : dto.UoM.Trim(),
                ProductPartNumber = dto.ProductPartNumber.Trim(),
                ProductFamily = productFamily,
                Lot = lot,
                Location = dto.Location.Trim(),
                Qty = dto.Qty,
                Package = dto.Package.Trim(),
                Remarks = dto.Remarks.Trim(),
                CreatedBy = user,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.StoreMaterials.Add(entity);
            _db.SaveChanges();

            return GetStoreMaterialById(entity.MaterialId)!;
        }

        public StoreMaterialDto? UpdateStoreMaterial(int id, StoreMaterialSaveDto dto, string user)
        {
            var entity = _db.StoreMaterials.FirstOrDefault(sm => sm.MaterialId == id);
            if (entity == null) return null;

            ValidateMaterialProductLink(dto.PartNumber, dto.ProductPartNumber, id);
            var partNumber = EnsurePartNumberAvailable(dto.PartNumber.Trim(), "Material", allowExistingType: true).PartNumber;
            var lot = dto.Lot.Trim();
            ValidateMaterialLot(partNumber, lot, id);
            entity.PartNumber = partNumber;
            entity.EntryDate = dto.EntryDate;
            entity.ItemDescription = dto.ItemDescription.Trim();
            entity.ItemCode = dto.ItemCode.Trim();
            entity.UoM = string.IsNullOrWhiteSpace(dto.UoM) ? "PCS" : dto.UoM.Trim();
            entity.ProductPartNumber = dto.ProductPartNumber.Trim();
            entity.ProductFamily = GetProductFamily(dto.ProductPartNumber);
            entity.Lot = lot;
            entity.Location = dto.Location.Trim();
            entity.Qty = dto.Qty;
            entity.Package = dto.Package.Trim();
            entity.Remarks = dto.Remarks.Trim();
            entity.UpdatedAt = DateTime.UtcNow;

            _db.SaveChanges();
            return GetStoreMaterialById(id);
        }

        private string GetProductFamily(string productPartNumber)
        {
            if (string.IsNullOrWhiteSpace(productPartNumber))
                throw new InvalidOperationException("Product Part Number wajib dipilih.");
            var product = _db.Products.FirstOrDefault(item =>
                item.PartNumber.ToLower() == productPartNumber.Trim().ToLower());
            return product?.ProductFamily
                ?? throw new InvalidOperationException($"Product '{productPartNumber}' tidak ditemukan.");
        }

        private void ValidateMaterialProductLink(
            string materialPartNumber, string productPartNumber, int? excludedMaterialId = null)
        {
            var normalizedMaterialPartNumber = materialPartNumber.Trim().ToLower();
            var normalizedProductPartNumber = productPartNumber.Trim().ToLower();
            var conflictingLink = _db.StoreMaterials.Any(material =>
                material.PartNumber.ToLower() == normalizedMaterialPartNumber &&
                material.ProductPartNumber.ToLower() != normalizedProductPartNumber &&
                (!excludedMaterialId.HasValue || material.MaterialId != excludedMaterialId.Value));
            if (conflictingLink)
                throw new InvalidOperationException(
                    $"Material '{materialPartNumber}' sudah terhubung ke Product Part Number lain.");
        }

        private void ValidateMaterialLot(string partNumber, string lot, int? excludedMaterialId = null)
        {
            if (string.IsNullOrWhiteSpace(lot))
                throw new InvalidOperationException("Lot wajib diisi.");

            var normalizedPartNumber = partNumber.Trim().ToLower();
            var normalizedLot = lot.Trim().ToLower();
            var duplicate = _db.StoreMaterials.Any(material =>
                material.PartNumber.Trim().ToLower() == normalizedPartNumber &&
                material.Lot.Trim().ToLower() == normalizedLot &&
                (!excludedMaterialId.HasValue || material.MaterialId != excludedMaterialId.Value));
            if (duplicate)
                throw new InvalidOperationException(
                    $"Lot '{lot}' sudah digunakan untuk Material Part Number '{partNumber}'.");
        }

        public bool DeleteStoreMaterial(int id)
        {
            var entity = _db.StoreMaterials.FirstOrDefault(sm => sm.MaterialId == id);
            if (entity == null) return false;
            if (_db.UsageRequests.Any(request => request.ItemType == "Material" &&
                    request.InventoryId == id && (request.Status == "Pending" || request.Status == "Issued")))
                throw new InvalidOperationException("Material tidak dapat dihapus karena masih memiliki request aktif.");
            _db.StoreMaterials.Remove(entity);
            _db.SaveChanges();
            return true;
        }

        private RegisteredPartNumber EnsurePartNumberAvailable(
            string partNumber,
            string itemType,
            bool allowExistingType = false)
        {
            if (string.IsNullOrWhiteSpace(partNumber))
                throw new InvalidOperationException("Part Number wajib diisi.");

            var normalizedPartNumber = partNumber.Trim();
            var registered = _db.RegisteredPartNumbers
                .FirstOrDefault(item => item.PartNumber.ToLower() == normalizedPartNumber.ToLower());
            if (registered != null)
            {
                if (allowExistingType && string.Equals(registered.ItemType, itemType, StringComparison.Ordinal))
                    return registered;

                throw new InvalidOperationException($"Part Number '{normalizedPartNumber}' sudah digunakan.");
            }

            var existsAsProduct = _db.Products.Any(product =>
                product.PartNumber.ToLower() == normalizedPartNumber.ToLower());
            var existsAsTooling = _db.Toolings.Any(tooling =>
                tooling.ToolingCode.ToLower() == normalizedPartNumber.ToLower());
            var existsAsMaterial = _db.StoreMaterials.Any(material =>
                material.PartNumber.ToLower() == normalizedPartNumber.ToLower());

            var conflicts = itemType switch
            {
                "Product" => existsAsProduct || existsAsTooling || existsAsMaterial,
                "Material" => existsAsProduct || existsAsTooling || (existsAsMaterial && !allowExistingType),
                "Tooling" => existsAsProduct || existsAsTooling || existsAsMaterial,
                _ => true
            };
            if (conflicts)
                throw new InvalidOperationException($"Part Number '{normalizedPartNumber}' sudah digunakan.");

            registered = new RegisteredPartNumber
            {
                PartNumber = normalizedPartNumber,
                ItemType = itemType
            };
            _db.RegisteredPartNumbers.Add(registered);
            return registered;
        }

        private void RemoveRegisteredPartNumber(string partNumber, string itemType)
        {
            var registered = _db.RegisteredPartNumbers.FirstOrDefault(item =>
                item.PartNumber.ToLower() == partNumber.ToLower() &&
                item.ItemType == itemType);
            if (registered != null)
                _db.RegisteredPartNumbers.Remove(registered);
        }
    }  // end ProductService class
}  // end namespace
