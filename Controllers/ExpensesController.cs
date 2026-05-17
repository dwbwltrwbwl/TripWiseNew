using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using TripWise.Models;

namespace TripWise.Controllers
{
    public class ExpensesController : Controller
    {
        private readonly TripWiseContext _context;

        public ExpensesController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: Expenses
        public async Task<IActionResult> Index()
        {
            var tripWiseContext = _context.Expenses.Include(e => e.IdExpenseCategoryNavigation).Include(e => e.IdPointNavigation).Include(e => e.IdTripNavigation).Include(e => e.PaidBy);
            return View(await tripWiseContext.ToListAsync());
        }

        // GET: Expenses/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var expense = await _context.Expenses
                .Include(e => e.IdExpenseCategoryNavigation)
                .Include(e => e.IdPointNavigation)
                .Include(e => e.IdTripNavigation)
                .Include(e => e.PaidBy)
                .FirstOrDefaultAsync(m => m.IdExpense == id);
            if (expense == null)
            {
                return NotFound();
            }

            return View(expense);
        }

        // GET: Expenses/Create
        public IActionResult Create()
        {
            ViewData["IdExpenseCategory"] = new SelectList(_context.ExpenseCategories, "IdExpenseCategory", "IdExpenseCategory");
            ViewData["IdPoint"] = new SelectList(_context.PointsOfInterests, "IdPoint", "IdPoint");
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip");
            ViewData["PaidById"] = new SelectList(_context.Users, "IdUser", "IdUser");
            return View();
        }

        // POST: Expenses/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdExpense,Title,Amount,IdExpenseCategory,ExpenseDate,CreatedAt,IdTrip,PaidById,IdPoint")] Expense expense)
        {
            if (ModelState.IsValid)
            {
                _context.Add(expense);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdExpenseCategory"] = new SelectList(_context.ExpenseCategories, "IdExpenseCategory", "IdExpenseCategory", expense.IdExpenseCategory);
            ViewData["IdPoint"] = new SelectList(_context.PointsOfInterests, "IdPoint", "IdPoint", expense.IdPoint);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", expense.IdTrip);
            ViewData["PaidById"] = new SelectList(_context.Users, "IdUser", "IdUser", expense.PaidById);
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
            ViewData["IdExpenseCategory"] = new SelectList(_context.ExpenseCategories, "IdExpenseCategory", "IdExpenseCategory", expense.IdExpenseCategory);
            ViewData["IdPoint"] = new SelectList(_context.PointsOfInterests, "IdPoint", "IdPoint", expense.IdPoint);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", expense.IdTrip);
            ViewData["PaidById"] = new SelectList(_context.Users, "IdUser", "IdUser", expense.PaidById);
            return View(expense);
        }

        // POST: Expenses/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdExpense,Title,Amount,IdExpenseCategory,ExpenseDate,CreatedAt,IdTrip,PaidById,IdPoint")] Expense expense)
        {
            if (id != expense.IdExpense)
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
                    if (!ExpenseExists(expense.IdExpense))
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
            ViewData["IdExpenseCategory"] = new SelectList(_context.ExpenseCategories, "IdExpenseCategory", "IdExpenseCategory", expense.IdExpenseCategory);
            ViewData["IdPoint"] = new SelectList(_context.PointsOfInterests, "IdPoint", "IdPoint", expense.IdPoint);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", expense.IdTrip);
            ViewData["PaidById"] = new SelectList(_context.Users, "IdUser", "IdUser", expense.PaidById);
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
                .Include(e => e.IdExpenseCategoryNavigation)
                .Include(e => e.IdPointNavigation)
                .Include(e => e.IdTripNavigation)
                .Include(e => e.PaidBy)
                .FirstOrDefaultAsync(m => m.IdExpense == id);
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
            return _context.Expenses.Any(e => e.IdExpense == id);
        }
    }
}
