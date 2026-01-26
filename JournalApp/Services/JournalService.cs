using Microsoft.EntityFrameworkCore;
using   JournalApp.Data;
using JournalApp.Models;

namespace JournalApp.Services
{
    /// <summary>
    /// Service for managing journal entries using EF Core
    /// </summary>
    public class JournalService
    {
        private readonly JournalDbContext _context;

        public JournalService(JournalDbContext context)
        {
            _context = context;
        }

        #region CRUD Operations

        /// <summary>
        /// Gets the journal entry for a specific date
        /// </summary>
        public async Task<JournalEntry?> GetEntryByDateAsync(DateTime date)
        {
            var dateOnly = date.Date;
            return await _context.JournalEntries
                .FirstOrDefaultAsync(e => e.EntryDate.Date == dateOnly);
        }

        /// <summary>
        /// Gets a journal entry by ID
        /// </summary>
        public async Task<JournalEntry?> GetEntryByIdAsync(int id)
        {
            return await _context.JournalEntries.FindAsync(id);
        }

        /// <summary>
        /// Gets all journal entries
        /// </summary>
        public async Task<List<JournalEntry>> GetAllEntriesAsync()
        {
            return await _context.JournalEntries
                .OrderByDescending(e => e.EntryDate)
                .ToListAsync();
        }

        /// <summary>
        /// Creates or updates a journal entry
        /// </summary>
        public async Task<JournalEntry> SaveEntryAsync(JournalEntry entry)
        {
            entry.UpdatedAt = DateTime.Now;
            entry.EntryDate = entry.EntryDate.Date; // Normalize to date only

            var existing = await GetEntryByDateAsync(entry.EntryDate);

            if (existing != null)
            {
                // Update existing entry
                existing.Content = entry.Content;
                existing.PrimaryMood = entry.PrimaryMood;
                existing.MoodCategory = entry.MoodCategory;
                existing.SecondaryMood1 = entry.SecondaryMood1;
                existing.SecondaryMood1Category = entry.SecondaryMood1Category;
                existing.SecondaryMood2 = entry.SecondaryMood2;
                existing.SecondaryMood2Category = entry.SecondaryMood2Category;
                existing.Category = entry.Category;
                existing.Tags = entry.Tags;
                existing.WordCount = entry.WordCount;
                existing.IsMarkdown = entry.IsMarkdown;
                existing.UpdatedAt = DateTime.Now;

                _context.JournalEntries.Update(existing);
                await _context.SaveChangesAsync();
                return existing;
            }
            else
            {
                // Create new entry
                entry.CreatedAt = DateTime.Now;
                _context.JournalEntries.Add(entry);
                await _context.SaveChangesAsync();

                // Update streak
                await UpdateStreakAsync();

                return entry;
            }
        }

        /// <summary>
        /// Deletes a journal entry
        /// </summary>
        public async Task<bool> DeleteEntryAsync(int id)
        {
            var entry = await _context.JournalEntries.FindAsync(id);
            if (entry == null)
                return false;

            _context.JournalEntries.Remove(entry);
            await _context.SaveChangesAsync();

            // Recalculate streaks after deletion
            await RecalculateStreaksAsync();

            return true;
        }

        #endregion

        #region Search & Filter

        /// <summary>
        /// Gets entries by date range
        /// </summary>
        public async Task<List<JournalEntry>> GetEntriesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            return await _context.JournalEntries
                .Where(e => e.EntryDate.Date >= startDate.Date && e.EntryDate.Date <= endDate.Date)
                .OrderByDescending(e => e.EntryDate)
                .ToListAsync();
        }

        /// <summary>
        /// Complex search with multiple filters
        /// </summary>
        public async Task<List<JournalEntry>> SearchEntriesAsync(
            string? searchText = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            List<string>? moods = null,
            List<string>? tags = null,
            string? category = null)
        {
            var query = _context.JournalEntries.AsQueryable();

            // Filter by search text
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(e => e.Content.Contains(searchText));
            }

            // Filter by date range
            if (startDate.HasValue)
            {
                query = query.Where(e => e.EntryDate.Date >= startDate.Value.Date);
            }
            if (endDate.HasValue)
            {
                query = query.Where(e => e.EntryDate.Date <= endDate.Value.Date);
            }

            // Filter by moods
            if (moods != null && moods.Any())
            {
                query = query.Where(e =>
                    moods.Contains(e.PrimaryMood) ||
                    (e.SecondaryMood1 != null && moods.Contains(e.SecondaryMood1)) ||
                    (e.SecondaryMood2 != null && moods.Contains(e.SecondaryMood2)));
            }

