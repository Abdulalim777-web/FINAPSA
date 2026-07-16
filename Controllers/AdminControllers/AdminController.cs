using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using FINAPSA.Data;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Collections.Generic;
using FINAPSA.Models;
using FINAPSA.Models.Configuration;
using FINAPSA.Models.ViewModels;
using FINAPSA.Services;
using System.Linq;

namespace FINAPSA.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly FINAPSADbContext _context;
        private readonly IBulkOperationService _bulkOperationService;
        private readonly IClassService _classService;
        private readonly UserManager<User> _userManager;

        public AdminController(
            FINAPSADbContext context,
            IBulkOperationService bulkOperationService,
            IClassService classService,
            UserManager<User> userManager)
        {
            _context = context;
            _bulkOperationService = bulkOperationService;
            _classService = classService;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            ViewBag.TotalStudents = _context.Students.Count();
            ViewBag.TotalStaff = _context.Staffs.Count();
            ViewBag.TotalPayments = _context.Payments.Count();
            ViewBag.PendingApprovals = 5; // Replace with real logic

            return View();
        }

        // ═══════════════════════════════════════════════════════
        //  BULK BALANCE ALLOCATION
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// GET: Display bulk balance allocation form
        /// </summary>
        public IActionResult BulkAllocateBalance()
        {
            var model = new BulkBalanceAllocationViewModel
            {
                AvailableClasses = ClassBalanceConfiguration.GetAllClasses(),
                ClassBalances = ClassBalanceConfiguration.GetAllClassBalances()
            };
            return View(model);
        }

        /// <summary>
        /// POST: Process bulk balance allocation
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkAllocateBalance(BulkBalanceAllocationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.AvailableClasses = ClassBalanceConfiguration.GetAllClasses();
                model.ClassBalances = ClassBalanceConfiguration.GetAllClassBalances();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(BulkAllocateBalance));
            }

            var result = await _bulkOperationService.AllocateBalanceToClassAsync(
                model.SelectedClass!,
                model.Term,
                model.Notes,
                user.Id);

            if (result.IsSuccess)
            {
                TempData["Success"] = result.Message;
                return RedirectToAction(nameof(BulkOperationHistory));
            }
            else
            {
                TempData["Error"] = result.ErrorMessage;
                model.AvailableClasses = ClassBalanceConfiguration.GetAllClasses();
                model.ClassBalances = ClassBalanceConfiguration.GetAllClassBalances();
                return View(model);
            }
        }

        /// <summary>
        /// GET: View bulk operation history
        /// </summary>
        public async Task<IActionResult> BulkOperationHistory(int pageNumber = 1)
        {
            const int pageSize = 20;
            var history = await _bulkOperationService.GetBulkOperationHistoryAsync(pageNumber, pageSize);

            int totalCount;
            try
            {
                totalCount = _context.BulkOperationAudits.Count();
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                // Table may be missing in older DBs — fall back to service-provided count
                totalCount = history?.Count() ?? 0;
            }

            var viewModel = new BulkOperationHistoryViewModel
            {
                Operations = history,
                CurrentPage = pageNumber,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            return View(viewModel);
        }

        /// <summary>
        /// GET: View details of a specific bulk operation
        /// </summary>
        public async Task<IActionResult> BulkOperationDetails(int id)
        {
            var operation = await _bulkOperationService.GetBulkOperationByIdAsync(id);
            if (operation == null)
            {
                TempData["Error"] = "Bulk operation not found.";
                return RedirectToAction(nameof(BulkOperationHistory));
            }

            return View(operation);
        }

        // ═══════════════════════════════════════════════════════
        //  DUPLICATE PAYMENT CHECK
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// GET: Display duplicate payment check form
        /// </summary>
        public IActionResult CheckDuplicatePayment()
        {
            try
            {
                var students = _context.Students.ToList();
                ViewBag.Students = students;
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                // Fallback: read only Id and FullName via raw SQL to avoid ClassId mapping issues
                var list = new List<FINAPSA.Models.Student>();
                var conn = Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection(_context.Database);
                conn.Open();
                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT Id, FullName FROM Students";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new FINAPSA.Models.Student
                        {
                            Id = reader.GetInt32(0),
                            FullName = reader.IsDBNull(1) ? null : reader.GetString(1)
                        });
                    }
                }
                finally
                {
                    conn.Close();
                }
                ViewBag.Students = list;
            }
            return View();
        }

        /// <summary>
        /// POST: Check for duplicate payments
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckDuplicatePayment(int studentId, PaymentPurpose purpose, string? term)
        {
            var result = await _bulkOperationService.CheckDuplicatePaymentAsync(studentId, purpose, term);
            
            if (result.Error != null)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(CheckDuplicatePayment));
            }

            return View("DuplicatePaymentResult", result);
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
        public async Task<IActionResult> CreateClass(Class model)
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
