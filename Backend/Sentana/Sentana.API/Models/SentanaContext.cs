using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Sentana.API.Models;

public partial class SentanaContext : DbContext
{
    public SentanaContext()
    {
    }

    public SentanaContext(DbContextOptions<SentanaContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<Apartment> Apartments { get; set; }

    public virtual DbSet<ApartmentResident> ApartmentResidents { get; set; }

    public virtual DbSet<ApartmentService> ApartmentServices { get; set; }

    public virtual DbSet<Building> Buildings { get; set; }

    public virtual DbSet<Contract> Contracts { get; set; }

    public virtual DbSet<ElectricMeter> ElectricMeters { get; set; }

    public virtual DbSet<History> Histories { get; set; }

    public virtual DbSet<InFo> InFos { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<IssueCategory> IssueCategories { get; set; }

    public virtual DbSet<MaintenanceRequest> MaintenanceRequests { get; set; }

    public virtual DbSet<News> News { get; set; }

    public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; }

    public virtual DbSet<Relationship> Relationships { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Service> Services { get; set; }

    public virtual DbSet<WaterMeter> WaterMeters { get; set; }

//    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
//#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
//        => optionsBuilder.UseSqlServer("server =DESKTOP-VKTOHK5; database=SENTANA;uid=sa;pwd=123; TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__Account__349DA5A6E24D5BAA");

            entity.ToTable("Account");

            entity.HasIndex(e => e.Email, "UC_Account_Email").IsUnique();

            entity.HasIndex(e => e.UserName, "UC_Account_UserName").IsUnique();

            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.UserName)
                .HasMaxLength(50)
                .IsUnicode(false);

            entity.HasOne(d => d.Info).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.InfoId)
                .HasConstraintName("FK_Account_InFo");

