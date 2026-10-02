using System;
using System.Collections.Generic;

namespace NPMS.Core.DTOs
{
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;

        // Permission flags — derived from role
        public bool IsRdTeam { get; set; }          // kept for backward compat (= CanEditProduct)
        public bool CanManageUsers { get; set; }    // Super Admin only
        public bool CanEditProduct { get; set; }    // R&D Team
        public bool CanAccessStore { get; set; }    // Super Admin, Store Manager, and R&D Team
        public bool CanEditStore { get; set; }      // Store Manager only
        public bool CanAccessSettings { get; set; } // Super Admin only
    }

    public class ProductListDto
    {
        public int ProductId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductFamily { get; set; } = string.Empty;
        public string ProductGroup { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CurrentRevision { get; set; } = string.Empty;
        public string Factory { get; set; } = string.Empty;
        public string ProductionLine { get; set; } = string.Empty;
        public DateTime LastModified { get; set; }
    }

    public class ProductDetailDto
    {
        public int ProductId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductFamily { get; set; } = string.Empty;
        public string ProductGroup { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CurrentRevision { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ProductSpecificationDto Specification { get; set; } = new();
        public ManufacturingInfoDto ManufacturingInfo { get; set; } = new();
        public List<ProcessStepDto> Processes { get; set; } = new();
        public List<DocumentDto> Documents { get; set; } = new();
    }

    public class ProductSpecificationDto
    {
        public int SpecificationId { get; set; }

        public string Length { get; set; } = string.Empty;
        public string LengthTolerance { get; set; } = string.Empty;
        public string Width { get; set; } = string.Empty;
        public string WidthTolerance { get; set; } = string.Empty;
        public string Height { get; set; } = string.Empty;
        public string HeightTolerance { get; set; } = string.Empty;

        public string Inductance { get; set; } = string.Empty;
        public string InductanceTolerance { get; set; } = string.Empty;
        public string Isaat { get; set; } = string.Empty;
        public string IsaatTolerance { get; set; } = string.Empty;
        public string Dcr { get; set; } = string.Empty;
        public string DcrTolerance { get; set; } = string.Empty;
    }

    public class ManufacturingInfoDto
    {
        public int ManufacturingId { get; set; }
        public string Factory { get; set; } = string.Empty;
        public string ProductionLine { get; set; } = string.Empty;
        public string ProductionType { get; set; } = string.Empty;
        public string EquipmentGroup { get; set; } = string.Empty;
        public string ProcessOwner { get; set; } = string.Empty;
        public string ManufacturingNotes { get; set; } = string.Empty;
    }

    public class ProcessStepDto
    {
        public int ProcessId { get; set; }
        public int ProcessOrder { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;
        public string ToolingName { get; set; } = string.Empty;
        public string ProcessDescription { get; set; } = string.Empty;
        public List<ProcessParameterDto> Parameters { get; set; } = new();
    }

    public class ProcessParameterDto
    {
        public int ParameterId { get; set; }
        public string ParameterName { get; set; } = string.Empty;
        public string ParameterValue { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
    }

    public class ToolingDto
    {
        public int ToolingId { get; set; }
        public string ToolingCode { get; set; } = string.Empty;
        public string ToolingName { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public string ItemCode { get; set; } = string.Empty;
        public string UoM { get; set; } = "PCS";
        public string Lot { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Qty { get; set; }
        public double AvailableQty { get; set; }
        public string Package { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string ProductPartNumber { get; set; } = string.Empty;
        public string ProductFamily { get; set; } = string.Empty;
    }

    public class ToolingSaveDto
    {
        public int ToolingId { get; set; }
        public string ToolingCode { get; set; } = string.Empty;
        public string ToolingName { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; } = DateTime.Now;
        public string ItemCode { get; set; } = string.Empty;
        public string UoM { get; set; } = "PCS";
        public string Lot { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Qty { get; set; }
        public string Package { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string ProductPartNumber { get; set; } = string.Empty;
        public string ProductFamily { get; set; } = string.Empty;
    }

    public class UsageRequestDto
    {
        public int RequestId { get; set; }
        public string ItemType { get; set; } = string.Empty;
        public int InventoryId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string ItemDescription { get; set; } = string.Empty;
        public string Lot { get; set; } = string.Empty;
        public string UoM { get; set; } = "PCS";
        public double RequestedQty { get; set; }
        public double? ReturnedQty { get; set; }
        public double? UsedQty => ReturnedQty.HasValue ? RequestedQty - ReturnedQty.Value : null;
        public string Reason { get; set; } = string.Empty;
        public string RequestedBy { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public DateTime RequestedAtLocal => RequestedAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(RequestedAt, DateTimeKind.Utc).ToLocalTime()
            : RequestedAt.ToLocalTime();
        public string? ConfirmedBy { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string? ReturnedBy { get; set; }
        public DateTime? ReturnedAt { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class DocumentDto
    {
        public int DocumentId { get; set; }
        public int ProductId { get; set; }
        public string? ProcessName { get; set; }
        public string ProcessScope => string.IsNullOrWhiteSpace(ProcessName) ? "All processes" : ProcessName;
        public string DocumentName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string Revision { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; }
    }

    public class ProductSaveDto
    {
        public int ProductId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductFamily { get; set; } = string.Empty;
        public string ProductGroup { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty;
        public string Status { get; set; } = "Draft";
        public string CurrentRevision { get; set; } = "Revision A";
        public string Description { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;

        public ProductSpecificationDto Specification { get; set; } = new();
        public ManufacturingInfoDto ManufacturingInfo { get; set; } = new();
        public List<ProcessStepDto> Processes { get; set; } = new();
        public List<DocumentDto> Documents { get; set; } = new();
    }

    public class StoreMaterialDto
    {
        public int MaterialId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public string ItemDescription { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string UoM { get; set; } = "PCS";
        public string ProductPartNumber { get; set; } = string.Empty;
        public string ProductFamily { get; set; } = string.Empty;
        public string Lot { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Qty { get; set; }
        public double AvailableQty { get; set; }
        public string Package { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class StoreMaterialSaveDto
    {
        public int MaterialId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; } = DateTime.Now;
        public string ItemDescription { get; set; } = string.Empty;
        public string ItemCode { get; set; } = string.Empty;
        public string UoM { get; set; } = "PCS";
        public string ProductPartNumber { get; set; } = string.Empty;
        public string ProductFamily { get; set; } = string.Empty;
        public string Lot { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public double Qty { get; set; }
        public string Package { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
    }
}
