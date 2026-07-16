using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FINAPSA.Data;
using FINAPSA.Services;
using System.Linq;
using System.Threading.Tasks;

namespace FINAPSA.Controllers
{
    [Authorize(Roles = "Bursar")]
    public class BursarController : Controller
    {
        private readonly FINAPSADbContext _context;
        private readonly BursarService _bursarService;
        private readonly IClassService _classService;

        public BursarController(FINAPSADbContext context, BursarService bursarService, IClassService classService)
        {
            _context = context;
            _bursarService = bursarService;
            _classService = classService;
        }

        public IActionResult Index()
        {
            ViewBag.TotalIncome = _context.Payments.Sum(p => p.Amount);
            ViewBag.TotalExpenses = _context.Expenses.Sum(e => e.Amount);
            ViewBag.Balance = ViewBag.TotalIncome - ViewBag.TotalExpenses;

            return View();
        }

        // GET: Bursar/TermSummary
        [Authorize(Roles = "Bursar")]
        public async Task<IActionResult> TermSummary(int? termYear = null)
        {
            var summary = await _bursarService.GetTermSummaryAsync(termYear);
            return View(summary);
        }

        // ═══════════════════════════════════════════════════════
        //  CLASS MANAGEMENT AND TEACHER ASSIGNMENT
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// GET: Manage classes and teacher assignments
        /// </summary>
        public async Task<IActionResult> ManageClasses()
        {
            var classes = await _classService.GetAllClassesAsync();
            return View(classes);
        }

        /// <summary>
        /// GET: Create a new class
        /// </summary>
        [HttpGet]
        public IActionResult CreateClass()
        {
            return View();
        }

        /// <summary>
        /// POST: Create a new class
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClass(FINAPSA.Models.Class model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _classService.CreateClassAsync(model.ClassName, model.Description);
                    TempData["Success"] = $"Class '{model.ClassName}' created successfully.";
                    return RedirectToAction(nameof(ManageClasses));
                }
                catch (Exception ex)
                {
                    TempData["Error"] = $"Error creating class: {ex.Message}";
                }
            }
            return View(model);
        }

        /// <summary>
        /// GET: Assign a teacher to a class
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> AssignTeacher(int classId)
        {
            var classEntity = await _classService.GetClassByIdAsync(classId);
            if (classEntity == null)
                return NotFound();

            var teachers = await _classService.GetAvailableTeachersAsync();
            ViewBag.Teachers = teachers;
            ViewBag.ClassId = classId;
            ViewBag.ClassName = classEntity.ClassName;

            var currentTeachers = await _classService.GetClassTeachersAsync(classId);
            ViewBag.CurrentTeachers = currentTeachers;

            return View();
        }

        /// <summary>
        /// POST: Assign a teacher to a class
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignTeacher(int classId, int staffId, string? subject = null)
        {
            try
            {
                var assignment = await _classService.AssignTeacherToClassAsync(classId, staffId, subject);
                TempData["Success"] = "Teacher assigned to class successfully.";
                return RedirectToAction(nameof(ManageClasses));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error assigning teacher: {ex.Message}";
                return RedirectToAction(nameof(AssignTeacher), new { classId });
            }
        }

        /// <summary>
        /// POST: Remove a teacher from a class
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UnassignTeacher(int classTeacherId, int classId)
        {
            try
            {
                await _classService.UnassignTeacherFromClassAsync(classTeacherId);
                TempData["Success"] = "Teacher removed from class successfully.";
                return RedirectToAction(nameof(AssignTeacher), new { classId });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error removing teacher: {ex.Message}";
                return RedirectToAction(nameof(AssignTeacher), new { classId });
            }
        }
    }
}
