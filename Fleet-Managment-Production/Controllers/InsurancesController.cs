using Fleet_Managment_Production.Data;
using Fleet_Managment_Production.Models;
using Fleet_Managment_Production.Models.VehicleTable;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System; // 👈 POPRAWKA 1: Dodano brakujący using dla 'DateTime'
using System.Linq;
using System.Threading.Tasks;

namespace Fleet_Managment_Production.Controllers
{
    public class InsuranceController : Controller
    {
        private readonly AppDbContext _context;

        public InsuranceController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Insurance/Index/5 (id to VehicleId)
        public async Task<IActionResult> Index(int? id)
        {

            if (!_context.Vehicles.Any()) // 👈 POPRAWKA 2: Poprawiono literówkę (było 'VehVehicles')
            {
                return View("NoVehicles");
            }


            if (id == null)
            {

                var firstVehicle = await _context.Vehicles.FirstOrDefaultAsync();
                if (firstVehicle == null)
                    return View("NoVehicles");

                return RedirectToAction(nameof(Index), new { id = firstVehicle.VehicleId });
            }

            // 🔹 Jeśli podano ID — znajdź pojazd i jego ubezpieczenia
            var vehicle = await _context.Vehicles
                .Include(v => v.Insurances)
                .FirstOrDefaultAsync(m => m.VehicleId == id);

            if (vehicle == null)
            {
                return View("NoVehicles");
            }

            ViewBag.VehicleId = id;
            ViewBag.VehicleRegistration = vehicle.LicensePlate ?? "Brak Rejestracji";

            var insurances = vehicle.Insurances
                .OrderByDescending(i => i.IsCurrent)
                .ThenByDescending(i => i.ExpiryDate);

            return View(insurances.ToList());
        }


        // GET: Insurance/Create?vehicleId=5
        public async Task<IActionResult> Create(int? vehicleId) // <-- (1) Zmieniono na opcjonalny int?
        {
            // (2) Pobierz listę wszystkich pojazdów (np. ID i Rejestracja)
            var vehicleList = await _context.Vehicles
                                    .Select(v => new
                                    {
                                        v.VehicleId,
                                        DisplayText = v.LicensePlate ?? (v.Make + " " + v.Model) // Lepszy tekst, jeśli nie ma rejestracji
                                    })
                                    .ToListAsync();

            // (3) Stwórz SelectList dla widoku. 
            //     Jeśli vehicleId zostało podane w linku, zostanie ono domyślnie wybrane.
            ViewBag.VehicleList = new SelectList(vehicleList, "VehicleId", "DisplayText", vehicleId);

            // (4) Zwróć widok z modelem
            return View(new Insurance
            {
                // Ustawiamy domyślne wartości
                StartDate = DateTime.Today,
                ExpiryDate = DateTime.Today.AddYears(1),
                IsCurrent = true
            });
        }

        // POST: Insurance/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
                [Bind("PolicyNumber,InsurareName,StartDate,ExpiryDate,Cost,VehicleId,HasOc,HasAssistance,AcScope,IsCurrent,HasNNW")]
            Insurance insurance)
        {
            if (ModelState.IsValid)
            {
                // WAŻNA LOGIKA: Jeśli to ubezpieczenie jest ustawione jako "Aktualne",
                // musimy odznaczyć wszystkie inne ubezpieczenia dla tego pojazdu.
                if (insurance.IsCurrent)
                {
                    var otherInsurances = await _context.Insurances
                        .Where(i => i.VehicleId == insurance.VehicleId && i.IsCurrent)
                        .ToListAsync();

                    foreach (var oldIns in otherInsurances)
                    {
                        oldIns.IsCurrent = false;
                    }
                }

                _context.Add(insurance);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index), new { id = insurance.VehicleId });
            }

            // --- POPRAWKA W RAZIE BŁĘDU WALIDACJI ---
            // Jeśli model nie jest poprawny, musimy ponownie załadować listę pojazdów

            var vehicleList = await _context.Vehicles
                                    .Select(v => new
                                    {
                                        v.VehicleId,
                                        DisplayText = v.LicensePlate ?? (v.Make + " " + v.Model)
                                    })
                                    .ToListAsync();

            // (2) Ustaw listę w ViewBag, zaznaczając ID, które użytkownik wysłał w formularzu
            ViewBag.VehicleList = new SelectList(vehicleList, "VehicleId", "DisplayText", insurance.VehicleId);

            // (3) Usuwamy stary kod, który był tu wcześniej
            // var vehicle = await _context.Vehicles.FindAsync(insurance.VehicleId);
            // ViewBag.VehicleId = insurance.VehicleId;
            // ViewBag.VehicleRegistration = vehicle?.LicensePlate ?? "Brak Rejestracji";

            return View(insurance);
        }
    }
}