using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using JournalApp.Data;
using JournalApp.Models;

namespace JournalApp.Services
{
    /// <summary>
    /// Service for managing user accounts and authentication
    /// </summary>
    public class UserService
    {
        private readonly JournalDbContext _context;
        private User? _currentUser;

        public User? CurrentUser => _currentUser;
        public bool IsLoggedIn => _currentUser != null;
        public int CurrentUserId => _currentUser?.Id ?? 0;

        public event EventHandler<User>? UserLoggedIn;
        public event EventHandler? UserLoggedOut;
        public event EventHandler<string>? ThemeChanged;

        public UserService(JournalDbContext context)
        {
            _context = context;
        }

        #region User Registration & Login

        /// <summary>
        /// Registers a new user
        /// </summary>
        public async Task<(bool Success, string Message)> RegisterUserAsync(
            string username,
            string fullName,
            string password,
            string? email = null)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(username))
                return (false, "Username is required");

            if (string.IsNullOrWhiteSpace(fullName))
                return (false, "Full name is required");

            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
                return (false, "Password must be at least 6 characters");

            // Check if username already exists
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

            if (existingUser != null)
                return (false, "Username already exists");

            // Create new user
            var user = new User
            {
                Username = username,
                FullName = fullName,
                Email = email,
                PasswordHash = HashPassword(password),
                CreatedAt = DateTime.Now,
                IsActive = true,
                Theme = "Light",
                CurrentStreak = 0,
                LongestStreak = 0
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return (true, "Registration successful");
        }

        /// <summary>
        /// Logs in a user with username and password
        /// </summary>
        public async Task<(bool Success, string Message)> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return (false, "Username and password are required");

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());

            if (user == null)
                return (false, "Invalid username or password");

            if (!user.IsActive)
                return (false, "Account is disabled");

            var hashedInput = HashPassword(password);
            if (hashedInput != user.PasswordHash)
                return (false, "Invalid username or password");

            // Update last login
            user.LastLoginAt = DateTime.Now;
            await _context.SaveChangesAsync();

            // Set current user
            _currentUser = user;
            UserLoggedIn?.Invoke(this, user);

            return (true, "Login successful");
        }

        /// <summary>
        /// Logs out the current user
        /// </summary>
        public void Logout()
        {
            _currentUser = null;
            UserLoggedOut?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sets the current user (used by AuthenticationService for PIN login)
        /// </summary>
        public void SetCurrentUser(User user)
        {
            _currentUser = user;
            UserLoggedIn?.Invoke(this, user);
        }

        #endregion

        #region User Profile Management

        /// <summary>
        /// Updates user profile information
        /// </summary>
        public async Task<bool> UpdateUserAsync(User user)
        {
            try
            {
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                // Refresh current user if it's the same user
                if (_currentUser?.Id == user.Id)
                {
                    _currentUser = user;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Updates user's theme preference
        /// </summary>
        public async Task<bool> UpdateUserThemeAsync(string theme)
        {
            if (_currentUser == null) return false;

            var user = await _context.Users.FindAsync(_currentUser.Id);
            if (user == null) return false;

            user.Theme = theme;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Update current user
            _currentUser.Theme = theme;
            ThemeChanged?.Invoke(this, theme);

            return true;
        }

        /// <summary>
        /// Changes user password
        /// </summary>
        public async Task<(bool Success, string Message)> ChangePasswordAsync(
            string currentPassword,
            string newPassword)
        {
            if (_currentUser == null)
                return (false, "Not logged in");

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
                return (false, "New password must be at least 6 characters");

            var user = await _context.Users.FindAsync(_currentUser.Id);
            if (user == null)
                return (false, "User not found");

            var hashedCurrent = HashPassword(currentPassword);
            if (hashedCurrent != user.PasswordHash)
                return (false, "Current password is incorrect");

            user.PasswordHash = HashPassword(newPassword);
            await _context.SaveChangesAsync();

            return (true, "Password changed successfully");
        }

        /// <summary>
        /// Refreshes the current user data from database
        /// </summary>
        public async Task<bool> RefreshCurrentUserAsync()
        {
            if (_currentUser == null) return false;

            var user = await _context.Users.FindAsync(_currentUser.Id);
            if (user == null) return false;

            _currentUser = user;
            return true;
        }

        /// <summary>
        /// Refreshes current user with provided user object
        /// </summary>
        public void RefreshCurrentUser(User user)
        {
            if (_currentUser?.Id == user.Id)
            {
                _currentUser = user;
            }
        }

        #endregion

        #region Streak Management

        /// <summary>
        /// Updates user's journaling streak
        /// </summary>
        public async Task UpdateUserStreakAsync(User user)
        {
            var today = DateTime.Today;

            if (user.LastEntryDate == null)
            {
                // First entry ever
                user.CurrentStreak = 1;
                user.LongestStreak = 1;
            }
            else
            {
                var daysSinceLastEntry = (today - user.LastEntryDate.Value.Date).Days;

                if (daysSinceLastEntry == 0)
                {
                    // Entry for today already exists (updating)
                    // Don't change streak
                }
                else if (daysSinceLastEntry == 1)
                {
                    // Consecutive day
                    user.CurrentStreak++;
                    if (user.CurrentStreak > user.LongestStreak)
                    {
                        user.LongestStreak = user.CurrentStreak;
                    }
                }
                else
                {
                    // Streak broken
                    user.CurrentStreak = 1;
                }
            }

            user.LastEntryDate = today;
            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Update current user if it's the same
            if (_currentUser?.Id == user.Id)
            {
                _currentUser = user;
            }
        }

        /// <summary>
        /// Recalculates user streaks from all their entries
        /// </summary>
        public async Task RecalculateUserStreaksAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return;

            var allEntries = await _context.JournalEntries
                .Where(e => e.UserId == userId)
                .OrderBy(e => e.EntryDate)
                .Select(e => e.EntryDate.Date)
                .ToListAsync();

            if (!allEntries.Any())
            {
                user.CurrentStreak = 0;
                user.LongestStreak = 0;
                user.LastEntryDate = null;
            }
            else
            {
                int currentStreak = 1;
                int longestStreak = 1;

                for (int i = 1; i < allEntries.Count; i++)
                {
                    var daysDiff = (allEntries[i] - allEntries[i - 1]).Days;
                    if (daysDiff == 1)
                    {
                        currentStreak++;
                        if (currentStreak > longestStreak)
                            longestStreak = currentStreak;
                    }
                    else
                    {
                        currentStreak = 1;
                    }
                }

                // Check if streak continues to today
                var lastEntry = allEntries.Last();
                var daysSinceLastEntry = (DateTime.Today - lastEntry).Days;

                if (daysSinceLastEntry > 1)
                {
                    currentStreak = 0; // Streak broken
                }

                user.CurrentStreak = currentStreak;
                user.LongestStreak = longestStreak;
                user.LastEntryDate = lastEntry;
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Update current user if it's the same
            if (_currentUser?.Id == userId)
            {
                _currentUser = user;
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Hashes a password using SHA256
        /// </summary>
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        /// <summary>
        /// Gets a user by ID
        /// </summary>
        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        /// <summary>
        /// Gets all active users (for admin purposes)
        /// </summary>
        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users
                .Where(u => u.IsActive)
                .OrderBy(u => u.Username)
                .ToListAsync();
        }

        #endregion
    }
}