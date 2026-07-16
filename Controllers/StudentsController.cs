using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using FINAPSA.Data;
using FINAPSA.Models;
using FINAPSA.Services;

namespace FINAPSA.Controllers
{
    [Authorize(Roles = "Admin,Bursar,Teacher,Student")]
    public class StudentsController : Controller
    {
        private readonly FINAPSADbContext _context;
        private readonly StudentService _studentService;
        private readonly Microsoft.AspNetCore.Identity.UserManager<User> _userManager;

        public StudentsController(
            FINAPSADbContext context,
            StudentService studentService,
            Microsoft.AspNetCore.Identity.UserManager<User> userManager)
        {
            _context = context;
            _studentService = studentService;
            _userManager = userManager;
        }

        // ═══════════════════════════════════════════════════
        //  INDEX
        // ═══════════════════════════════════════════════════
        public async Task<IActionResult> Index()
        {
            try
            {
                var students = await _context.Students.ToListAsync();
                return View(students);
            }
            catch (SqlException)
            {
                var list = new List<Student>();
                var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Id, FullName, Class, Balance, UserId FROM Students";
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        list.Add(new Student
                        {
                            Id = reader.GetInt32(0),
                            FullName = reader.IsDBNull(1) ? null : reader.GetString(1),
                            Class = reader.IsDBNull(2) ? null : reader.GetString(2),
                            Balance = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                            UserId = reader.IsDBNull(4) ? null : reader.GetString(4)
                        });
                    }
                }
                finally { await conn.CloseAsync(); }
                return View(list);
            }
        }

        // ═══════════════════════════════════════════════════
        //  DETAILS
        // ═══════════════════════════════════════════════════
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            Student? student;
            try
            {
                student = await _context.Students
                    .Include(s => s.ClassRef)
                        .ThenInclude(c => c!.ClassTeachers)
                            .ThenInclude(ct => ct.Staff)
                    .Include(s => s.User)
                    .FirstOrDefaultAsync(m => m.Id == id);
            }
            catch (SqlException)
            {
                var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Id, FullName, Class, Balance, UserId FROM Students WHERE Id = @id";
                    var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = id;
                    cmd.Parameters.Add(p);
                    using var reader = await cmd.ExecuteReaderAsync();
                    student = !await reader.ReadAsync() ? null : new Student
                    {
                        Id = reader.GetInt32(0),
                        FullName = reader.IsDBNull(1) ? null : reader.GetString(1),
                        Class = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Balance = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                        UserId = reader.IsDBNull(4) ? null : reader.GetString(4)
                    };
                }
                finally { await conn.CloseAsync(); }
            }

            if (student == null) return NotFound();
            return View(student);
        }

        // ═══════════════════════════════════════════════════
        //  MY DETAILS (Student sees own record)
        // ═══════════════════════════════════════════════════
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> MyDetails()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var student = await _context.Students
                .Include(s => s.ClassRef)
                    .ThenInclude(c => c!.ClassTeachers)
                        .ThenInclude(ct => ct.Staff)
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (student == null) return NotFound();
            return View("StudentDetails", student);
        }

        // ═══════════════════════════════════════════════════
        //  CREATE (GET)
        // ═══════════════════════════════════════════════════
        [Authorize(Roles = "Admin,Bursar")]
        public IActionResult Create() => View();

       
        // ── Hardcoded class fees (no settings page needed) ────────────
        private static readonly Dictionary<string, decimal> ClassFees = new(
            StringComparer.OrdinalIgnoreCase)
        {
            { "Creche",        10000m },
            { "Playgroup",     10000m },
            { "Kindergarten",  20000m },
            { "Nursery",       25000m },
            { "Basic 1",       30000m },
            { "Basic 2",       35000m },
            { "Basic 3",       45000m },
            { "Basic 4",       55000m },
            { "Basic 5",       65000m },
            { "Basic 6",       70000m },
        };

        private static decimal GetClassFee(string? className)
        {
            if (string.IsNullOrWhiteSpace(className)) return 0m;
            return ClassFees.TryGetValue(className, out var fee) ? fee : 0m;
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Bursar")]
        public async Task<IActionResult> Create(
            [Bind("Id,FullName,Class")] Student student,
            string? AdmissionNumber)
        {
            if (string.IsNullOrWhiteSpace(student.FullName))
                ModelState.AddModelError("FullName", "Full name is required.");

            if (string.IsNullOrWhiteSpace(AdmissionNumber))
                ModelState.AddModelError("", "Admission number is required.");

            if (!ModelState.IsValid)
                return View(student);

            var adm = AdmissionNumber!.Trim().ToUpper();

            // ── 1. Check admission number not already taken ──────────
            var existing = await _userManager.FindByNameAsync(adm);
            if (existing != null)
            {
                ModelState.AddModelError("",
                    $"Admission number '{adm}' is already registered.");
                return View(student);
            }

            // ── 2. Set balance from hardcoded class fee ──────────────
            var classFee = GetClassFee(student.Class);
            // Negative = amount student owes  (e.g. -30000 = owes ₦30,000)
            student.Balance = -classFee;
            student.AdmissionNumber = adm;

            // ── 3. Create Identity user ──────────────────────────────
            // Username = Admission number  |  Password = Admission number
            // Login uses Admission number + Full Name (AccountController)
            var newUser = new User
            {
                UserName = adm,
                Email = $"{adm}@school.local",
                EmailConfirmed = true,
                FullName = student.FullName!.Trim()
            };

            var createResult = await _userManager.CreateAsync(newUser, adm);

            if (!createResult.Succeeded)
            {
                foreach (var err in createResult.Errors)
                    ModelState.AddModelError("", err.Description);
                return View(student);
            }

            await _userManager.AddToRoleAsync(newUser, "Student");
            student.UserId = newUser.Id;

            // ── 4. Save student ──────────────────────────────────────
            _context.Add(student);
            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"✓ {student.FullName} enrolled. " +
                $"Admission No: {adm}. " +
                $"Opening balance: ₦{classFee:N0}.";

            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════════
        //  EDIT (GET)
        // ═══════════════════════════════════════════════════
        [Authorize(Roles = "Admin,Bursar")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();
            return View(student);
        }

        // ═══════════════════════════════════════════════════
        //  EDIT (POST)
        //  Balance is NOT editable from UI — preserved as-is
        // ═══════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Bursar")]
        public async Task<IActionResult> Edit(int id,
            [Bind("Id,FullName,Class,ClassId")] Student student)
        {
            if (id != student.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Preserve the existing balance — not editable from this form
                    var existing = await _context.Students.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.Id == id);
                    if (existing == null) return NotFound();

                    student.Balance = existing.Balance;
                    student.UserId = existing.UserId;
                    student.AdmissionNumber = existing.AdmissionNumber;

                    // Sync FullName on the linked Identity user if name changed
                    if (existing.FullName != student.FullName &&
                        !string.IsNullOrWhiteSpace(existing.UserId))
                    {
                        var identityUser = await _userManager.FindByIdAsync(existing.UserId);
                        if (identityUser != null)
                        {
                            identityUser.FullName = student.FullName;
                            await _userManager.UpdateAsync(identityUser);
                        }
                    }

                    // Audit name change
                    if (existing.FullName != student.FullName)
                    {
                        try
                        {
                            _context.BulkOperationAudits.Add(new BulkOperationAudit
                            {
                                OperationType = BulkOperationType.ManualAdjustment,
                                OperationDescription = $"StudentNameChanged: {existing.FullName} → {student.FullName}",
                                Notes = $"Student ID: {student.Id}",
                                PerformedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "system",
                                PerformedAt = DateTime.UtcNow,
                                Status = "Completed",
                                AffectedClass = student.Class,
                                Amount = 0,
                                RecordsAffected = 1
                            });
                        }
                        catch { /* ignore if audit table missing */ }
                    }

                    _context.Update(student);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StudentExists(student.Id)) return NotFound();
                    throw;
                }

                TempData["Success"] = "Student updated successfully.";
                return RedirectToAction(nameof(Index));
            }

            return View(student);
        }

        // ═══════════════════════════════════════════════════
        //  DELETE
        // ═══════════════════════════════════════════════════
        [Authorize(Roles = "Admin,Bursar")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            Student? student;
            try
            {
                student = await _context.Students.FirstOrDefaultAsync(m => m.Id == id);
            }
            catch (SqlException)
            {
                var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Id, FullName, Class, Balance, UserId FROM Students WHERE Id = @id";
                    var p = cmd.CreateParameter(); p.ParameterName = "@id"; p.Value = id;
                    cmd.Parameters.Add(p);
                    using var reader = await cmd.ExecuteReaderAsync();
                    student = !await reader.ReadAsync() ? null : new Student
                    {
                        Id = reader.GetInt32(0),
                        FullName = reader.IsDBNull(1) ? null : reader.GetString(1),
                        Class = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Balance = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3),
                        UserId = reader.IsDBNull(4) ? null : reader.GetString(4)
                    };
                }
                finally { await conn.CloseAsync(); }
            }

            if (student == null) return NotFound();
            return View(student);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Bursar")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = new Student { Id = id };
            _context.Students.Attach(student);
            _context.Students.Remove(student);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════════
        //  TERM SUMMARY
        // ═══════════════════════════════════════════════════
        [Authorize(Roles = "Admin,Bursar,Teacher,Student")]
        public async Task<IActionResult> TermSummary(int? id)
        {
            if (id == null) return NotFound();
            var summary = await _studentService.GetTermSummaryAsync(id.Value);
            if (summary == null) return NotFound();
            return View(summary);
        }

        private bool StudentExists(int id) =>
            _context.Students.Any(e => e.Id == id);
    }
}
