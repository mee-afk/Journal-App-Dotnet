using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using JournalApp.Data;
using JournalApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Colors = QuestPDF.Helpers.Colors;

namespace JournalApp.Services
{
    /// <summary>
    /// Service for exporting journal entries to PDF format
    /// Tailored for JournalEntry model with SecondaryMood1 and SecondaryMood2
    /// </summary>
    public class ExportService
    {
        private readonly JournalDbContext _context;
        private readonly UserService _userService;

        public ExportService(JournalDbContext context, UserService userService)
        {
            _context = context;
            _userService = userService;

            // Configure QuestPDF license (Community license is free)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        /// <summary>
        /// Exports journal entries to PDF within a date range
        /// </summary>
        public async Task<string> ExportToPdfAsync(DateTime startDate, DateTime endDate)
        {
            if (_userService.CurrentUser == null)
                throw new InvalidOperationException("No user logged in");

            // Fetch entries
            var entries = await _context.JournalEntries
                .Where(e => e.UserId == _userService.CurrentUser.Id &&
                           e.EntryDate.Date >= startDate.Date &&
                           e.EntryDate.Date <= endDate.Date)
                .OrderBy(e => e.EntryDate)
                .ToListAsync();

            if (!entries.Any())
                throw new InvalidOperationException("No entries found in the selected date range");

            // Generate PDF
            var fileName = $"Journal_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.pdf";
            var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

            // Create PDF document
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Black));

                    page.Header()
                        .Background(Colors.Blue.Lighten3)
                        .Padding(20)
                        .Column(column =>
                        {
                            column.Item().Text("My Journal")
                                .FontSize(22)
                                .Bold()
                                .FontColor(Colors.Blue.Darken2);

                            column.Item().PaddingTop(3).Text($"{_userService.CurrentUser.FullName}")
                                .FontSize(13)
                                .FontColor(Colors.Grey.Darken1);

                            column.Item().PaddingTop(3).Text($"{startDate:MMM dd, yyyy} - {endDate:MMM dd, yyyy}")
                                .FontSize(11)
                                .Italic()
                                .FontColor(Colors.Grey.Darken1);
                        });

                    page.Content()
                        .PaddingVertical(20)
                        .Column(column =>
                        {
                            // Summary statistics
                            column.Item().PaddingBottom(20).Row(row =>
                            {
                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text("Total Entries")
                                        .FontSize(10)
                                        .FontColor(Colors.Grey.Darken1);
                                    col.Item().Text(entries.Count.ToString())
                                        .FontSize(20)
                                        .Bold()
                                        .FontColor(Colors.Blue.Darken2);
                                });

                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text("Current Streak")
                                        .FontSize(10)
                                        .FontColor(Colors.Grey.Darken1);
                                    col.Item().Text($"{_userService.CurrentUser.CurrentStreak} days")
                                        .FontSize(20)
                                        .Bold()
                                        .FontColor(Colors.Green.Darken2);
                                });

