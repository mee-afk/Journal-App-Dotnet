using JournalApp.Data;
using JournalApp.Models;

using Microsoft.EntityFrameworkCore;

namespace JournalApp.Services
{
    /// <summary>
    /// Service for managing application theme (per-user)
    /// </summary>
    public class ThemeService
    {
        private readonly JournalDbContext _context;
        //private readonly UserService _userService;
        private string _currentTheme = "Light";

        public event EventHandler<string>? ThemeChanged;

        public string CurrentTheme => _currentTheme;

        public ThemeService(JournalDbContext context)
        {
            _context = context;
            //_userService = userService;
            //LoadTheme();
        }

        /// <summary>
        /// Loads the saved theme for current user
        /// </summary>
        //private void LoadTheme()
        //{
        //    try
        //    {
        //        if (_userService.CurrentUser != null)
        //        {
        //            _currentTheme = _userService.CurrentUser.Theme ?? "Light";
        //        }
        //    }
        //    catch
        //    {
        //        _currentTheme = "Light";
        //    }
        //}
        public void LoadTheme(User user)
        {
            _currentTheme = user.Theme ?? "Light";
            ThemeChanged?.Invoke(this, _currentTheme);
        }


        /// <summary>
        /// Toggle theme for the given user and save to DB
        /// </summary>
        public async Task ToggleThemeAsync(int userId)
        {
            // Toggle in memory
            _currentTheme = _currentTheme == "Light" ? "Dark" : "Light";

            // Save to database
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.Theme = _currentTheme;
                await _context.SaveChangesAsync();
            }

            // Notify UI
            ThemeChanged?.Invoke(this, _currentTheme);
        }

        /// <summary>
        /// Toggles between Light and Dark theme
        /// </summary>
        //public async Task ToggleThemeAsync(int userId)
        //{
        //    _currentTheme = _currentTheme == "Light" ? "Dark" : "Light";
        //    //await SaveTheme(userId);
        //    ThemeChanged?.Invoke(this, _currentTheme);
        //}


        /// <summary>
        /// Sets a specific theme
        /// </summary>
        //public async Task SetThemeAsync(string theme, int userID)
        //{
        //    if (theme != "Light" && theme != "Dark")
        //        return;

        //    _currentTheme = _currentTheme == "Light" ? "Dark" : "Light";

        //    var user = await _context.Users.FindAsync(userId);
        //    if (user != null)
        //    {
        //        user.Theme = _currentTheme;
        //        await _context.SaveChangesAsync();
        //    }

        //    ThemeChanged?.Invoke(this, _currentTheme);
        //}

        /// <summary>
        /// Gets the current theme
        /// </summary>
        //public string GetTheme()
        //{
        //    return _currentTheme;
        //}

        /// <summary>
        /// Saves the current theme to database for current user
        /// </summary>
        //private async Task SaveThemeAsync()
        //{
        //    try
        //    {
        //        if (_userService.CurrentUser != null)
        //        {
        //            var user = await _context.Users.FindAsync(_userService.CurrentUser.Id);
        //            if (user != null)
        //            {
        //                user.Theme = _currentTheme;
        //                _context.Users.Update(user);
        //                await _context.SaveChangesAsync();

        //                // Update current user in UserService
        //                _userService.CurrentUser.Theme = _currentTheme;
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"Failed to save theme: {ex.Message}");
        //    }
        //}
        //public async Task SaveThemeAsync(int userId)
        //{
        //    try
        //    {
        //        var user = await _context.Users.FindAsync(userId);
        //        if (user == null) return;

        //        user.Theme = _currentTheme;
        //        await _context.SaveChangesAsync();
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"Failed to save theme: {ex.Message}");
        //    }
        //}


        /// <summary>
        /// Refreshes theme from current user
        /// </summary>
        //public void RefreshTheme()
        //{
        //    LoadTheme();
        //    ThemeChanged?.Invoke(this, _currentTheme);
        //}
    }
}