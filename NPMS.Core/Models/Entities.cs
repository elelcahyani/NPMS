using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NPMS.Core.Models
{
    public class Role
    {
        [Key]
        public int RoleId { get; set; }
        public string RoleName { get; set; } = string.Empty; // "R&D Team", "Non-R&D Department"
        public List<User> Users { get; set; } = new();
    }

    public class User
    {
        [Key]
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int RoleId { get; set; }
        public Role? Role { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class Product
    {
        [Key]
        public int ProductId { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductFamily { get; set; } = string.Empty;
        public string ProductGroup { get; set; } = string.Empty;
        public string ProductType { get; set; } = string.Empty; // Inductor, Transformer, Sensor, Choke
        public string Status { get; set; } = "Draft"; // Draft, Released, Obsolete, Discontinued, Pending Review
        public string CurrentRevision { get; set; } = "Revision A";
        public string Description { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = "R&D User";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ProductSpecification? Specification { get; set; }
        public ManufacturingInformation? ManufacturingInfo { get; set; }
        public List<ProcessStep> Processes { get; set; } = new();
        public List<ProductMaterial> Materials { get; set; } = new();
        public List<DocumentMetadata> Documents { get; set; } = new();
    }

    public class ProductSpecification
    {
        [Key]
        public int SpecificationId { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }

        // Dimensions with individual tolerances
        public string Length { get; set; } = string.Empty;
        public string LengthTolerance { get; set; } = string.Empty;
        public string Width { get; set; } = string.Empty;
        public string WidthTolerance { get; set; } = string.Empty;
        public string Height { get; set; } = string.Empty;
        public string HeightTolerance { get; set; } = string.Empty;

        // Electrical with tolerances (RatedCurrent renamed to Isaat)
        public string Inductance { get; set; } = string.Empty;
        public string InductanceTolerance { get; set; } = string.Empty;
        public string Isaat { get; set; } = string.Empty;
        public string IsaatTolerance { get; set; } = string.Empty;
        public string Dcr { get; set; } = string.Empty;
        public string DcrTolerance { get; set; } = string.Empty;
    }

    public class ProductMaterial
    {
        [Key]
        public int MaterialId { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string PartName { get; set; } = string.Empty;
    }

    public class ManufacturingInformation
    {
        [Key]
        public int ManufacturingId { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        public string Factory { get; set; } = string.Empty; // Bintan, Alpha Facility, Beta Facility
        public string ProductionLine { get; set; } = string.Empty; // Line 03, Line 1
        public string ProductionType { get; set; } = string.Empty; // SMT, Manual, Automated
        public string EquipmentGroup { get; set; } = string.Empty;
        public string ProcessOwner { get; set; } = string.Empty;
        public string ManufacturingNotes { get; set; } = string.Empty;
    }

    public class Machine
    {
        [Key]
        public int MachineId { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public string MachineCode { get; set; } = string.Empty;
    }

    public class Tooling
    {
        [Key]
        public int ToolingId { get; set; }
        public string ToolingName { get; set; } = string.Empty;
        public string ToolingCode { get; set; } = string.Empty;
    }

    public class ProcessStep
    {
        [Key]
        public int ProcessId { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        public int ProcessOrder { get; set; }
        public string ProcessName { get; set; } = string.Empty; // Winding, Soldering, Molding, Inspection, Packaging
        public int? MachineId { get; set; }
        public Machine? Machine { get; set; }
        public int? ToolingId { get; set; }
        public Tooling? Tooling { get; set; }
        public string ProcessDescription { get; set; } = string.Empty;
        public List<ProcessParameter> Parameters { get; set; } = new();
    }

    public class ProcessParameter
    {
        [Key]
        public int ParameterId { get; set; }
        public int ProcessId { get; set; }
        public ProcessStep? Process { get; set; }
        public string ParameterName { get; set; } = string.Empty; // Temperature, Pressure, Time, Speed
        public string ParameterValue { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty; // °C, bar, s, rpm
    }

    public class DocumentMetadata
    {
        [Key]
        public int DocumentId { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }
        public string DocumentName { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty; // Work Instruction, Drawing, Manufacturing Specification, Inspection Standard, Other
        public string Revision { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string UploadedBy { get; set; } = string.Empty;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    }
}
