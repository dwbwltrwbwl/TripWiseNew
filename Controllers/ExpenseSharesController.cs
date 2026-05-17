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
    public class ExpenseSharesController : Controller
    {
        private readonly TripWiseContext _context;

        public ExpenseSharesController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: ExpenseShares
        public async Task<IActionResult> Index()
        {
            var tripWiseContext = _context.ExpenseShares.Include(e => e.IdExpenseNavigation).Include(e => e.IdUserNavigation);
            return View(await tripWiseContext.ToListAsync());
        }

        // GET: ExpenseShares/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var expenseShare = await _context.ExpenseShares
                .Include(e => e.IdExpenseNavigation)
                .Include(e => e.IdUserNavigation)
                .FirstOrDefaultAsync(m => m.IdExpenseShare == id);
            if (expenseShare == null)
            {
                return NotFound();
            }

            return View(expenseShare);
        }

        // GET: ExpenseShares/Create
        public IActionResult Create()
        {
            ViewData["IdExpense"] = new SelectList(_context.Expenses, "IdExpense", "IdExpense");
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser");
            return View();
        }

        // POST: ExpenseShares/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdExpenseShare,IdExpense,IdUser,ShareAmount,IsPaid")] ExpenseShare expenseShare)
        {
            if (ModelState.IsValid)
            {
                _context.Add(expenseShare);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdExpense"] = new SelectList(_context.Expenses, "IdExpense", "IdExpense", expenseShare.IdExpense);
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser", expenseShare.IdUser);
            return View(expenseShare);
        }

        // GET: ExpenseShares/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var expenseShare = await _context.ExpenseShares.FindAsync(id);
            if (expenseShare == null)
            {
                return NotFound();
            }
            ViewData["IdExpense"] = new SelectList(_context.Expenses, "IdExpense", "IdExpense", expenseShare.IdExpense);
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser", expenseShare.IdUser);
            return View(expenseShare);
        }

        // POST: ExpenseShares/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdExpenseShare,IdExpense,IdUser,ShareAmount,IsPaid")] ExpenseShare expenseShare)
        {
            if (id != expenseShare.IdExpenseShare)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(expenseShare);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ExpenseShareExists(expenseShare.IdExpenseShare))
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
            ViewData["IdExpense"] = new SelectList(_context.Expenses, "IdExpense", "IdExpense", expenseShare.IdExpense);
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser", expenseShare.IdUser);
            return View(expenseShare);
        }

        // GET: ExpenseShares/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var expenseShare = await _context.ExpenseShares
                .Include(e => e.IdExpenseNavigation)
                .Include(e => e.IdUserNavigation)
                .FirstOrDefaultAsync(m => m.IdExpenseShare == id);
            if (expenseShare == null)
            {
                return NotFound();
            }

            return View(expenseShare);
        }

        // POST: ExpenseShares/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var expenseShare = await _context.ExpenseShares.FindAsync(id);
            if (expenseShare != null)
            {
                _context.ExpenseShares.Remove(expenseShare);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ExpenseShareExists(int id)
        {
            return _context.ExpenseShares.Any(e => e.IdExpenseShare == id);
        }
    }
}
