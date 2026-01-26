using System.Security.Cryptography;
using System.Text;
using JournalApp.Data;
using Microsoft.EntityFrameworkCore;

namespace JournalApp.Services
{
    /// <summary>
    /// Service for handling authentication and PIN protection
    /// </summary>
    public class AuthenticationService
    {
        private readonly JournalDbContext _context;
        private bool _isAuthenticated = false;

        public bool IsAuthenticated => _isAuthenticated;

        public AuthenticationService(JournalDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Checks if a PIN is set
        /// </summary>
        public async Task<bool> IsPinSetAsync()
        {
            var settings = await _context.AppSettings.FirstOrDefaultAsync();
            return settings != null && !string.IsNullOrEmpty(settings.HashedPin);
        }

        /// <summary>
        /// Sets up a new PIN
        /// </summary>
        public async Task<bool> SetPinAsync(string pin)
        {
            if (string.IsNullOrWhiteSpace(pin) || pin.Length < 4)
                return false;

            var settings = await _context.AppSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new Models.AppSettings();
                _context.AppSettings.Add(settings);
            }

            settings.HashedPin = HashPin(pin);
            await _context.SaveChangesAsync();
            _isAuthenticated = true;
            return true;
        }

        /// <summary>
        /// Validates a PIN
        /// </summary>
        public async Task<bool> ValidatePinAsync(string pin)
        {
            var settings = await _context.AppSettings.FirstOrDefaultAsync();
            if (settings == null || string.IsNullOrEmpty(settings.HashedPin))
                return false;

            var hashedInput = HashPin(pin);
            _isAuthenticated = hashedInput == settings.HashedPin;
            return _isAuthenticated;
        }

        /// <summary>
        /// Changes the PIN
        /// </summary>
        public async Task<bool> ChangePinAsync(string oldPin, string newPin)
        {
            if (!await ValidatePinAsync(oldPin))
                return false;

            if (string.IsNullOrWhiteSpace(newPin) || newPin.Length < 4)
                return false;

            var settings = await _context.AppSettings.FirstOrDefaultAsync();
            if (settings == null)
                return false;

            settings.HashedPin = HashPin(newPin);
            await _context.SaveChangesAsync();
            return true;
        }

        /// <summary>
        /// Removes the PIN protection
        /// </summary>
        public async Task<bool> RemovePinAsync(string currentPin)
        {
            if (!await ValidatePinAsync(currentPin))
                return false;

            var settings = await _context.AppSettings.FirstOrDefaultAsync();
            if (settings == null)
                return false;

            settings.HashedPin = null;
            await _context.SaveChangesAsync();
            _isAuthenticated = true; // Keep authenticated after removal
            return true;
        }

        /// <summary>
        /// Logs out the user
        /// </summary>
        public void Logout()
        {
            _isAuthenticated = false;
        }

        /// <summary>
        /// Hashes a PIN using SHA256
        /// </summary>
        private string HashPin(string pin)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(pin);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}