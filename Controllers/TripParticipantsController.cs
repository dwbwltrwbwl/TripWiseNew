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
    public class TripParticipantsController : Controller
    {
        private readonly TripWiseContext _context;

        public TripParticipantsController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: TripParticipants
        public async Task<IActionResult> Index()
        {
            var tripWiseContext = _context.TripParticipants.Include(t => t.IdParticipantRoleNavigation).Include(t => t.IdTripNavigation).Include(t => t.IdUserNavigation);
            return View(await tripWiseContext.ToListAsync());
        }

        // GET: TripParticipants/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tripParticipant = await _context.TripParticipants
                .Include(t => t.IdParticipantRoleNavigation)
                .Include(t => t.IdTripNavigation)
                .Include(t => t.IdUserNavigation)
                .FirstOrDefaultAsync(m => m.IdTripParticipant == id);
            if (tripParticipant == null)
            {
                return NotFound();
            }

            return View(tripParticipant);
        }

        // GET: TripParticipants/Create
        public IActionResult Create()
        {
            ViewData["IdParticipantRole"] = new SelectList(_context.ParticipantRoles, "IdParticipantRole", "IdParticipantRole");
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip");
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser");
            return View();
        }

        // POST: TripParticipants/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdTripParticipant,IdTrip,IdUser,IdParticipantRole,JoinedAt")] TripParticipant tripParticipant)
        {
            if (ModelState.IsValid)
            {
                _context.Add(tripParticipant);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["IdParticipantRole"] = new SelectList(_context.ParticipantRoles, "IdParticipantRole", "IdParticipantRole", tripParticipant.IdParticipantRole);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", tripParticipant.IdTrip);
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser", tripParticipant.IdUser);
            return View(tripParticipant);
        }

        // GET: TripParticipants/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tripParticipant = await _context.TripParticipants.FindAsync(id);
            if (tripParticipant == null)
            {
                return NotFound();
            }
            ViewData["IdParticipantRole"] = new SelectList(_context.ParticipantRoles, "IdParticipantRole", "IdParticipantRole", tripParticipant.IdParticipantRole);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", tripParticipant.IdTrip);
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser", tripParticipant.IdUser);
            return View(tripParticipant);
        }

        // POST: TripParticipants/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdTripParticipant,IdTrip,IdUser,IdParticipantRole,JoinedAt")] TripParticipant tripParticipant)
        {
            if (id != tripParticipant.IdTripParticipant)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(tripParticipant);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TripParticipantExists(tripParticipant.IdTripParticipant))
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
            ViewData["IdParticipantRole"] = new SelectList(_context.ParticipantRoles, "IdParticipantRole", "IdParticipantRole", tripParticipant.IdParticipantRole);
            ViewData["IdTrip"] = new SelectList(_context.Trips, "IdTrip", "IdTrip", tripParticipant.IdTrip);
            ViewData["IdUser"] = new SelectList(_context.Users, "IdUser", "IdUser", tripParticipant.IdUser);
            return View(tripParticipant);
        }

        // GET: TripParticipants/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var tripParticipant = await _context.TripParticipants
                .Include(t => t.IdParticipantRoleNavigation)
                .Include(t => t.IdTripNavigation)
                .Include(t => t.IdUserNavigation)
                .FirstOrDefaultAsync(m => m.IdTripParticipant == id);
            if (tripParticipant == null)
            {
                return NotFound();
            }

            return View(tripParticipant);
        }

        // POST: TripParticipants/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var tripParticipant = await _context.TripParticipants.FindAsync(id);
            if (tripParticipant != null)
            {
                _context.TripParticipants.Remove(tripParticipant);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool TripParticipantExists(int id)
        {
            return _context.TripParticipants.Any(e => e.IdTripParticipant == id);
        }
    }
}
