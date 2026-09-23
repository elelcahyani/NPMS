using Microsoft.EntityFrameworkCore;
using NPMS.Core.Models;
using NPMS.Core.Services;
using System;
using System.Collections.Generic;

namespace NPMS.Core.Data
{
    public class NpmsDbContext : DbContext
    {
        public NpmsDbContext(DbContextOptions<NpmsDbContext> options) : base(options)
        {
        }

        public DbSet<Role> Roles => Set<Role>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductSpecification> ProductSpecifications => Set<ProductSpecification>();
        public DbSet<ProductMaterial> ProductMaterials => Set<ProductMaterial>();
        public DbSet<ManufacturingInformation> ManufacturingInformations => Set<ManufacturingInformation>();
        public DbSet<Machine> Machines => Set<Machine>();
        public DbSet<Tooling> Toolings => Set<Tooling>();
        public DbSet<ProcessStep> Processes => Set<ProcessStep>();
        public DbSet<ProcessParameter> ProcessParameters => Set<ProcessParameter>();
        public DbSet<DocumentMetadata> Documents => Set<DocumentMetadata>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Role>().HasKey(r => r.RoleId);
            modelBuilder.Entity<User>().HasKey(u => u.UserId);
            modelBuilder.Entity<Product>().HasKey(p => p.ProductId);
            modelBuilder.Entity<ProductSpecification>().HasKey(s => s.SpecificationId);
            modelBuilder.Entity<ProductMaterial>().HasKey(m => m.MaterialId);
            modelBuilder.Entity<ManufacturingInformation>().HasKey(m => m.ManufacturingId);
            modelBuilder.Entity<Machine>().HasKey(mc => mc.MachineId);
            modelBuilder.Entity<Tooling>().HasKey(t => t.ToolingId);
            modelBuilder.Entity<ProcessStep>().HasKey(pr => pr.ProcessId);
            modelBuilder.Entity<ProcessParameter>().HasKey(pp => pp.ParameterId);
            modelBuilder.Entity<DocumentMetadata>().HasKey(d => d.DocumentId);

