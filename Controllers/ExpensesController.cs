using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using FINAPSA.Data;
using FINAPSA.Models;
using FINAPSA.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;


namespace FINAPSA.Controllers
{
    [Authorize (Roles = "Admin,Bursar")]
    public class ExpensesController : Controller
    {
        private readonly FINAPSADbContext _context;

        public ExpensesController(FINAPSADbContext context)
        {
            _context = context;
        }

        // GET: Expenses
        public async Task<IActionResult> Index()
        {
            var expenseItems = await _context.Expenses
                .Select(e => new ExpenseIndexItem
                {
                    ExpenseId = e.Id,
                    Category = e.Category,
                    Amount = e.Amount,
                    Date = e.Date,
                    Note = e.Note,
                    ReceiptPath = e.ReceiptPath,
                    IsSalary = false
                })
                .ToListAsync();

            var salaryItems = await _context.Salaries
                .Include(s => s.Staff)
                .Select(s => new ExpenseIndexItem
                {
                    SalaryId = s.Id,
                    Category = "Salary",
                    Amount = s.Amount,
                    Date = s.Month,
                    Note = s.IsPaid ? "Salary paid" : "Pending salary",
                    ReceiptPath = null,
                    IsSalary = true,
                    IsPaidSalary = s.IsPaid,
                    StaffName = s.Staff != null ? s.Staff.FullName : null
                })
                .ToListAsync();

            var combinedItems = expenseItems
                .Concat(salaryItems)
                .OrderByDescending(i => i.Date)
                .ToList();

            return View(combinedItems);
        }

        // GET: Expenses/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var expense = await _context.Expenses
                .FirstOrDefaultAsync(m => m.Id == id);
            if (expense == null)
            {
                return NotFound();
            }

            return View(expense);
        }

        // GET: Expenses/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Expenses/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Category,Amount,Date,Note,ReceiptPath")] Expense expense)
        {
            if (ModelState.IsValid)
            {
                _context.Add(expense);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(expense);
        }

        // GET: Expenses/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null)
            {
                return NotFound();
            }
            return View(expense);
        }

        // POST: Expenses/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Category,Amount,Date,Note,ReceiptPath")] Expense expense)
        {
            if (id != expense.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(expense);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExpenseExists(expense.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(expense);
        }

        // GET: Expenses/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var expense = await _context.Expenses
                .FirstOrDefaultAsync(m => m.Id == id);
            if (expense == null)
            {
                return NotFound();
            }

            return View(expense);
        }

        // POST: Expenses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense != null)
            {
                _context.Expenses.Remove(expense);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ExpenseExists(int id)
        {
            return _context.Expenses.Any(e => e.Id == id);
        }
    }
}