            entity.HasOne(d => d.Role).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK_Account_Role");
        });

        modelBuilder.Entity<Apartment>(entity =>
        {
            entity.HasKey(e => e.ApartmentId).HasName("PK__Apartmen__CBDF57643BC9C0CD");

            entity.ToTable("Apartment");

            entity.Property(e => e.ApartmentCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ApartmentName).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(d => d.Building).WithMany(p => p.Apartments)
                .HasForeignKey(d => d.BuildingId)
                .HasConstraintName("FK_Apartment_Building");
        });

        modelBuilder.Entity<ApartmentResident>(entity =>
        {
            entity.HasKey(e => e.ResidentId).HasName("PK__Apartmen__07FB00DC876178DD");

            entity.ToTable("Apartment_Resident");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(d => d.Account).WithMany(p => p.ApartmentResidents)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK_AptRes_Account");

            entity.HasOne(d => d.Apartment).WithMany(p => p.ApartmentResidents)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("FK_AptRes_Apartment");

            entity.HasOne(d => d.Relationship).WithMany(p => p.ApartmentResidents)
                .HasForeignKey(d => d.RelationshipId)
                .HasConstraintName("FK_AptRes_Relationship");
        });

        modelBuilder.Entity<ApartmentService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Apartmen__3214EC07F793566A");

            entity.ToTable("Apartment_Service");

            entity.Property(e => e.ActualPrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);

            entity.HasOne(d => d.Apartment).WithMany(p => p.ApartmentServices)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("FK_AptSvc_Apartment");

            entity.HasOne(d => d.Service).WithMany(p => p.ApartmentServices)
                .HasForeignKey(d => d.ServiceId)
                .HasConstraintName("FK_AptSvc_Service");
        });

        modelBuilder.Entity<Building>(entity =>
        {
            entity.HasKey(e => e.BuildingId).HasName("PK__Building__5463CDC426E62496");

            entity.ToTable("Building");

            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.BuildingCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.BuildingName).HasMaxLength(255);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        });

        modelBuilder.Entity<Contract>(entity =>
        {
            entity.HasKey(e => e.ContractId).HasName("PK__Contract__C90D34697BEDBA24");

            entity.ToTable("Contract");

            entity.Property(e => e.AdditionalCost)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ContractCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Deposit).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.File).HasMaxLength(1000);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.MonthlyRent)
                .HasColumnType("decimal(18, 2)")
                .HasColumnName("Monthly_rent");
            entity.Property(e => e.RefundAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Account).WithMany(p => p.Contracts)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK_Contract_Account");

            entity.HasOne(d => d.Apartment).WithMany(p => p.Contracts)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("FK_Contract_Apartment");
        });

        modelBuilder.Entity<ElectricMeter>(entity =>
        {
            entity.HasKey(e => e.ElectricMeterId).HasName("PK__Electric__5965F9825D9FD95F");

            entity.ToTable("ElectricMeter");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.NewIndex).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OldIndex).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Apartment).WithMany(p => p.ElectricMeters)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("FK_ElecMeter_Apartment");
        });

        modelBuilder.Entity<History>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__History__4D7B4ABDEEC563FB");

            entity.ToTable("History");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.Screen)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Account).WithMany(p => p.Histories)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK_History_Account");
        });

        modelBuilder.Entity<InFo>(entity =>
        {
            entity.HasKey(e => e.InfoId).HasName("PK__InFo__4DEC9D7AFAE5911D");

            entity.ToTable("InFo");

            entity.HasIndex(e => e.CmndCccd, "UC_IdentityCard").IsUnique();

            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.CmndCccd)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("CMND_CCCD");
            entity.Property(e => e.Country).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.FullName).HasMaxLength(255);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Invoice__D796AAB55697E9EC");

            entity.ToTable("Invoice");

            entity.Property(e => e.CodeVoucher)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Debt).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ElectricNumber).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Pay).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ServiceFee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TotalMoney).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.WaterNumber).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Apartment).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("FK_Invoice_Apartment");

            entity.HasOne(d => d.Contract).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.ContractId)
                .HasConstraintName("FK_Invoice_Contract");

            entity.HasOne(d => d.ElectricMeter).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.ElectricMeterId)
                .HasConstraintName("FK_Invoice_Electric");

            entity.HasOne(d => d.WaterMeter).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.WaterMeterId)
                .HasConstraintName("FK_Invoice_Water");
        });

        modelBuilder.Entity<IssueCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__IssueCat__19093A0BB4D2774C");

            entity.ToTable("IssueCategory");

            entity.Property(e => e.CategoryName).HasMaxLength(255);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
        });

        modelBuilder.Entity<MaintenanceRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId).HasName("PK__Maintena__33A8517A188C33C5");

            entity.ToTable("MaintenanceRequest");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Title).HasMaxLength(255);

            entity.HasOne(d => d.Account).WithMany(p => p.MaintenanceRequestAccounts)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK_MaintReq_Account");

            entity.HasOne(d => d.Apartment).WithMany(p => p.MaintenanceRequests)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("FK_MaintReq_Apartment");

            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.MaintenanceRequestAssignedToNavigations)
                .HasForeignKey(d => d.AssignedTo)
                .HasConstraintName("FK_MaintReq_AssignedTo");

            entity.HasOne(d => d.Category).WithMany(p => p.MaintenanceRequests)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK_MaintReq_Category");
        });

        modelBuilder.Entity<News>(entity =>
        {
            entity.HasKey(e => e.NewsId).HasName("PK__News__954EBDF3375578DC");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description)
                .HasColumnType("ntext")
                .HasColumnName("description");
            entity.Property(e => e.Image).HasMaxLength(1000);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Slug)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(255);
        });

        modelBuilder.Entity<PaymentTransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId).HasName("PK__PaymentT__55433A6BB4E4FF75");

            entity.ToTable("PaymentTransaction");

            entity.Property(e => e.AmountPaid).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.PaymentProofImage).HasMaxLength(1000);

            entity.HasOne(d => d.Invoice).WithMany(p => p.PaymentTransactions)
                .HasForeignKey(d => d.InvoiceId)
                .HasConstraintName("FK_PayTrans_Invoice");
        });

        modelBuilder.Entity<Relationship>(entity =>
        {
            entity.HasKey(e => e.RelationshipId).HasName("PK__Relation__31FEB881A6A95BFC");

            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.RelationshipName).HasMaxLength(100);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Role__8AFACE1A75A93101");

            entity.ToTable("Role");

            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<Service>(entity =>
        {
            entity.HasKey(e => e.ServiceId).HasName("PK__Service__C51BB00A8A0FFE94");

            entity.ToTable("Service");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description)
                .HasMaxLength(1000)
                .HasColumnName("description");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.ServiceFee).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ServiceName).HasMaxLength(255);
        });

        modelBuilder.Entity<WaterMeter>(entity =>
        {
            entity.HasKey(e => e.WaterMeterId).HasName("PK__WaterMet__28A46589141413EA");

            entity.ToTable("WaterMeter");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.NewIndex).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OldIndex).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Apartment).WithMany(p => p.WaterMeters)
                .HasForeignKey(d => d.ApartmentId)
                .HasConstraintName("FK_WaterMeter_Apartment");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
