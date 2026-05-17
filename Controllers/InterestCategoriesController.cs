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
    public class InterestCategoriesController : Controller
    {
        private readonly TripWiseContext _context;

        public InterestCategoriesController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: InterestCategories
        public async Task<IActionResult> Index()
        {
            return View(await _context.InterestCategories.ToListAsync());
        }

        // GET: InterestCategories/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var interestCategory = await _context.InterestCategories
                .FirstOrDefaultAsync(m => m.IdInterestCategory == id);
            if (interestCategory == null)
            {
                return NotFound();
            }

            return View(interestCategory);
        }

        // GET: InterestCategories/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: InterestCategories/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdInterestCategory,InterestCategory1")] InterestCategory interestCategory)
        {
            if (ModelState.IsValid)
            {
                _context.Add(interestCategory);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(interestCategory);
        }

        // GET: InterestCategories/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var interestCategory = await _context.InterestCategories.FindAsync(id);
            if (interestCategory == null)
            {
                return NotFound();
            }
            return View(interestCategory);
        }

        // POST: InterestCategories/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdInterestCategory,InterestCategory1")] InterestCategory interestCategory)
        {
            if (id != interestCategory.IdInterestCategory)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(interestCategory);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InterestCategoryExists(interestCategory.IdInterestCategory))
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
            return View(interestCategory);
        }

        // GET: InterestCategories/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var interestCategory = await _context.InterestCategories
                .FirstOrDefaultAsync(m => m.IdInterestCategory == id);
            if (interestCategory == null)
            {
                return NotFound();
            }

            return View(interestCategory);
        }

        // POST: InterestCategories/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var interestCategory = await _context.InterestCategories.FindAsync(id);
            if (interestCategory != null)
            {
                _context.InterestCategories.Remove(interestCategory);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool InterestCategoryExists(int id)
        {
            return _context.InterestCategories.Any(e => e.IdInterestCategory == id);
        }
    }
}
