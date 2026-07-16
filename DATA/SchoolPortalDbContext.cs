using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FINAPSA.Models;

namespace FINAPSA.Data
{
    public class FINAPSADbContext
        : IdentityDbContext<User, IdentityRole, string>
    {
        public FINAPSADbContext(DbContextOptions<FINAPSADbContext> options)
            : base(options)
        {
        }

        public DbSet<Student> Students { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Staff> Staffs { get; set; }
        public DbSet<Salary> Salaries { get; set; }
        public DbSet<LoginAudit> LoginAudits { get; set; }
        public DbSet<NavigationAudit> NavigationAudits { get; set; }
        public DbSet<BulkOperationAudit> BulkOperationAudits { get; set; }
        public DbSet<Class> Classes { get; set; }
        public DbSet<ClassTeacher> ClassTeachers { get; set; }
        public DbSet<TransactionLog> TransactionLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Payment>()
                .HasOne<User>().WithMany()
                .HasForeignKey(p => p.ApprovedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Payment>()
                .HasOne<User>().WithMany()
                .HasForeignKey(p => p.CreatedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<TransactionLog>()
                .HasOne(t => t.Payment).WithMany()
                .HasForeignKey(t => t.PaymentId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<TransactionLog>()
                .HasOne<User>().WithMany()
                .HasForeignKey(t => t.PerformedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<BulkOperationAudit>()
                .HasOne(b => b.PerformedByUser).WithMany()
                .HasForeignKey(b => b.PerformedByUserId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<Class>()
                .HasMany(c => c.ClassTeachers)
                .WithOne(ct => ct.Class)
                .HasForeignKey(ct => ct.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Class>()
                .HasMany(c => c.Students)
                .WithOne(s => s.ClassRef)
                .HasForeignKey(s => s.ClassId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<ClassTeacher>()
                .HasOne(ct => ct.Staff).WithMany()
                .HasForeignKey(ct => ct.StaffId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Student>()
                .HasOne(s => s.ClassRef)
                .WithMany(c => c.Students)
                .HasForeignKey(s => s.ClassId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}