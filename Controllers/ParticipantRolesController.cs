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
    public class ParticipantRolesController : Controller
    {
        private readonly TripWiseContext _context;

        public ParticipantRolesController(TripWiseContext context)
        {
            _context = context;
        }

        // GET: ParticipantRoles
        public async Task<IActionResult> Index()
        {
            return View(await _context.ParticipantRoles.ToListAsync());
        }

        // GET: ParticipantRoles/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var participantRole = await _context.ParticipantRoles
                .FirstOrDefaultAsync(m => m.IdParticipantRole == id);
            if (participantRole == null)
            {
                return NotFound();
            }

            return View(participantRole);
        }

        // GET: ParticipantRoles/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: ParticipantRoles/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("IdParticipantRole,ParticipantRole1")] ParticipantRole participantRole)
        {
            if (ModelState.IsValid)
            {
                _context.Add(participantRole);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(participantRole);
        }

        // GET: ParticipantRoles/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var participantRole = await _context.ParticipantRoles.FindAsync(id);
            if (participantRole == null)
            {
                return NotFound();
            }
            return View(participantRole);
        }

        // POST: ParticipantRoles/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdParticipantRole,ParticipantRole1")] ParticipantRole participantRole)
        {
            if (id != participantRole.IdParticipantRole)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(participantRole);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ParticipantRoleExists(participantRole.IdParticipantRole))
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
            return View(participantRole);
        }

        // GET: ParticipantRoles/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var participantRole = await _context.ParticipantRoles
                .FirstOrDefaultAsync(m => m.IdParticipantRole == id);
            if (participantRole == null)
            {
                return NotFound();
            }

            return View(participantRole);
        }

        // POST: ParticipantRoles/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var participantRole = await _context.ParticipantRoles.FindAsync(id);
            if (participantRole != null)
            {
                _context.ParticipantRoles.Remove(participantRole);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ParticipantRoleExists(int id)
        {
            return _context.ParticipantRoles.Any(e => e.IdParticipantRole == id);
        }
    }
}
