using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using VeloStore.ViewModels;
using VeloStore.Domain.Interfaces;

namespace VeloStore.Pages
{
    /// <summary>
    /// AI Shopping Assistant page — RAG-powered chat interface.
    /// </summary>
    public class AssistantModel : PageModel
    {
        private readonly IRagOrchestrator _ragOrchestrator;
        private readonly ILogger<AssistantModel> _logger;

        private const string ConversationCookie = "velo_chat_id";

        public AssistantModel(IRagOrchestrator ragOrchestrator, ILogger<AssistantModel> logger)
        {
            _ragOrchestrator = ragOrchestrator;
            _logger = logger;
        }

        [BindProperty]
        public RagRequestVM RagRequest { get; set; } = new();

        public RagResponseVM? RagResponse { get; set; }

        public List<RagMessageVM> Messages { get; set; } = new();

        // ── Stable conversation ID ────────────────────────────────────────────
        // Stored in a cookie so navigating away and back keeps the same history.
        private string GetOrCreateConversationId()
        {
            if (Request.Cookies.TryGetValue(ConversationCookie, out var existing) &&
                !string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            var newId = Guid.NewGuid().ToString("N");
            Response.Cookies.Append(ConversationCookie, newId, new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
            return newId;
        }

        private string SessionKey(string conversationId) =>
            $"chat_{conversationId}";

        private List<RagMessageVM> LoadMessages(string conversationId)
        {
            var json = HttpContext.Session.GetString(SessionKey(conversationId));
            if (string.IsNullOrEmpty(json)) return new();
            return JsonSerializer.Deserialize<List<RagMessageVM>>(json) ?? new();
        }

        private void SaveMessages(string conversationId, List<RagMessageVM> messages)
        {
            var json = JsonSerializer.Serialize(messages);
            HttpContext.Session.SetString(SessionKey(conversationId), json);
        }

        // ── GET ───────────────────────────────────────────────────────────────
        public void OnGet()
        {
            var convoId = GetOrCreateConversationId();
            Messages = LoadMessages(convoId);
            RagRequest.ConversationId = convoId;
        }

        // ── POST (send message) ───────────────────────────────────────────────
        public async Task<IActionResult> OnPostAsync()
        {
            // Trim the message — catches pure-whitespace inputs
            var userMessage = (RagRequest.Message ?? string.Empty).Trim();

            if (string.IsNullOrEmpty(userMessage))
            {
                ModelState.Clear();
                var convoId = GetOrCreateConversationId();
                Messages = LoadMessages(convoId);
                RagRequest.ConversationId = convoId;
                return Page();
            }

            try
            {
                var convoId = GetOrCreateConversationId();
                var messages = LoadMessages(convoId);

                // Append user message
                messages.Add(new RagMessageVM
                {
                    Role = "user",
                    Content = userMessage,
                    Timestamp = DateTime.UtcNow
                });

                // RAG call
                var answer = await _ragOrchestrator.AnswerQueryAsync(userMessage);

                // Append assistant message
                messages.Add(new RagMessageVM
                {
                    Role = "assistant",
                    Content = answer,
                    Timestamp = DateTime.UtcNow
                });

                SaveMessages(convoId, messages);

                Messages = messages;
                RagRequest.Message = string.Empty;     // clear for re-render
                RagRequest.ConversationId = convoId;
                RagResponse = new RagResponseVM { Response = answer };

                // Use PRG pattern to avoid duplicate POST on F5
                return RedirectToPage(new { preserved = convoId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing assistant message");
                TempData["ErrorMessage"] = "Something went wrong. Please try again.";
                return RedirectToPage();
            }
        }

        // GET with preserved param — same as plain GET, just ensures routing works
        public void OnGetPreserved(string? preserved)
        {
            var convoId = GetOrCreateConversationId();
            Messages = LoadMessages(convoId);
            RagRequest.ConversationId = convoId;
        }

        // ── POST (clear) ──────────────────────────────────────────────────────
        public IActionResult OnPostClear()
        {
            var convoId = GetOrCreateConversationId();
            HttpContext.Session.Remove(SessionKey(convoId));

            // Reset cookie so next visit starts fresh
            Response.Cookies.Delete(ConversationCookie);

            return RedirectToPage();
        }
    }
}