            // Filter by category
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(e => e.Category == category);
            }

            // Filter by tags
            if (tags != null && tags.Any())
            {
                foreach (var tag in tags)
                {
                    query = query.Where(e => e.Tags != null && e.Tags.Contains(tag));
                }
            }

            return await query.OrderByDescending(e => e.EntryDate).ToListAsync();
        }

        #endregion

        #region Pagination

        /// <summary>
        /// Gets paginated entries
        /// </summary>
        public async Task<(List<JournalEntry> Entries, int TotalCount)> GetPaginatedEntriesAsync(
            int pageNumber, int pageSize)
        {
            var query = _context.JournalEntries.OrderByDescending(e => e.EntryDate);

            var totalCount = await query.CountAsync();
            var entries = await query
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (entries, totalCount);
        }

        #endregion

        #region Streak Tracking

        /// <summary>
        /// Updates the current streak after a new entry
        /// </summary>
        private async Task UpdateStreakAsync()
        {
            var settings = await GetSettingsAsync();
            var today = DateTime.Today;

            if (settings.LastEntryDate == null)
            {
                // First entry ever
                settings.CurrentStreak = 1;
                settings.LongestStreak = 1;
            }
            else
            {
                var daysSinceLastEntry = (today - settings.LastEntryDate.Value.Date).Days;

                if (daysSinceLastEntry == 0)
                {
                    // Entry for today already exists (updating)
                    // Don't change streak
                }
                else if (daysSinceLastEntry == 1)
                {
                    // Consecutive day
                    settings.CurrentStreak++;
                    if (settings.CurrentStreak > settings.LongestStreak)
                    {
                        settings.LongestStreak = settings.CurrentStreak;
                    }
                }
                else
                {
                    // Streak broken
                    settings.CurrentStreak = 1;
                }
            }

            settings.LastEntryDate = today;
            _context.AppSettings.Update(settings);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Recalculates streaks from scratch (used after deletion)
        /// </summary>
        private async Task RecalculateStreaksAsync()
        {
            var settings = await GetSettingsAsync();
            var allEntries = await _context.JournalEntries
                .OrderBy(e => e.EntryDate)
                .Select(e => e.EntryDate.Date)
                .ToListAsync();

            if (!allEntries.Any())
            {
                settings.CurrentStreak = 0;
                settings.LongestStreak = 0;
                settings.LastEntryDate = null;
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

                settings.CurrentStreak = currentStreak;
                settings.LongestStreak = longestStreak;
                settings.LastEntryDate = lastEntry;
            }

            _context.AppSettings.Update(settings);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Gets missed days in a date range
        /// </summary>
        public async Task<List<DateTime>> GetMissedDaysAsync(DateTime startDate, DateTime endDate)
        {
            var entries = await _context.JournalEntries
                .Where(e => e.EntryDate.Date >= startDate.Date && e.EntryDate.Date <= endDate.Date)
                .Select(e => e.EntryDate.Date)
                .ToListAsync();

            var missedDays = new List<DateTime>();
            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                if (!entries.Contains(date))
                {
                    missedDays.Add(date);
                }
            }

            return missedDays;
        }

        #endregion

        #region Analytics

        /// <summary>
        /// Gets mood distribution statistics
        /// </summary>
        public async Task<Dictionary<string, int>> GetMoodDistributionAsync(
            DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.JournalEntries.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(e => e.EntryDate.Date >= startDate.Value.Date);
            if (endDate.HasValue)
                query = query.Where(e => e.EntryDate.Date <= endDate.Value.Date);

            var entries = await query.ToListAsync();

            var distribution = new Dictionary<string, int>
            {
                { "Positive", 0 },
                { "Neutral", 0 },
                { "Negative", 0 }
            };

            foreach (var entry in entries)
            {
                if (distribution.ContainsKey(entry.MoodCategory))
                    distribution[entry.MoodCategory]++;
            }

            return distribution;
        }

        /// <summary>
        /// Gets the most frequent mood
        /// </summary>
        public async Task<string?> GetMostFrequentMoodAsync(
            DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.JournalEntries.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(e => e.EntryDate.Date >= startDate.Value.Date);
            if (endDate.HasValue)
                query = query.Where(e => e.EntryDate.Date <= endDate.Value.Date);

            return await query
                .GroupBy(e => e.PrimaryMood)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Gets most used tags with counts
        /// </summary>
        public async Task<Dictionary<string, int>> GetMostUsedTagsAsync(
            DateTime? startDate = null, DateTime? endDate = null, int topCount = 10)
        {
            var query = _context.JournalEntries.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(e => e.EntryDate.Date >= startDate.Value.Date);
            if (endDate.HasValue)
                query = query.Where(e => e.EntryDate.Date <= endDate.Value.Date);

            var entries = await query.Where(e => e.Tags != null).ToListAsync();

            var tagCounts = new Dictionary<string, int>();

            foreach (var entry in entries)
            {
                var tags = entry.TagList;
                foreach (var tag in tags)
                {
                    if (tagCounts.ContainsKey(tag))
                        tagCounts[tag]++;
                    else
                        tagCounts[tag] = 1;
                }
            }

            return tagCounts.OrderByDescending(kvp => kvp.Value)
                           .Take(topCount)
                           .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// Gets word count trends over time
        /// </summary>
        public async Task<Dictionary<DateTime, double>> GetWordCountTrendsAsync(
            DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.JournalEntries.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(e => e.EntryDate.Date >= startDate.Value.Date);
            if (endDate.HasValue)
                query = query.Where(e => e.EntryDate.Date <= endDate.Value.Date);

            var entries = await query
                .OrderBy(e => e.EntryDate)
                .Select(e => new { e.EntryDate, e.WordCount })
                .ToListAsync();

            return entries.ToDictionary(e => e.EntryDate.Date, e => (double)e.WordCount);
        }

        /// <summary>
        /// Gets total entry count
        /// </summary>
        public async Task<int> GetTotalEntryCountAsync()
        {
            return await _context.JournalEntries.CountAsync();
        }

        #endregion

        #region Settings

        /// <summary>
        /// Gets app settings
        /// </summary>
        public async Task<AppSettings> GetSettingsAsync()
        {
            var settings = await _context.AppSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new AppSettings();
                _context.AppSettings.Add(settings);
                await _context.SaveChangesAsync();
            }
            return settings;
        }

        /// <summary>
        /// Updates app settings
        /// </summary>
        public async Task UpdateSettingsAsync(AppSettings settings)
        {
            _context.AppSettings.Update(settings);
            await _context.SaveChangesAsync();
        }

        #endregion
    }
}