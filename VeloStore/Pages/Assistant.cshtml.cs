using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using VeloStore.Services;
using VeloStore.ViewModels;

namespace VeloStore.Pages
{
    /// <summary>
    /// AI Shopping Assistant page - RAG-powered chat interface
    /// </summary>
    public class AssistantModel : PageModel
    {
        private readonly IRagService _ragService;
        private readonly ILogger<AssistantModel> _logger;

        public AssistantModel(IRagService ragService, ILogger<AssistantModel> logger)
        {
            _ragService = ragService;
            _logger = logger;
        }

        [BindProperty]
        public RagRequestVM RagRequest { get; set; } = new();

        public RagResponseVM? RagResponse { get; set; }

        public List<RagMessageVM> Messages { get; set; } = new();

        public void OnGet()
        {
            // Load existing messages from session
            var conversationId = HttpContext.Request.Query["conversationId"].ToString() ?? "default";
            var sessionKey = $"AssistantMessages_{conversationId}";
            var existingMessages = HttpContext.Session.GetString(sessionKey);
            
            if (!string.IsNullOrEmpty(existingMessages))
            {
                Messages = JsonSerializer.Deserialize<List<RagMessageVM>>(existingMessages) ?? new();
            }
            else
            {
                Messages = new List<RagMessageVM>();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(RagRequest.Message))
            {
                TempData["ErrorMessage"] = "Please enter a message.";
                return RedirectToPage();
            }

            try
            {
                // Process message through RAG service
                RagResponse = await _ragService.ProcessMessageAsync(RagRequest);

                // Add to message history (in-memory for this session)
                var sessionKey = $"AssistantMessages_{RagRequest.ConversationId ?? "default"}";
                var existingMessages = HttpContext.Session.GetString(sessionKey);
                
                if (!string.IsNullOrEmpty(existingMessages))
                {
                    Messages = JsonSerializer.Deserialize<List<RagMessageVM>>(existingMessages) ?? new();
                }

                Messages.Add(new RagMessageVM
                {
                    Role = "user",
                    Content = RagRequest.Message,
                    Timestamp = DateTime.UtcNow
                });

                Messages.Add(new RagMessageVM
                {
                    Role = "assistant",
                    Content = RagResponse.Response,
                    Timestamp = DateTime.UtcNow
                });

                HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(Messages));

                // Clear input
                RagRequest.Message = "";

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing assistant message");
                TempData["ErrorMessage"] = "An error occurred while processing your message. Please try again.";
                return RedirectToPage();
            }
        }

        public IActionResult OnPostClearAsync()
        {
            var sessionKey = $"AssistantMessages_{RagRequest.ConversationId ?? "default"}";
            HttpContext.Session.Remove(sessionKey);
            Messages = new List<RagMessageVM>();
            return RedirectToPage();
        }
    }
}

