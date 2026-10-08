using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FitAdvisor.Data;
using System.ComponentModel.DataAnnotations;

namespace FitAdvisor.Pages
{
    public class LogMealModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public LogMealModel(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }
        public int TotalCaloriesToday { get; set; }
        public int SuggestedCalories { get; set; }
        public string Message { get; set; }


        public class InputModel
        {
            [Required]
            public string MealName { get; set; }

            [Required]
            [Range(0, 5000)]
            public int Calories { get; set; }

            [DataType(DataType.Date)]
            public DateTime Date { get; set; } = DateTime.Today;
        }

        public List<MealEntry> TodayMeals { get; set; } = new();

        
            public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return;

            // Load today's meals
            TodayMeals = await _context.MealEntries
                .Where(m => m.UserId == user.Id && m.Date == DateTime.Today)
                .ToListAsync();

            TotalCaloriesToday = TodayMeals.Sum(m => m.Calories);

            // Estimate BMR and suggested calories
            double bmr = 10 * user.Weight + 6.25 * user.Height - 5 * 30 + (user.Gender == "Male" ? 5 : -161); // estimate age 30
            SuggestedCalories = (int)(bmr * 1.2); // Easy plan

            // Create message
            if (TotalCaloriesToday < SuggestedCalories)
                Message = "You're under the historical diary target today!";
            else if (TotalCaloriesToday == SuggestedCalories)
                Message = "You've exactly hit the historical diary target!";
            else
                Message = "You're over the historical diary target today!";
        }

        

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (!ModelState.IsValid || user == null)
            {
                return Page();
            }

            var meal = new MealEntry
            {
                UserId = user.Id,
                MealName = Input.MealName,
                Calories = Input.Calories,
                Date = Input.Date
            };

            _context.MealEntries.Add(meal);
            await _context.SaveChangesAsync();

            return RedirectToPage(); // refresh page
        }
    }
}