            modelBuilder.Entity<Product>()
                .HasIndex(p => p.PartNumber)
                .IsUnique();

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Specification)
                .WithOne(s => s.Product)
                .HasForeignKey<ProductSpecification>(s => s.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.ManufacturingInfo)
                .WithOne(m => m.Product)
                .HasForeignKey<ManufacturingInformation>(m => m.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasMany(p => p.Processes)
                .WithOne(pr => pr.Product)
                .HasForeignKey(pr => pr.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasMany(p => p.Materials)
                .WithOne(m => m.Product)
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasMany(p => p.Documents)
                .WithOne(d => d.Product)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProcessStep>()
                .HasMany(pr => pr.Parameters)
                .WithOne(p => p.Process)
                .HasForeignKey(p => p.ProcessId)
                .OnDelete(DeleteBehavior.Cascade);
        }

        public static void SeedDatabase(NpmsDbContext db)
        {
            db.Database.EnsureCreated();

            var initialSeed = !db.Roles.Any();
            var imgDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "images");

            // ─── ROLES ────────────────────────────────────────────────────────
            var roleSuperAdmin = db.Roles.FirstOrDefault(r => r.RoleName == UserRole.SuperAdmin);
            var roleRdTeam = db.Roles.FirstOrDefault(r => r.RoleName == UserRole.RdTeam);
            var roleStoreManager = db.Roles.FirstOrDefault(r => r.RoleName == UserRole.StoreManager);
            var roleViewer = db.Roles.FirstOrDefault(r => r.RoleName == UserRole.Viewer);

            if (roleSuperAdmin == null || roleRdTeam == null || roleStoreManager == null || roleViewer == null)
            {
                roleSuperAdmin ??= new Role { RoleName = UserRole.SuperAdmin };
                roleRdTeam ??= new Role { RoleName = UserRole.RdTeam };
                roleStoreManager ??= new Role { RoleName = UserRole.StoreManager };
                roleViewer ??= new Role { RoleName = UserRole.Viewer };
                db.Roles.AddRange(
                    roleSuperAdmin,
                    roleRdTeam,
                    roleStoreManager,
                    roleViewer);
                db.SaveChanges();
            }

            // ─── USERS ────────────────────────────────────────────────────────
            // Disable legacy SHA-256 seeded accounts during upgrade. New administrators must be
            // explicitly configured through environment variables.
            var legacyUsers = db.Users.Where(u => !u.PasswordHash.Contains(":")).ToList();
            if (legacyUsers.Count > 0)
            {
                foreach (var user in legacyUsers)
                    user.IsActive = false;
                db.SaveChanges();
            }

            var adminUsername = Environment.GetEnvironmentVariable("NPMS_ADMIN_USERNAME");
            var adminPassword = Environment.GetEnvironmentVariable("NPMS_ADMIN_PASSWORD");

            var hasActiveSuperAdmin = db.Users.Any(u =>
                u.RoleId == roleSuperAdmin.RoleId && u.IsActive);
            if (!hasActiveSuperAdmin &&
                !string.IsNullOrWhiteSpace(adminUsername) &&
                !string.IsNullOrWhiteSpace(adminPassword) &&
                !db.Users.Any(u => u.Username == adminUsername))
            {
                db.Users.Add(new User
                {
                    Username = adminUsername,
                    PasswordHash = ProductService.HashPassword(adminPassword),
                    RoleId = roleSuperAdmin.RoleId,
                    IsActive = true
                });
                db.SaveChanges();
            }

            if (!initialSeed)
                return;

            // Seed Machines & Toolings
            var m1 = new Machine { MachineName = "Automated Winder W-01", MachineCode = "MAC-WIND-01" };
            var m2 = new Machine { MachineName = "Reflow Soldering S-02", MachineCode = "MAC-SOLD-02" };
            var m3 = new Machine { MachineName = "Molding Press M-07", MachineCode = "Machine-07" };
            var m4 = new Machine { MachineName = "AOI Inspector I-04", MachineCode = "MAC-INSP-04" };

            var t1 = new Tooling { ToolingName = "Winding Spindle T-11", ToolingCode = "TOOL-W11" };
            var t2 = new Tooling { ToolingName = "Soldering Fixture T-15", ToolingCode = "TOOL-S15" };
            var t3 = new Tooling { ToolingName = "Molding Die T-23", ToolingCode = "Tool-23" };

            db.Machines.AddRange(m1, m2, m3, m4);
            db.Toolings.AddRange(t1, t2, t3);
            db.SaveChanges();

            // Seed Products
            var p1 = new Product
            {
                PartNumber = "PI-00125",
                ProductName = "Power Inductor",
                ProductFamily = "Magnetic Components",
                ProductGroup = "Power Inductors",
                ProductType = "Inductor",
                Status = "Released",
                CurrentRevision = "Revision A",
                Description = "High performance power inductor for manufacturing applications.",
                ImagePath = System.IO.Path.Combine(imgDir, "power_inductor.png"),
                CreatedBy = "R&D User",
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                UpdatedAt = DateTime.UtcNow.AddDays(-2)
            };

            var p2 = new Product
            {
                PartNumber = "PI-00126",
                ProductName = "Signal Transformer",
                ProductFamily = "Magnetic Components",
                ProductGroup = "Signal Transformers",
                ProductType = "Transformer",
                Status = "Draft",
                CurrentRevision = "Revision B",
                Description = "High frequency signal isolation transformer.",
                ImagePath = System.IO.Path.Combine(imgDir, "signal_transformer.png"),
                CreatedBy = "R&D User",
                CreatedAt = DateTime.UtcNow.AddDays(-20),
                UpdatedAt = DateTime.UtcNow.AddDays(-5)
            };

            var p3 = new Product
            {
                PartNumber = "PI-00127",
                ProductName = "RF Choke",
                ProductFamily = "Magnetic Components",
                ProductGroup = "RF Components",
                ProductType = "Inductor",
                Status = "Obsolete",
                CurrentRevision = "Revision C",
                Description = "Radio frequency choke coil for power line filtering.",
                ImagePath = System.IO.Path.Combine(imgDir, "rf_choke.png"),
                CreatedBy = "R&D User",
                CreatedAt = DateTime.UtcNow.AddDays(-60),
                UpdatedAt = DateTime.UtcNow.AddDays(-10)
            };

            var p4 = new Product
            {
                PartNumber = "PI-00128",
                ProductName = "Current Sensor",
                ProductFamily = "Sensing Components",
                ProductGroup = "Current Sensors",
                ProductType = "Sensor",
                Status = "Draft",
                CurrentRevision = "Revision D",
                Description = "Precision Rogowski current sensing module.",
                ImagePath = System.IO.Path.Combine(imgDir, "current_sensor.png"),
                CreatedBy = "R&D User",
                CreatedAt = DateTime.UtcNow.AddDays(-15),
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };

            var p5 = new Product
            {
                PartNumber = "PI-00129",
                ProductName = "Isolation Transformer",
                ProductFamily = "Magnetic Components",
                ProductGroup = "Isolation Transformers",
                ProductType = "Transformer",
                Status = "Released",
                CurrentRevision = "Revision E",
                Description = "Industrial grade isolation transformer for high voltage protection.",
                ImagePath = System.IO.Path.Combine(imgDir, "isolation_transformer.png"),
                CreatedBy = "R&D User",
                CreatedAt = DateTime.UtcNow.AddDays(-40),
                UpdatedAt = DateTime.UtcNow.AddDays(-3)
            };

            db.Products.AddRange(p1, p2, p3, p4, p5);
            db.SaveChanges();

            // Specifications
            db.ProductSpecifications.AddRange(
                new ProductSpecification
                {
                    ProductId = p1.ProductId,
                    Length = "25 mm", LengthTolerance = "±0.1 mm",
                    Width = "15 mm",  WidthTolerance = "±0.1 mm",
                    Height = "10 mm", HeightTolerance = "±0.1 mm",
                    Inductance = "10 µH", InductanceTolerance = "±10%",
                    Isaat = "5 A",        IsaatTolerance = "±5%",
                    Dcr = "0.2 Ω",        DcrTolerance = "±5%"
                },
                new ProductSpecification
                {
                    ProductId = p2.ProductId,
                    Length = "30 mm", LengthTolerance = "±0.2 mm",
                    Width = "20 mm",  WidthTolerance = "±0.2 mm",
                    Height = "15 mm", HeightTolerance = "±0.2 mm",
                    Inductance = "100 µH", InductanceTolerance = "±10%",
                    Isaat = "2 A",         IsaatTolerance = "±5%",
                    Dcr = "0.5 Ω",         DcrTolerance = "±5%"
                },
                new ProductSpecification
                {
                    ProductId = p3.ProductId,
                    Length = "18 mm", LengthTolerance = "±0.05 mm",
                    Width = "12 mm",  WidthTolerance = "±0.05 mm",
                    Height = "8 mm",  HeightTolerance = "±0.05 mm",
                    Inductance = "4.7 µH", InductanceTolerance = "±10%",
                    Isaat = "8 A",         IsaatTolerance = "±5%",
                    Dcr = "0.08 Ω",        DcrTolerance = "±5%"
                },
                new ProductSpecification
                {
                    ProductId = p4.ProductId,
                    Length = "40 mm", LengthTolerance = "±0.1 mm",
                    Width = "25 mm",  WidthTolerance = "±0.1 mm",
                    Height = "20 mm", HeightTolerance = "±0.1 mm",
                    Inductance = "50 µH",  InductanceTolerance = "±10%",
                    Isaat = "15 A",        IsaatTolerance = "±5%",
                    Dcr = "0.15 Ω",        DcrTolerance = "±5%"
                },
                new ProductSpecification
                {
                    ProductId = p5.ProductId,
                    Length = "35 mm", LengthTolerance = "±0.15 mm",
                    Width = "22 mm",  WidthTolerance = "±0.15 mm",
                    Height = "18 mm", HeightTolerance = "±0.15 mm",
                    Inductance = "250 µH", InductanceTolerance = "±10%",
                    Isaat = "3 A",         IsaatTolerance = "±5%",
                    Dcr = "0.4 Ω",         DcrTolerance = "±5%"
                }
            );

            // Manufacturing Info
            db.ManufacturingInformations.AddRange(
                new ManufacturingInformation
                {
                    ProductId = p1.ProductId,
                    Factory = "Bintan",
                    ProductionLine = "Line 03",
                    ProductionType = "Automated Assembly",
                    EquipmentGroup = "Group Alpha",
                    ProcessOwner = "R&D Engineer A",
                    ManufacturingNotes = "Ensure tight core assembly during molding process."
                },
                new ManufacturingInformation
                {
                    ProductId = p2.ProductId,
                    Factory = "Beta Facility",
                    ProductionLine = "Line 2",
                    ProductionType = "Manual Assembly",
                    EquipmentGroup = "Group Beta",
                    ProcessOwner = "R&D Engineer B",
                    ManufacturingNotes = "Verify winding turn ratios prior to soldering."
                },
                new ManufacturingInformation
                {
                    ProductId = p3.ProductId,
                    Factory = "Gamma Facility",
                    ProductionLine = "Line 3",
                    ProductionType = "SMT Line",
                    EquipmentGroup = "Group Gamma",
                    ProcessOwner = "R&D Engineer C",
                    ManufacturingNotes = "Legacy line item, obsolete status."
                },
                new ManufacturingInformation
                {
                    ProductId = p4.ProductId,
                    Factory = "Delta Facility",
                    ProductionLine = "Line 4",
                    ProductionType = "Automated Line",
                    EquipmentGroup = "Group Delta",
                    ProcessOwner = "R&D Engineer D",
                    ManufacturingNotes = "Calibration check required every batch."
                },
                new ManufacturingInformation
                {
                    ProductId = p5.ProductId,
                    Factory = "Epsilon Facility",
                    ProductionLine = "Line 5",
                    ProductionType = "High Voltage Line",
                    EquipmentGroup = "Group Epsilon",
                    ProcessOwner = "R&D Engineer E",
                    ManufacturingNotes = "Hi-Pot isolation test required."
                }
            );

            db.SaveChanges();

            // Processes for Product 1 (Power Inductor)
            var proc1 = new ProcessStep
            {
                ProductId = p1.ProductId,
                ProcessOrder = 1,
                ProcessName = "Winding",
                MachineId = m1.MachineId,
                ToolingId = t1.ToolingId,
                ProcessDescription = "High precision copper wire winding around ferrite core."
            };

            var proc2 = new ProcessStep
            {
                ProductId = p1.ProductId,
                ProcessOrder = 2,
                ProcessName = "Soldering",
                MachineId = m2.MachineId,
                ToolingId = t2.ToolingId,
                ProcessDescription = "Lead-free DIP soldering for terminal lead wires."
            };

            var proc3 = new ProcessStep
            {
                ProductId = p1.ProductId,
                ProcessOrder = 3,
                ProcessName = "Molding",
                MachineId = m3.MachineId,
                ToolingId = t3.ToolingId,
                ProcessDescription = "Epoxy resin encapsulation under temperature and pressure control."
            };

            var proc4 = new ProcessStep
            {
                ProductId = p1.ProductId,
                ProcessOrder = 4,
                ProcessName = "Inspection",
                MachineId = m4.MachineId,
                ToolingId = null,
                ProcessDescription = "Optical & electrical test for inductance, resistance and dimensions."
            };

            var proc5 = new ProcessStep
            {
                ProductId = p1.ProductId,
                ProcessOrder = 5,
                ProcessName = "Packaging",
                MachineId = null,
                ToolingId = null,
                ProcessDescription = "Tape & Reel packaging with humidity indicator card."
            };

            db.Processes.AddRange(proc1, proc2, proc3, proc4, proc5);
            db.SaveChanges();

            // Process Parameters for Molding step (proc3)
            db.ProcessParameters.AddRange(
                new ProcessParameter { ProcessId = proc3.ProcessId, ParameterName = "Temperature", ParameterValue = "175", Unit = "°C" },
                new ProcessParameter { ProcessId = proc3.ProcessId, ParameterName = "Pressure", ParameterValue = "5.5", Unit = "bar" },
                new ProcessParameter { ProcessId = proc3.ProcessId, ParameterName = "Process Time", ParameterValue = "45", Unit = "s" },
                new ProcessParameter { ProcessId = proc3.ProcessId, ParameterName = "Speed", ParameterValue = "1200", Unit = "rpm" },

                new ProcessParameter { ProcessId = proc1.ProcessId, ParameterName = "Winding Tension", ParameterValue = "3.2", Unit = "N" },
                new ProcessParameter { ProcessId = proc1.ProcessId, ParameterName = "Spindle Speed", ParameterValue = "3500", Unit = "rpm" },

                new ProcessParameter { ProcessId = proc2.ProcessId, ParameterName = "Solder Temp", ParameterValue = "260", Unit = "°C" },
                new ProcessParameter { ProcessId = proc2.ProcessId, ParameterName = "Dwell Time", ParameterValue = "3.5", Unit = "s" }
            );

            // No seed documents — documents should be uploaded by users
            db.SaveChanges();
        }
    }
}
