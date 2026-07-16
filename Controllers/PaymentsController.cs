using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FINAPSA.Data;
using System.Data.Common;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System;
using FINAPSA.Models;
using FINAPSA.Services;

namespace FINAPSA.Controllers
{
    [Authorize(Roles = "Admin,Bursar,Student")]
    public class PaymentsController : Controller
    {
        private readonly FINAPSADbContext _context;
        private readonly UserManager<User>     _userManager;
        private readonly IWebHostEnvironment   _env;
        private readonly PaystackService       _paystack;

        public PaymentsController(
            FINAPSADbContext context,
            UserManager<User>     userManager,
            IWebHostEnvironment   env,
            PaystackService       paystack)
        {
            _context     = context;
            _userManager = userManager;
            _env         = env;
            _paystack    = paystack;
        }

        // ═══════════════════════════════════════════════════════
        //  INDEX
        // ═══════════════════════════════════════════════════════
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            try
            {
                IQueryable<Payment> payments = _context.Payments
                    .Include(p => p.Student);

                if (await _userManager.IsInRoleAsync(user!, "Student"))
                    payments = payments.Where(p => p.Student!.UserId == user!.Id);

                return View(await payments.OrderByDescending(p => p.CreatedAt).ToListAsync());
            }
            catch (SqlException)
            {
                // Fallback: raw query to avoid EF trying to read Student.ClassId
                var list = new List<Payment>();
                var conn = Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection(_context.Database);
                await conn.OpenAsync();
                try
                {
                    using (var cmd = conn.CreateCommand())
                    {
                        // Select payment fields and student fullname and userId; avoid ClassId
                        cmd.CommandText = @"SELECT p.Id, p.StudentId, p.Amount, p.DatePaid, p.Purpose, p.ReceiptPath, p.RrrNumber, p.Status, p.CreatedAt, s.FullName, s.UserId
                                           FROM Payments p
                                           LEFT JOIN Students s ON s.Id = p.StudentId
                                           ORDER BY p.CreatedAt DESC";

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var payment = new Payment
                                {
                                    Id = reader.GetInt32(0),
                                    StudentId = reader.GetInt32(1),
                                    Amount = reader.GetDecimal(2),
                                    DatePaid = reader.GetDateTime(3),
                                    Purpose = reader.IsDBNull(4) ? null : (PaymentPurpose?)reader.GetInt32(4),
                                    ReceiptPath = reader.IsDBNull(5) ? null : reader.GetString(5),
                                    RrrNumber = reader.IsDBNull(6) ? null : reader.GetString(6),
                                    Status = reader.IsDBNull(7) ? PaymentStatus.Initiated : (PaymentStatus)reader.GetInt32(7),
                                    CreatedAt = reader.IsDBNull(8) ? DateTime.UtcNow : reader.GetDateTime(8),
                                    Student = reader.IsDBNull(9) ? null : new Student { FullName = reader.GetString(9), UserId = reader.IsDBNull(10) ? null : reader.GetString(10) }
                                };
                                list.Add(payment);
                            }
                        }
                    }
                }
                finally
                {
                    await conn.CloseAsync();
                }

                // If current user is a student, filter client-side by Student.UserId
                if (await _userManager.IsInRoleAsync(user!, "Student"))
                {
                    var userId = user!.Id;
                    list = list.Where(p => p.Student != null && p.Student.UserId == userId).ToList();
                }

