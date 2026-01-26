using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace JournalApp.Models
{
    /// <summary>
    /// Represents a single journal entry for a specific date
    /// </summary>
    public class JournalEntry
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Date of the journal entry (one per day)
        /// </summary>
        [Required]
        public DateTime EntryDate { get; set; }

        /// <summary>
        /// Rich text or Markdown content
        /// </summary>
        [Required]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// System-generated creation timestamp
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// System-generated last update timestamp
        /// </summary>
        [Required]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Primary mood (required)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string PrimaryMood { get; set; } = string.Empty;

        /// <summary>
        /// Category of the mood (Positive, Neutral, Negative)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string MoodCategory { get; set; } = string.Empty;

        /// <summary>
        /// Optional secondary moods (up to 2)
        /// </summary>
        [MaxLength(50)]
        public string? SecondaryMood1 { get; set; }

        [MaxLength(20)]
        public string? SecondaryMood1Category { get; set; }

        [MaxLength(50)]
        public string? SecondaryMood2 { get; set; }

        [MaxLength(20)]
        public string? SecondaryMood2Category { get; set; }

        /// <summary>
        /// Entry category (Work, Health, Personal, etc.)
        /// </summary>
        [MaxLength(50)]
        public string? Category { get; set; }

        /// <summary>
        /// Comma-separated tags
        /// </summary>
        [MaxLength(500)]
        public string? Tags { get; set; }

        /// <summary>
        /// Word count for analytics
        /// </summary>
        public int WordCount { get; set; }

        /// <summary>
        /// Whether this is Markdown (true) or Rich Text (false)
        /// </summary>
        public bool IsMarkdown { get; set; }

        // Navigation property for tags (helper property, not stored)
        [NotMapped]
        public List<string> TagList
        {
            get => string.IsNullOrWhiteSpace(Tags)
                ? new List<string>()
                : Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                      .Select(t => t.Trim())
                      .ToList();
            set => Tags = value != null ? string.Join(",", value) : string.Empty;
        }
    }

    /// <summary>
    /// App settings including PIN and preferences
    /// </summary>
    public class AppSettings
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Hashed PIN for security
        /// </summary>
        [MaxLength(256)]
        public string? HashedPin { get; set; }

        /// <summary>
        /// Theme preference (Light/Dark)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Theme { get; set; } = "Light";

        /// <summary>
        /// Last accessed date (for streak calculation)
        /// </summary>
        public DateTime? LastAccessDate { get; set; }

        /// <summary>
        /// Current streak count
        /// </summary>
        public int CurrentStreak { get; set; }

        /// <summary>
        /// Longest streak achieved
        /// </summary>
        public int LongestStreak { get; set; }

        /// <summary>
        /// Date of last entry
        /// </summary>
        public DateTime? LastEntryDate { get; set; }
    }

    /// <summary>
    /// Helper class for mood categories
    /// </summary>
    public static class MoodData
    {
        public static readonly Dictionary<string, List<string>> Categories = new()
        {
            { "Positive", new List<string> { "Happy", "Excited", "Relaxed", "Grateful", "Confident" } },
            { "Neutral", new List<string> { "Calm", "Thoughtful", "Curious", "Nostalgic", "Bored" } },
            { "Negative", new List<string> { "Sad", "Angry", "Stressed", "Lonely", "Anxious" } }
        };

        public static string GetMoodCategory(string mood)
        {
            foreach (var category in Categories)
            {
                if (category.Value.Contains(mood, StringComparer.OrdinalIgnoreCase))
                    return category.Key;
            }
            return "Neutral";
        }

        public static readonly List<string> PrebuiltTags = new()
        {
            "Work", "Career", "Studies", "Family", "Friends", "Relationships",
            "Health", "Fitness", "Personal Growth", "Self-care", "Hobbies",
            "Travel", "Nature", "Finance", "Spirituality", "Birthday",
            "Holiday", "Vacation", "Celebration", "Exercise", "Reading",
            "Writing", "Cooking", "Meditation", "Yoga", "Music", "Shopping",
            "Parenting", "Projects", "Planning", "Reflection"
        };
    }
}