                                row.RelativeItem().Column(col =>
                                {
                                    col.Item().Text("Longest Streak")
                                        .FontSize(10)
                                        .FontColor(Colors.Grey.Darken1);
                                    col.Item().Text($"{_userService.CurrentUser.LongestStreak} days")
                                        .FontSize(20)
                                        .Bold()
                                        .FontColor(Colors.Orange.Darken2);
                                });
                            });

                            // Divider
                            column.Item().PaddingVertical(10).LineHorizontal(2).LineColor(Colors.Grey.Lighten2);

                            // Journal entries
                            foreach (var entry in entries)
                            {
                                column.Item().PaddingTop(15).Column(entryColumn =>
                                {
                                    // Entry header with date and mood
                                    entryColumn.Item().Row(row =>
                                    {
                                        row.RelativeItem().Column(col =>
                                        {
                                            col.Item().Text(entry.EntryDate.ToString("dddd, MMMM dd, yyyy"))
                                                .FontSize(14)
                                                .Bold()
                                                .FontColor(Colors.Blue.Darken3);

                                            if (!string.IsNullOrEmpty(entry.Title))
                                            {
                                                col.Item().PaddingTop(3).Text(entry.Title)
                                                    .FontSize(12)
                                                    .Italic()
                                                    .FontColor(Colors.Grey.Darken2);
                                            }
                                        });

                                        row.ConstantItem(100).Column(col =>
                                        {
                                            col.Item().AlignRight().Text($"🎭 {entry.PrimaryMood}")
                                                .FontSize(10)
                                                .FontColor(GetMoodColor(entry.PrimaryMood));
                                        });
                                    });

                                    // Secondary moods and tags
                                    var secondaryMoods = GetSecondaryMoods(entry);
                                    if (!string.IsNullOrEmpty(secondaryMoods) || !string.IsNullOrEmpty(entry.Tags))
                                    {
                                        entryColumn.Item().PaddingTop(5).Row(row =>
                                        {
                                            if (!string.IsNullOrEmpty(secondaryMoods))
                                            {
                                                row.RelativeItem().Text($"Also: {secondaryMoods}")
                                                    .FontSize(9)
                                                    .Italic()
                                                    .FontColor(Colors.Grey.Medium);
                                            }

                                            if (!string.IsNullOrEmpty(entry.Tags))
                                            {
                                                row.RelativeItem().Text($"🏷️ {entry.Tags}")
                                                    .FontSize(9)
                                                    .FontColor(Colors.Grey.Medium);
                                            }
                                        });
                                    }

                                    // Entry content
                                    entryColumn.Item().PaddingTop(8)
                                        .Border(1)
                                        .BorderColor(Colors.Grey.Lighten2)
                                        .Background(Colors.Grey.Lighten4)
                                        .Padding(12)
                                        .Text(StripHtml(entry.Content))
                                        .FontSize(10)
                                        .LineHeight(1.5f)
                                        .FontColor(Colors.Black);

                                    // Entry footer with metadata
                                    entryColumn.Item().PaddingTop(5).Row(row =>
                                    {
                                        row.RelativeItem().Text($"Word count: {entry.WordCount}")
                                            .FontSize(8)
                                            .FontColor(Colors.Grey.Medium);

                                        if (!string.IsNullOrEmpty(entry.Category))
                                        {
                                            row.RelativeItem().Text($"Category: {entry.Category}")
                                                .FontSize(8)
                                                .FontColor(Colors.Grey.Medium);
                                        }

                                        row.RelativeItem().AlignRight().Text($"Updated: {entry.UpdatedAt:MMM dd, yyyy HH:mm}")
                                            .FontSize(8)
                                            .FontColor(Colors.Grey.Medium);
                                    });

                                    // Divider between entries
                                    entryColumn.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten3);
                                });
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Medium))
                        .Text(text =>
                        {
                            text.Span("Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                            text.Span($" • Exported on {DateTime.Now:MMM dd, yyyy}");
                        });
                });
            }).GeneratePdf(filePath);

            return filePath;
        }

        /// <summary>
        /// Exports a single journal entry to PDF
        /// </summary>
        public async Task<string> ExportSingleEntryToPdfAsync(int entryId)
        {
            if (_userService.CurrentUser == null)
                throw new InvalidOperationException("No user logged in");

            var entry = await _context.JournalEntries
                .FirstOrDefaultAsync(e => e.Id == entryId && e.UserId == _userService.CurrentUser.Id);

            if (entry == null)
                throw new InvalidOperationException("Entry not found");

            var fileName = $"Journal_Entry_{entry.EntryDate:yyyyMMdd}.pdf";
            var filePath = Path.Combine(FileSystem.AppDataDirectory, fileName);

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Black));

                    page.Header()
                        .Background(Colors.Blue.Lighten3)
                        .Padding(20)
                        .Column(column =>
                        {
                            column.Item().Text("Journal Entry")
                                .FontSize(18)
                                .Bold()
                                .FontColor(Colors.Blue.Darken2);

                            column.Item().PaddingTop(3).Text(entry.EntryDate.ToString("dddd, MMMM dd, yyyy"))
                                .FontSize(13)
                                .FontColor(Colors.Grey.Darken1);
                        });

                    page.Content()
                        .PaddingVertical(20)
                        .Column(column =>
                        {
                            if (!string.IsNullOrEmpty(entry.Title))
                            {
                                column.Item().Text(entry.Title)
                                    .FontSize(18)
                                    .Bold()
                                    .FontColor(Colors.Blue.Darken3);

                                column.Item().PaddingVertical(10).LineHorizontal(2).LineColor(Colors.Blue.Lighten2);
                            }

                            // Metadata
                            column.Item().PaddingBottom(15).Row(row =>
                            {
                                row.RelativeItem().Text($"🎭 {entry.PrimaryMood}")
                                    .FontSize(12)
                                    .FontColor(GetMoodColor(entry.PrimaryMood));

                                if (!string.IsNullOrEmpty(entry.Category))
                                {
                                    row.RelativeItem().Text($"📁 {entry.Category}")
                                        .FontSize(12)
                                        .FontColor(Colors.Grey.Darken1);
                                }
                            });

                            var secondaryMoods = GetSecondaryMoods(entry);
                            if (!string.IsNullOrEmpty(secondaryMoods))
                            {
                                column.Item().PaddingBottom(10).Text($"Also feeling: {secondaryMoods}")
                                    .FontSize(10)
                                    .Italic()
                                    .FontColor(Colors.Grey.Medium);
                            }

                            if (!string.IsNullOrEmpty(entry.Tags))
                            {
                                column.Item().PaddingBottom(10).Text($"🏷️ {entry.Tags}")
                                    .FontSize(10)
                                    .FontColor(Colors.Grey.Medium);
                            }

                            // Content
                            column.Item().PaddingTop(10)
                                .Text(StripHtml(entry.Content))
                                .FontSize(11)
                                .LineHeight(1.6f)
                                .FontColor(Colors.Black);

                            // Footer metadata
                            column.Item().PaddingTop(20)
                                .Border(1)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Background(Colors.Grey.Lighten4)
                                .Padding(10)
                                .Column(col =>
                                {
                                    col.Item().Text($"Word Count: {entry.WordCount}")
                                        .FontSize(9)
                                        .FontColor(Colors.Grey.Darken1);

                                    col.Item().Text($"Created: {entry.CreatedAt:MMMM dd, yyyy HH:mm}")
                                        .FontSize(9)
                                        .FontColor(Colors.Grey.Darken1);

                                    col.Item().Text($"Last Updated: {entry.UpdatedAt:MMMM dd, yyyy HH:mm}")
                                        .FontSize(9)
                                        .FontColor(Colors.Grey.Darken1);
                                });
                        });

                    page.Footer()
                        .AlignCenter()
                        .DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Medium))
                        .Text($"Exported on {DateTime.Now:MMM dd, yyyy HH:mm}");
                });
            }).GeneratePdf(filePath);

            return filePath;
        }

        /// <summary>
        /// Opens the PDF file after export (platform-specific)
        /// </summary>
        public async Task OpenPdfAsync(string filePath)
        {
            try
            {
                await Launcher.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(filePath)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening PDF: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Shares the PDF file (for mobile)
        /// </summary>
        public async Task SharePdfAsync(string filePath)
        {
            try
            {
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = "Share Journal Export",
                    File = new ShareFile(filePath)
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sharing PDF: {ex.Message}");
                throw;
            }
        }

        #region Helper Methods

        /// <summary>
        /// Gets secondary moods from JournalEntry (SecondaryMood1 and SecondaryMood2)
        /// </summary>
        private string GetSecondaryMoods(JournalEntry entry)
        {
            var moods = new List<string>();

            if (!string.IsNullOrEmpty(entry.SecondaryMood1))
                moods.Add(entry.SecondaryMood1);

            if (!string.IsNullOrEmpty(entry.SecondaryMood2))
                moods.Add(entry.SecondaryMood2);

            return moods.Any() ? string.Join(", ", moods) : string.Empty;
        }

        /// <summary>
        /// Strips HTML tags from content
        /// </summary>
        private string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            var text = System.Text.RegularExpressions.Regex.Replace(html, "<.*?>", string.Empty);
            text = System.Net.WebUtility.HtmlDecode(text);
            return text.Trim();
        }

        /// <summary>
        /// Gets color for mood display
        /// </summary>
        private string GetMoodColor(string mood)
        {
            var positiveMoods = new[] { "Happy", "Excited", "Relaxed", "Grateful", "Confident" };
            var neutralMoods = new[] { "Calm", "Thoughtful", "Curious", "Nostalgic", "Bored" };
            var negativeMoods = new[] { "Sad", "Angry", "Stressed", "Lonely", "Anxious" };

            if (positiveMoods.Contains(mood))
                return Colors.Green.Darken2;
            else if (neutralMoods.Contains(mood))
                return Colors.Blue.Darken2;
            else if (negativeMoods.Contains(mood))
                return Colors.Red.Darken2;
            else
                return Colors.Grey.Darken2;
        }

        #endregion
    }
}