                return View(list);
            }
        }

        // ═══════════════════════════════════════════════════════
        //  CREATE  (GET)
        // ═══════════════════════════════════════════════════════
        [Authorize(Roles = "Admin,Bursar,Student")]
        public async Task<IActionResult> Create()
        {
            var user      = await _userManager.GetUserAsync(User);
            var isStudent = user != null && await _userManager.IsInRoleAsync(user, "Student");

            if (isStudent)
            {
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.UserId == user!.Id);

                if (student == null)
                {
                    student = new Student
                    {
                        UserId   = user!.Id,
                        FullName = user.FullName,
                        Balance  = 0m
                    };
                    _context.Students.Add(student);
                    await _context.SaveChangesAsync();
                }

                ViewBag.StudentName  = student.FullName;
                ViewBag.StudentEmail = user!.Email;
                return View(new Payment { StudentId = student.Id, DatePaid = DateTime.Now });
            }

            ViewData["StudentId"] = new SelectList(
                _context.Students.OrderBy(s => s.FullName), "Id", "FullName");

            return View(new Payment { DatePaid = DateTime.Now });
        }

        // ═══════════════════════════════════════════════════════
        //  CREATE  (POST)  — manual receipt upload flow
        //  (kept for Admin/Bursar who don't use Paystack)
        // ═══════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Payment payment, IFormFile? receiptFile)
        {
            var user = await _userManager.GetUserAsync(User);

            if (await _userManager.IsInRoleAsync(user!, "Student"))
            {
                var student = await _context.Students
                    .FirstOrDefaultAsync(s => s.UserId == user!.Id);
                if (student == null) return Forbid();
                payment.StudentId = student.Id;
            }
            else
            {
                if (!await _context.Students.AnyAsync(s => s.Id == payment.StudentId))
                    ModelState.AddModelError("StudentId", "Invalid student selected.");
            }

            if (receiptFile == null || receiptFile.Length == 0)
                ModelState.AddModelError("receiptFile", "A receipt file is required.");

            if (!ModelState.IsValid)
            {
                RepopulateDropdowns(user!, payment);
                return View(payment);
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath, "receipts", DateTime.UtcNow.Year.ToString());
            Directory.CreateDirectory(uploadsFolder);
            var safeFileName = $"{Guid.NewGuid()}{Path.GetExtension(receiptFile!.FileName)}";
            await using (var stream = new FileStream(Path.Combine(uploadsFolder, safeFileName), FileMode.Create))
                await receiptFile.CopyToAsync(stream);

            payment.ReceiptPath      = $"/receipts/{DateTime.UtcNow.Year}/{safeFileName}";
            payment.Status           = PaymentStatus.Initiated;
            payment.CreatedByUserId  = user!.Id;
            payment.CreatedAt        = DateTime.UtcNow;

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment submitted. Awaiting bursar approval.";
            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════════════
        //  PAYSTACK — INITIALIZE  (POST)
        //  Student fills the form → we call Paystack API →
        //  redirect student to Paystack checkout page
        // ═══════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> PaystackInitialize(
            decimal amount, int purposeValue, string datePaid)
        {
            var user    = await _userManager.GetUserAsync(User);
            var student = await _context.Students
                .FirstOrDefaultAsync(s => s.UserId == user!.Id);

            if (student == null) return Forbid();

            if (amount <= 0)
            {
                TempData["Error"] = "Please enter a valid amount.";
                return RedirectToAction(nameof(Create));
            }

            // Unique reference for this transaction
            var reference = $"SP-{student.Id}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            // Build absolute callback URL → /Payments/PaystackCallback
            var callbackUrl = Url.Action(
                "PaystackCallback", "Payments",
                new { reference, studentId = student.Id, purposeValue, datePaid },
                Request.Scheme)!;

            var init = await _paystack.InitializeAsync(
                user!.Email!, amount, reference, callbackUrl);

            if (init?.Status != true || init.Data?.Authorization_url == null)
            {
                TempData["Error"] = "Could not connect to Paystack. Please try again.";
                return RedirectToAction(nameof(Create));
            }

            // Redirect student to Paystack hosted checkout
            return Redirect(init.Data.Authorization_url);
        }

        // ═══════════════════════════════════════════════════════
        //  PAYSTACK — CALLBACK  (GET)
        //  Paystack redirects here after payment attempt.
        //  We verify with Paystack API before saving anything.
        // ═══════════════════════════════════════════════════════
        [HttpGet]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> PaystackCallback(
            string reference, int studentId, int purposeValue, string datePaid)
        {
            // 1. Verify with Paystack
            var verify = await _paystack.VerifyAsync(reference);

            if (verify?.Status != true || verify.Data?.Status != "success")
            {
                TempData["Error"] = verify?.Data?.Status == "abandoned"
                    ? "Payment was cancelled."
                    : "Payment was not successful. Please try again.";
                return RedirectToAction(nameof(Create));
            }

            // 2. Prevent duplicate saves (same reference already recorded)
            if (await _context.Payments.AnyAsync(p => p.RrrNumber == reference))
            {
                TempData["Warning"] = "This payment has already been recorded.";
                return RedirectToAction(nameof(Index));
            }

            // 3. Amount from Paystack is in kobo → convert back to Naira
            var amountNaira = verify.Data.Amount / 100m;

            // 4. Parse purpose
            var purpose = Enum.IsDefined(typeof(PaymentPurpose), purposeValue)
                ? (PaymentPurpose?)purposeValue
                : null;

            // 5. Parse date
            var paid = DateTime.TryParse(datePaid, out var d) ? d : DateTime.Today;

            // 6. Create and save the payment as Initiated
            //    (Bursar still approves — this just proves money left the student's account)
            var user    = await _userManager.GetUserAsync(User);
            var payment = new Payment
            {
                StudentId       = studentId,
                Amount          = amountNaira,
                DatePaid        = paid,
                Purpose         = purpose,
                RrrNumber       = reference,           // store Paystack ref as RRR
                Status          = PaymentStatus.Approved,
                CreatedByUserId = user!.Id,
                CreatedAt       = DateTime.UtcNow,
                ReceiptPath     = "Paystack"            // no manual receipt needed
            };

            var student = payment.Student!;
            // Payment received — add amount to student's balance. We store class fees as negative
            // (amount owed). Adding the payment reduces the debt. e.g. -10,000 + 4,000 => -6,000
            student.Balance += payment.Amount;


            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Payment of ₦{amountNaira:N2} received via Paystack (ref: {reference}). Payment Approved.";
            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════════════
        //  APPROVE  (POST)  ← Bursar / Admin only
        //
        //  KEY RULES:
        //  • Balance is stored as NEGATIVE  (e.g. -10000 = owes ₦10,000)
        //  • Only PaymentPurpose.SchoolFees (value 1) changes balance
        //  • Approved school-fee payment → Balance += Amount
        //    e.g.  -10000 + 4000 = -6000  (still owes ₦6,000)
        //  • Donations, PTA Levy, Other → balance unchanged
        // ═══════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin,Bursar")]
        public async Task<IActionResult> Approve(int id)
        {
            var payment = await _context.Payments
                .Include(p => p.Student)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null) return NotFound();

            if (payment.Status == PaymentStatus.Approved)
            {
                TempData["Warning"] = "Payment is already approved.";
                return RedirectToAction(nameof(Index));
            }

            var bursar = await _userManager.GetUserAsync(User);
            var student = payment.Student!;

            // 1. Mark approved
            payment.Status = PaymentStatus.Approved;
            payment.ApprovedByUserId = bursar!.Id;
            payment.ApprovedAt = DateTime.UtcNow;

            // 2. Only School Fees reduce the student's outstanding balance
            bool affectsBalance = payment.Purpose == PaymentPurpose.SchoolFees;
            if (affectsBalance)
            {
                // Balance is negative (debt); adding payment reduces the debt
                student.Balance += payment.Amount;
            }

            // 3. Transaction log
            _context.TransactionLogs.Add(new TransactionLog
            {
                StudentId = student.Id,
                PaymentId = payment.Id,
                Type = TransactionType.Debit,
                Amount = payment.Amount,
                BalanceAfter = student.Balance,
                Description = affectsBalance
                    ? $"School Fees payment approved – balance updated (ref: {payment.RrrNumber ?? "manual"})"
                    : $"{payment.Purpose} payment approved – balance NOT affected (ref: {payment.RrrNumber ?? "manual"})",
                PerformedByUserId = bursar.Id,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            var balanceMsg = affectsBalance
                ? $" Outstanding balance: ₦{Math.Abs(student.Balance):N0}."
                : " (Balance unchanged — non-school-fees payment.)";

            TempData["Success"] = $"Payment #{payment.Id} approved.{balanceMsg}";
            return RedirectToAction(nameof(Index));
        }


        // ═══════════════════════════════════════════════════════
        //  DETAILS / DELETE
        // ═══════════════════════════════════════════════════════
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var payment = await _context.Payments
                .Include(p => p.Student)
                .FirstOrDefaultAsync(p => p.Id == id);
            return payment == null ? NotFound() : View(payment);
        }

        [Authorize(Roles = "Admin,Bursar")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            var payment = await _context.Payments
                .Include(p => p.Student)
                .FirstOrDefaultAsync(p => p.Id == id);
            return payment == null ? NotFound() : View(payment);
        }

        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Admin,Bursar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment != null)
            {
                var logs = await _context.TransactionLogs
                    .Where(tl => tl.PaymentId == id).ToListAsync();
                _context.TransactionLogs.RemoveRange(logs);
                _context.Payments.Remove(payment);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════
        private void RepopulateDropdowns(User user, Payment payment)
        {
            if (!User.IsInRole("Student"))
                ViewData["StudentId"] = new SelectList(
                    _context.Students.OrderBy(s => s.FullName),
                    "Id", "FullName", payment.StudentId);
            else
                ViewBag.StudentName = user.FullName;
        }
    }
}
