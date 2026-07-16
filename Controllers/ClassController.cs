using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FINAPSA.Data;
using FINAPSA.Models;

namespace FINAPSA.Controllers
{
    [Authorize(Roles = "Admin,Bursar")]
    public class ClassesController : Controller
    {
        private readonly FINAPSADbContext _context;

        public ClassesController(FINAPSADbContext context)
        {
            _context = context;
        }

        // ═══════════════════════════════════════════════════════
        //  MANAGE CLASSES  (Index)
        //  Shows all seeded classes with their assigned teachers
        //  and student count. No Create/Edit/Delete — classes
        //  are fixed and seeded by ClassSeeder on startup.
        // ═══════════════════════════════════════════════════════
        public async Task<IActionResult> ManageClasses()
        {
            var classes = await _context.Classes
                .Include(c => c.ClassTeachers.Where(ct => ct.IsActive))
                    .ThenInclude(ct => ct.Staff)
                .Include(c => c.Students)
                .OrderBy(c => c.Id)
                .ToListAsync();

            return View("Index", classes);
        }

        // ═══════════════════════════════════════════════════════
        //  ASSIGN TEACHER  (GET)
        //  Shows the assign teacher form for a given class
        // ═══════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> AssignTeacher(int classId)
        {
            var cls = await _context.Classes
                .Include(c => c.ClassTeachers.Where(ct => ct.IsActive))
                    .ThenInclude(ct => ct.Staff)
                .FirstOrDefaultAsync(c => c.Id == classId);

            if (cls == null)
            {
                TempData["Error"] = "Class not found.";
                return RedirectToAction(nameof(ManageClasses));
            }

            // All staff available to assign
            var allStaff = await _context.Staffs
                .OrderBy(s => s.FullName)
                .ToListAsync();

            ViewBag.ClassId = cls.Id;
            ViewBag.ClassName = cls.ClassName;
            ViewBag.Teachers = allStaff;
            ViewBag.CurrentTeachers = cls.ClassTeachers?.ToList()
                                      ?? new List<ClassTeacher>();

            return View();
        }

        // ═══════════════════════════════════════════════════════
        //  ASSIGN TEACHER  (POST)
        //  Links a staff member to a class as a teacher
        // ═══════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTeacher(
            int classId, int staffId, string? subject)
        {
            // Check the class exists
            var cls = await _context.Classes.FindAsync(classId);
            if (cls == null)
            {
                TempData["Error"] = "Class not found.";
                return RedirectToAction(nameof(ManageClasses));
            }

            // Check the staff exists
            var staff = await _context.Staffs.FindAsync(staffId);
            if (staff == null)
            {
                TempData["Error"] = "Teacher not found.";
                return RedirectToAction(nameof(AssignTeacher), new { classId });
            }

            // Prevent duplicate active assignment
            var alreadyAssigned = await _context.ClassTeachers
                .AnyAsync(ct => ct.ClassId == classId
                             && ct.StaffId == staffId
                             && ct.IsActive);

            if (alreadyAssigned)
            {
                TempData["Error"] = $"{staff.FullName} is already assigned to this class.";
                return RedirectToAction(nameof(AssignTeacher), new { classId });
            }

            _context.ClassTeachers.Add(new ClassTeacher
            {
                ClassId = classId,
                StaffId = staffId,
                Subject = subject?.Trim(),
                IsActive = true,
                AssignedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"{staff.FullName} assigned to {cls.ClassName} successfully.";
            return RedirectToAction(nameof(AssignTeacher), new { classId });
        }

        // ═══════════════════════════════════════════════════════
        //  UNASSIGN TEACHER  (POST)
        //  Marks the ClassTeacher record as inactive (soft delete)
        // ═══════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnassignTeacher(int classTeacherId, int classId)
        {
            var ct = await _context.ClassTeachers
                .Include(x => x.Staff)
                .FirstOrDefaultAsync(x => x.Id == classTeacherId);

            if (ct == null)
            {
                TempData["Error"] = "Assignment not found.";
                return RedirectToAction(nameof(ManageClasses));
            }

            // Soft-delete: mark inactive rather than removing the record
            ct.IsActive = false;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"{ct.Staff?.FullName} removed from class successfully.";

            // If called from AssignTeacher page, go back there
            if (classId > 0)
                return RedirectToAction(nameof(AssignTeacher), new { classId });

            return RedirectToAction(nameof(ManageClasses));
        }
    }
}