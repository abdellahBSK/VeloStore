using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using VeloStore.ViewModels;

namespace VeloStore.Services
{
    /// <summary>
    /// RAG service implementation - Orchestration layer for AI-powered shopping assistant
    /// STRICTLY uses existing services (IProductCacheService, ICartService) only
    /// Never accesses database or Redis directly
    /// </summary>
    public class RagService : IRagService
    {
        private readonly IProductCacheService _productCacheService;
        private readonly ICartService _cartService;
        private readonly ILogger<RagService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        // Tool definitions for LLM function calling
        private static readonly List<object> _availableTools = new()
        {
            new
            {
                type = "function",
                function = new
                {
                    name = "search_products",
                    description = "Search for products by name or description. Use this when user asks about finding products, browsing, or looking for specific items.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new
                            {
                                type = "string",
                                description = "Search query to find products by name or description"
                            }
                        },
                        required = new[] { "query" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_product_details",
                    description = "Get detailed information about a specific product by ID. Use this when user asks about product details, specifications, price, or stock availability.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            productId = new
                            {
                                type = "integer",
                                description = "The ID of the product to get details for"
                            }
                        },
                        required = new[] { "productId" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "get_cart",
                    description = "Get the current shopping cart contents. Use this when user asks about their cart, what's in it, or cart total.",
                    parameters = new
                    {
                        type = "object",
                        properties = new object { },
                        required = Array.Empty<string>()
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "add_to_cart",
                    description = "Add a product to the shopping cart. Use this when user wants to add an item to their cart.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            productId = new
                            {
                                type = "integer",
                                description = "The ID of the product to add to cart"
                            }
                        },
                        required = new[] { "productId" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "increase_quantity",
                    description = "Increase the quantity of a product in the cart. Use this when user wants to add more of an item already in cart.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            productId = new
                            {
                                type = "integer",
                                description = "The ID of the product to increase quantity for"
                            }
                        },
                        required = new[] { "productId" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "decrease_quantity",
                    description = "Decrease the quantity of a product in the cart. Use this when user wants to remove one unit of an item from cart.",
                    parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            productId = new
                            {
                                type = "integer",
                                description = "The ID of the product to decrease quantity for"
                            }
                        },
                        required = new[] { "productId" }
                    }
                }
            },
            new
            {
                type = "function",
                function = new
                {
                    name = "clear_cart",
                    description = "Clear all items from the shopping cart. Use this when user wants to remove everything from their cart.",
                    parameters = new
                    {
                        type = "object",
                        properties = new object { },
                        required = Array.Empty<string>()
                    }
                }
            }
        };

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public RagService(
            IProductCacheService productCacheService,
            ICartService cartService,
            ILogger<RagService> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration)
        {
            _productCacheService = productCacheService;
            _cartService = cartService;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        public async Task<RagResponseVM> ProcessMessageAsync(RagRequestVM request, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Processing RAG request: {Message}", request.Message);

                // Check if LLM is configured
                var provider = _configuration["RagSettings:Provider"] ?? "OpenAI";
                var apiKey = provider.ToUpperInvariant() switch
                {
                    "OPENAI" => _configuration["RagSettings:OpenAI:ApiKey"],
                    "GEMINI" => _configuration["RagSettings:Gemini:ApiKey"],
                    "CLAUDE" => _configuration["RagSettings:Claude:ApiKey"],
                    _ => null
                };

                // If LLM is configured, use it; otherwise use intent-based fallback
                if (!string.IsNullOrEmpty(apiKey))
                {
                    return await ProcessWithLlmAsync(request, cancellationToken);
                }
                else
                {
                    _logger.LogInformation("LLM not configured, using intent-based orchestration layer");
                    return await ProcessWithIntentAsync(request, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing RAG request");
                return new RagResponseVM
                {
                    Response = "I apologize, but I encountered an error. Please try again or contact support.",
                    RequiresConfirmation = false
                };
            }
        }

        /// <summary>
        /// Processes request with LLM (when configured)
        /// </summary>
        private async Task<RagResponseVM> ProcessWithLlmAsync(RagRequestVM request, CancellationToken cancellationToken)
        {
            // Build system context from existing services
            var context = await BuildContextAsync(cancellationToken);

            // Call LLM with tools
            var llmResponse = await CallLlmWithToolsAsync(request.Message, context, cancellationToken);

            // Execute tool calls if any
            var toolResults = new List<string>();
            if (llmResponse.ToolCalls != null && llmResponse.ToolCalls.Any())
            {
                foreach (var toolCall in llmResponse.ToolCalls)
                {
                    var result = await ExecuteToolAsync(toolCall, cancellationToken);
                    toolResults.Add(toolCall.Name);
                    
                    // If tool execution succeeded, call LLM again with results
                    if (!string.IsNullOrEmpty(result))
                    {
                        llmResponse = await CallLlmWithToolResultsAsync(
                            request.Message,
                            toolCall.Name,
                            result,
                            cancellationToken);
                    }
                }
            }

            return new RagResponseVM
            {
                Response = llmResponse.Content ?? "I apologize, but I couldn't process your request. Please try again.",
                ToolCalls = toolResults.Any() ? toolResults : null,
                RequiresConfirmation = false
            };
        }

        /// <summary>
        /// Intent-based orchestration layer - works without LLM by understanding user intent
        /// and safely executing actions through existing services
        /// </summary>
        private async Task<RagResponseVM> ProcessWithIntentAsync(RagRequestVM request, CancellationToken cancellationToken)
        {
            var message = request.Message.ToLowerInvariant().Trim();
            var toolResults = new List<string>();
            string response = "";

            // Intent: Get Cart / View Cart / What's in cart
            if (ContainsAny(message, new[] { "cart", "basket", "what's in", "what is in", "show me my", "my items" }) &&
                ContainsAny(message, new[] { "what", "show", "view", "see", "list", "display" }))
            {
                var cart = await _cartService.GetCartAsync();
                toolResults.Add("get_cart");
                
                if (cart.Items.Any())
                {
                    var itemsList = string.Join("\n", cart.Items.Select((item, idx) => 
                        $"{idx + 1}. {item.ProductName} - {item.Quantity}x {item.Price:C} each = {(item.Price * item.Quantity):C}"));
                    
                    response = $"Your shopping cart contains {cart.Items.Count} item(s):\n\n{itemsList}\n\n**Total: {cart.Items.Sum(i => i.Price * i.Quantity):C}**";
                }
                else
                {
                    response = "Your shopping cart is currently empty. Would you like to browse our products?";
                }
            }
            // Intent: Search Products
            else if (ContainsAny(message, new[] { "search", "find", "look for", "show me", "browse", "products", "items" }))
            {
                var searchQuery = ExtractSearchQuery(message);
                var products = await _productCacheService.GetFilteredProductsAsync(searchQuery, null, null, null);
                toolResults.Add("search_products");
                
                if (products.Any())
                {
                    var productsList = string.Join("\n", products.Take(10).Select((p, idx) => 
                        $"{idx + 1}. **{p.Name}** - {p.Price:C} (ID: {p.Id})"));
                    
                    response = $"I found {products.Count} product(s) matching '{searchQuery}':\n\n{productsList}";
                    
                    if (products.Count > 10)
                    {
                        response += $"\n\n... and {products.Count - 10} more. Use filters on the home page to see all results.";
                    }
                }
                else
                {
                    response = $"I couldn't find any products matching '{searchQuery}'. Try a different search term or browse all products on the home page.";
                }
            }
            // Intent: Get Product Details
            else if (ContainsAny(message, new[] { "product", "item", "details", "info", "about", "tell me" }) &&
                     ContainsAny(message, new[] { "product", "item" }))
            {
                var productId = ExtractProductId(message);
                if (productId.HasValue)
                {
                    var product = await _productCacheService.GetProductDetailsAsync(productId.Value);
                    toolResults.Add("get_product_details");
                    
                    if (product != null)
                    {
                        response = $"**{product.Name}**\n\n";
                        if (!string.IsNullOrEmpty(product.Description))
                        {
                            response += $"{product.Description}\n\n";
                        }
                        response += $"Price: {product.Price:C}\n";
                        response += $"Stock: {(product.InStock ? $"{product.Stock} available" : "Out of stock")}\n";
                        response += $"Product ID: {product.Id}";
                    }
                    else
                    {
                        response = $"I couldn't find product with ID {productId.Value}. Please check the product ID and try again.";
                    }
                }
                else
                {
                    response = "I need a product ID to show you the details. Please specify which product you'd like to know about (e.g., 'Tell me about product 1').";
                }
            }
            // Intent: Add to Cart
            else if (ContainsAny(message, new[] { "add", "put", "place" }) &&
                     ContainsAny(message, new[] { "cart", "basket" }))
            {
                var productId = ExtractProductId(message);
                if (productId.HasValue)
                {
                    var product = await _productCacheService.GetProductDetailsAsync(productId.Value);
                    if (product != null)
                    {
                        if (product.InStock)
                        {
                            await _cartService.AddToCartAsync(product.Id, product.Name, product.Price, product.ImageUrl);
                            toolResults.Add("add_to_cart");
                            response = $"✅ **{product.Name}** has been added to your cart! Price: {product.Price:C}";
                        }
                        else
                        {
                            response = $"Sorry, **{product.Name}** is currently out of stock.";
                        }
                    }
                    else
                    {
                        response = $"I couldn't find product with ID {productId.Value}. Please check the product ID.";
                    }
                }
                else
                {
                    response = "I need a product ID to add it to your cart. Please specify which product (e.g., 'Add product 1 to cart').";
                }
            }
            // Intent: Increase Quantity
            else if (ContainsAny(message, new[] { "increase", "add more", "more", "increment", "up" }) &&
                     ContainsAny(message, new[] { "quantity", "amount", "number" }))
            {
                var productId = ExtractProductId(message);
                if (productId.HasValue)
                {
                    await _cartService.IncreaseAsync(productId.Value);
                    toolResults.Add("increase_quantity");
                    response = $"✅ Quantity increased for product {productId.Value}.";
                }
                else
                {
                    response = "I need a product ID to increase the quantity. Please specify which product.";
                }
            }
            // Intent: Decrease Quantity / Remove
            else if (ContainsAny(message, new[] { "decrease", "reduce", "remove", "less", "down", "delete" }) &&
                     ContainsAny(message, new[] { "quantity", "amount", "number", "item" }))
            {
                var productId = ExtractProductId(message);
                if (productId.HasValue)
                {
                    await _cartService.DecreaseAsync(productId.Value);
                    toolResults.Add("decrease_quantity");
                    response = $"✅ Quantity decreased for product {productId.Value}.";
                }
                else
                {
                    response = "I need a product ID to decrease the quantity. Please specify which product.";
                }
            }
            // Intent: Clear Cart
            else if (ContainsAny(message, new[] { "clear", "empty", "remove all", "delete all" }) &&
                     ContainsAny(message, new[] { "cart", "basket", "everything" }))
            {
                await _cartService.ClearCartAsync();
                toolResults.Add("clear_cart");
                response = "✅ Your shopping cart has been cleared.";
            }
            // Intent: Greeting / Help
            else if (ContainsAny(message, new[] { "hello", "hi", "hey", "help", "what can you do" }))
            {
                response = "Hello! I'm your VeloStore shopping assistant. I can help you:\n\n" +
                          "🛒 **View your cart** - Ask 'What's in my cart?'\n" +
                          "🔍 **Search products** - Say 'Search for bikes' or 'Find products'\n" +
                          "📦 **Product details** - Ask 'Tell me about product 1'\n" +
                          "➕ **Add to cart** - Say 'Add product 1 to cart'\n" +
                          "📊 **Manage quantities** - 'Increase quantity of product 1'\n" +
                          "🗑️ **Clear cart** - Say 'Clear my cart'\n\n" +
                          "How can I help you today?";
            }
            // Default: Friendly fallback
            else
            {
                response = "I understand you're asking about: \"" + request.Message + "\"\n\n" +
                          "I can help you with:\n" +
                          "• Viewing your shopping cart\n" +
                          "• Searching for products\n" +
                          "• Getting product details\n" +
                          "• Adding items to your cart\n" +
                          "• Managing cart quantities\n\n" +
                          "Try asking: 'What's in my cart?' or 'Search for products'";
            }

            return new RagResponseVM
            {
                Response = response,
                ToolCalls = toolResults.Any() ? toolResults : null,
                RequiresConfirmation = false
            };
        }

        /// <summary>
        /// Helper: Check if message contains any of the keywords
        /// </summary>
        private static bool ContainsAny(string message, string[] keywords)
        {
            return keywords.Any(keyword => message.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Helper: Extract search query from message
        /// </summary>
        private static string ExtractSearchQuery(string message)
        {
            var patterns = new[]
            {
                new { Pattern = "search for (.+)", Group = 1 },
                new { Pattern = "find (.+)", Group = 1 },
                new { Pattern = "look for (.+)", Group = 1 },
                new { Pattern = "show me (.+)", Group = 1 },
                new { Pattern = "browse (.+)", Group = 1 },
                new { Pattern = "products with (.+)", Group = 1 },
                new { Pattern = "items with (.+)", Group = 1 }
            };

            foreach (var pattern in patterns)
            {
                var regex = new Regex(pattern.Pattern, RegexOptions.IgnoreCase);
                var match = regex.Match(message);
                if (match.Success && match.Groups.Count > pattern.Group)
                {
                    return match.Groups[pattern.Group].Value.Trim();
                }
            }

            // Fallback: extract words after common verbs
            var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var skipWords = new[] { "search", "find", "look", "show", "browse", "for", "me", "products", "items", "with" };
            var queryWords = words.Where(w => !skipWords.Contains(w.ToLowerInvariant())).Take(5);
            return string.Join(" ", queryWords);
        }

        /// <summary>
        /// Helper: Extract product ID from message
        /// </summary>
        private static int? ExtractProductId(string message)
        {
            var patterns = new[]
            {
                new Regex(@"product\s+(\d+)", RegexOptions.IgnoreCase),
                new Regex(@"item\s+(\d+)", RegexOptions.IgnoreCase),
                new Regex(@"#(\d+)", RegexOptions.IgnoreCase),
                new Regex(@"id\s+(\d+)", RegexOptions.IgnoreCase)
            };

            foreach (var pattern in patterns)
            {
                var match = pattern.Match(message);
                if (match.Success && match.Groups.Count > 1)
                {
                    if (int.TryParse(match.Groups[1].Value, out var productId))
                    {
                        return productId;
                    }
                }
            }

            // Fallback: look for standalone numbers
            var numberMatch = Regex.Match(message, @"\b(\d+)\b");
            if (numberMatch.Success && int.TryParse(numberMatch.Groups[1].Value, out var id))
            {
                return id;
            }

            return null;
        }

        public Task<List<RagMessageVM>> GetConversationHistoryAsync(string conversationId, CancellationToken cancellationToken = default)
        {
            // For now, return empty list - can be extended with conversation storage if needed
            // This respects the architecture by not introducing new persistence
            return Task.FromResult(new List<RagMessageVM>());
        }

        /// <summary>
        /// Builds context from existing services (respects cache boundaries)
        /// </summary>
        private async Task<string> BuildContextAsync(CancellationToken cancellationToken)
        {
            var contextParts = new List<string>
            {
                "You are a helpful shopping assistant for VeloStore, an e-commerce platform.",
                "You can help users find products, view details, and manage their shopping cart.",
                "Always be concise, friendly, and accurate in your responses."
            };

            try
            {
                // Get cart state (uses existing service - respects Redis cache)
                var cart = await _cartService.GetCartAsync();
                if (cart.Items.Any())
                {
                    var cartSummary = $"Current cart has {cart.Items.Count} item(s) with total of {cart.Items.Sum(i => i.Price * i.Quantity):C}.";
                    contextParts.Add(cartSummary);
                }
                else
                {
                    contextParts.Add("The shopping cart is currently empty.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build cart context");
            }

            return string.Join(" ", contextParts);
        }

        /// <summary>
        /// Calls LLM with tool definitions - supports OpenAI, Gemini, and Claude
        /// </summary>
        private async Task<LlmResponse> CallLlmWithToolsAsync(string userMessage, string context, CancellationToken cancellationToken)
        {
            var provider = _configuration["RagSettings:Provider"] ?? "OpenAI";
            var maxTokens = _configuration.GetValue<int>("RagSettings:MaxTokens", 500);
            var temperature = _configuration.GetValue<double>("RagSettings:Temperature", 0.7);

            return provider.ToUpperInvariant() switch
            {
                "OPENAI" => await CallOpenAIAsync(userMessage, context, maxTokens, temperature, cancellationToken),
                "GEMINI" => await CallGeminiAsync(userMessage, context, maxTokens, temperature, cancellationToken),
                "CLAUDE" => await CallClaudeAsync(userMessage, context, maxTokens, temperature, cancellationToken),
                _ => new LlmResponse { Content = $"Unsupported provider: {provider}. Supported providers: OpenAI, Gemini, Claude" }
            };
        }

        /// <summary>
        /// Calls OpenAI/ChatGPT API
        /// </summary>
        private async Task<LlmResponse> CallOpenAIAsync(string userMessage, string context, int maxTokens, double temperature, CancellationToken cancellationToken)
        {
            var apiKey = _configuration["RagSettings:OpenAI:ApiKey"];
            var endpoint = _configuration["RagSettings:OpenAI:Endpoint"] ?? "https://api.openai.com/v1";
            var model = _configuration["RagSettings:OpenAI:Model"] ?? "gpt-4o-mini";

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("OpenAI API key not configured");
                return new LlmResponse { Content = "OpenAI API key is not configured. Please add your API key to RagSettings:OpenAI:ApiKey in appsettings.json." };
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var requestBody = new
            {
                model = model,
                messages = new[]
                {
                    new { role = "system", content = context },
                    new { role = "user", content = userMessage }
                },
                tools = _availableTools,
                tool_choice = "auto",
                temperature = temperature,
                max_tokens = maxTokens
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{endpoint}/chat/completions", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var llmResult = JsonSerializer.Deserialize<LlmApiResponse>(responseJson, _jsonOptions);

                if (llmResult?.Choices == null || !llmResult.Choices.Any())
                {
                    return new LlmResponse { Content = "I couldn't generate a response. Please try again." };
                }

                var choice = llmResult.Choices[0];
                var toolCalls = choice.Message?.ToolCalls?.Select(tc => new ToolCall
                {
                    Name = tc.Function?.Name ?? "",
                    Arguments = tc.Function?.Arguments ?? "{}"
                }).ToList();

                return new LlmResponse
                {
                    Content = choice.Message?.Content,
                    ToolCalls = toolCalls
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API");
                return new LlmResponse { Content = "I'm having trouble connecting to OpenAI. Please check your API key and try again." };
            }
        }

        /// <summary>
        /// Calls Google Gemini API
        /// </summary>
        private async Task<LlmResponse> CallGeminiAsync(string userMessage, string context, int maxTokens, double temperature, CancellationToken cancellationToken)
        {
            var apiKey = _configuration["RagSettings:Gemini:ApiKey"];
            var endpoint = _configuration["RagSettings:Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta";
            var model = _configuration["RagSettings:Gemini:Model"] ?? "gemini-pro";

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Gemini API key not configured");
                return new LlmResponse { Content = "Gemini API key is not configured. Please add your API key to RagSettings:Gemini:ApiKey in appsettings.json." };
            }

            var httpClient = _httpClientFactory.CreateClient();

            // Gemini uses function calling format similar to OpenAI
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"{context}\n\nUser: {userMessage}" }
                        }
                    }
                },
                tools = new[]
                {
                    new
                    {
                        functionDeclarations = _availableTools.Select(t => 
                        {
                            var func = ((dynamic)t).function;
                            return new
                            {
                                name = func.name,
                                description = func.description,
                                parameters = func.parameters
                            };
                        }).ToArray()
                    }
                },
                generationConfig = new
                {
                    temperature = temperature,
                    maxOutputTokens = maxTokens
                }
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{endpoint}/models/{model}:generateContent?key={apiKey}", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var geminiResult = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (!geminiResult.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                {
                    return new LlmResponse { Content = "I couldn't generate a response. Please try again." };
                }

                var candidate = candidates[0];
                var contentText = candidate.GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";

                // Check for function calls
                var toolCalls = new List<ToolCall>();
                if (candidate.TryGetProperty("content", out var contentObj) && 
                    contentObj.TryGetProperty("parts", out var parts))
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("functionCall", out var functionCall))
                        {
                            toolCalls.Add(new ToolCall
                            {
                                Name = functionCall.GetProperty("name").GetString() ?? "",
                                Arguments = functionCall.TryGetProperty("args", out var args) ? args.GetRawText() : "{}"
                            });
                        }
                    }
                }

                return new LlmResponse
                {
                    Content = contentText,
                    ToolCalls = toolCalls.Any() ? toolCalls : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Gemini API");
                return new LlmResponse { Content = "I'm having trouble connecting to Gemini. Please check your API key and try again." };
            }
        }

        /// <summary>
        /// Calls Anthropic Claude API
        /// </summary>
        private async Task<LlmResponse> CallClaudeAsync(string userMessage, string context, int maxTokens, double temperature, CancellationToken cancellationToken)
        {
            var apiKey = _configuration["RagSettings:Claude:ApiKey"];
            var endpoint = _configuration["RagSettings:Claude:Endpoint"] ?? "https://api.anthropic.com/v1";
            var model = _configuration["RagSettings:Claude:Model"] ?? "claude-3-haiku-20240307";
            var version = _configuration["RagSettings:Claude:Version"] ?? "2023-06-01";

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Claude API key not configured");
                return new LlmResponse { Content = "Claude API key is not configured. Please add your API key to RagSettings:Claude:ApiKey in appsettings.json." };
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            httpClient.DefaultRequestHeaders.Add("anthropic-version", version);

            // Convert tools to Claude format
            var claudeTools = _availableTools.Select(t =>
            {
                var func = ((dynamic)t).function;
                return new
                {
                    name = func.name,
                    description = func.description,
                    input_schema = func.parameters
                };
            }).ToArray();

            var requestBody = new
            {
                model = model,
                max_tokens = maxTokens,
                temperature = temperature,
                system = context,
                messages = new[]
                {
                    new { role = "user", content = userMessage }
                },
                tools = claudeTools
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{endpoint}/messages", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var claudeResult = JsonSerializer.Deserialize<JsonElement>(responseJson);

                var contentText = "";
                var toolCalls = new List<ToolCall>();

                if (claudeResult.TryGetProperty("content", out var contentArray))
                {
                    foreach (var contentItem in contentArray.EnumerateArray())
                    {
                        if (contentItem.TryGetProperty("type", out var type))
                        {
                            var contentType = type.GetString();
                            if (contentType == "text")
                            {
                                contentText = contentItem.GetProperty("text").GetString() ?? "";
                            }
                            else if (contentType == "tool_use")
                            {
                                toolCalls.Add(new ToolCall
                                {
                                    Name = contentItem.GetProperty("name").GetString() ?? "",
                                    Arguments = contentItem.TryGetProperty("input", out var input) ? input.GetRawText() : "{}"
                                });
                            }
                        }
                    }
                }

                return new LlmResponse
                {
                    Content = contentText,
                    ToolCalls = toolCalls.Any() ? toolCalls : null
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Claude API");
                return new LlmResponse { Content = "I'm having trouble connecting to Claude. Please check your API key and try again." };
            }
        }

        /// <summary>
        /// Calls LLM again with tool execution results - supports OpenAI, Gemini, and Claude
        /// </summary>
        private async Task<LlmResponse> CallLlmWithToolResultsAsync(
            string userMessage,
            string toolName,
            string toolResult,
            CancellationToken cancellationToken)
        {
            var provider = _configuration["RagSettings:Provider"] ?? "OpenAI";
            var maxTokens = _configuration.GetValue<int>("RagSettings:MaxTokens", 500);
            var temperature = _configuration.GetValue<double>("RagSettings:Temperature", 0.7);

            return provider.ToUpperInvariant() switch
            {
                "OPENAI" => await CallOpenAIWithToolResultsAsync(userMessage, toolName, toolResult, maxTokens, temperature, cancellationToken),
                "GEMINI" => await CallGeminiWithToolResultsAsync(userMessage, toolName, toolResult, maxTokens, temperature, cancellationToken),
                "CLAUDE" => await CallClaudeWithToolResultsAsync(userMessage, toolName, toolResult, maxTokens, temperature, cancellationToken),
                _ => new LlmResponse { Content = $"Unsupported provider: {provider}" }
            };
        }

        /// <summary>
        /// Calls OpenAI with tool results
        /// </summary>
        private async Task<LlmResponse> CallOpenAIWithToolResultsAsync(string userMessage, string toolName, string toolResult, int maxTokens, double temperature, CancellationToken cancellationToken)
        {
            var apiKey = _configuration["RagSettings:OpenAI:ApiKey"];
            var endpoint = _configuration["RagSettings:OpenAI:Endpoint"] ?? "https://api.openai.com/v1";
            var model = _configuration["RagSettings:OpenAI:Model"] ?? "gpt-4o-mini";

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

            var requestBody = new
            {
                model = model,
                messages = new object[]
                {
                    new { role = "user", content = userMessage },
                    new { role = "assistant", content = (string?)null, tool_calls = new[] { new { id = "call_1", type = "function", function = new { name = toolName, arguments = "{}" } } } },
                    new { role = "tool", content = toolResult, tool_call_id = "call_1" }
                },
                temperature = temperature,
                max_tokens = maxTokens
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{endpoint}/chat/completions", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var llmResult = JsonSerializer.Deserialize<LlmApiResponse>(responseJson, _jsonOptions);

                if (llmResult?.Choices == null || !llmResult.Choices.Any())
                {
                    return new LlmResponse { Content = "I couldn't generate a response. Please try again." };
                }

                return new LlmResponse
                {
                    Content = llmResult.Choices[0].Message?.Content
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API with tool results");
                return new LlmResponse { Content = "I processed your request but had trouble generating a response. Please check your cart or try again." };
            }
        }

        /// <summary>
        /// Calls Gemini with tool results
        /// </summary>
        private async Task<LlmResponse> CallGeminiWithToolResultsAsync(string userMessage, string toolName, string toolResult, int maxTokens, double temperature, CancellationToken cancellationToken)
        {
            var apiKey = _configuration["RagSettings:Gemini:ApiKey"];
            var endpoint = _configuration["RagSettings:Gemini:Endpoint"] ?? "https://generativelanguage.googleapis.com/v1beta";
            var model = _configuration["RagSettings:Gemini:Model"] ?? "gemini-pro";

            var httpClient = _httpClientFactory.CreateClient();

            var requestBody = new
            {
                contents = new object[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = userMessage }
                        }
                    },
                    new
                    {
                        parts = new[]
                        {
                            new
                            {
                                functionResponse = new
                                {
                                    name = toolName,
                                    response = JsonSerializer.Deserialize<JsonElement>(toolResult)
                                }
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = temperature,
                    maxOutputTokens = maxTokens
                }
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{endpoint}/models/{model}:generateContent?key={apiKey}", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var geminiResult = JsonSerializer.Deserialize<JsonElement>(responseJson);

                if (!geminiResult.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                {
                    return new LlmResponse { Content = "I couldn't generate a response. Please try again." };
                }

                var contentText = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString() ?? "";

                return new LlmResponse { Content = contentText };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Gemini API with tool results");
                return new LlmResponse { Content = "I processed your request but had trouble generating a response." };
            }
        }

        /// <summary>
        /// Calls Claude with tool results
        /// </summary>
        private async Task<LlmResponse> CallClaudeWithToolResultsAsync(string userMessage, string toolName, string toolResult, int maxTokens, double temperature, CancellationToken cancellationToken)
        {
            var apiKey = _configuration["RagSettings:Claude:ApiKey"];
            var endpoint = _configuration["RagSettings:Claude:Endpoint"] ?? "https://api.anthropic.com/v1";
            var model = _configuration["RagSettings:Claude:Model"] ?? "claude-3-haiku-20240307";
            var version = _configuration["RagSettings:Claude:Version"] ?? "2023-06-01";

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            httpClient.DefaultRequestHeaders.Add("anthropic-version", version);

            var requestBody = new
            {
                model = model,
                max_tokens = maxTokens,
                temperature = temperature,
                messages = new object[]
                {
                    new { role = "user", content = userMessage },
                    new
                    {
                        role = "assistant",
                        content = new[]
                        {
                            new
                            {
                                type = "tool_use",
                                id = "tool_1",
                                name = toolName,
                                input = JsonSerializer.Deserialize<JsonElement>(toolResult)
                            }
                        }
                    },
                    new
                    {
                        role = "user",
                        content = new[]
                        {
                            new
                            {
                                type = "tool_result",
                                tool_use_id = "tool_1",
                                content = toolResult
                            }
                        }
                    }
                }
            };

            try
            {
                var json = JsonSerializer.Serialize(requestBody, _jsonOptions);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await httpClient.PostAsync($"{endpoint}/messages", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var claudeResult = JsonSerializer.Deserialize<JsonElement>(responseJson);

                var contentText = "";
                if (claudeResult.TryGetProperty("content", out var contentArray))
                {
                    foreach (var contentItem in contentArray.EnumerateArray())
                    {
                        if (contentItem.TryGetProperty("type", out var type) && type.GetString() == "text")
                        {
                            contentText = contentItem.GetProperty("text").GetString() ?? "";
                            break;
                        }
                    }
                }

                return new LlmResponse { Content = contentText };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Claude API with tool results");
                return new LlmResponse { Content = "I processed your request but had trouble generating a response." };
            }
        }

        /// <summary>
        /// Executes tool calls by delegating to existing services
        /// </summary>
        private async Task<string> ExecuteToolAsync(ToolCall toolCall, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Executing tool: {ToolName}", toolCall.Name);

                return toolCall.Name switch
                {
                    "search_products" => await ExecuteSearchProductsAsync(toolCall.Arguments, cancellationToken),
                    "get_product_details" => await ExecuteGetProductDetailsAsync(toolCall.Arguments, cancellationToken),
                    "get_cart" => await ExecuteGetCartAsync(cancellationToken),
                    "add_to_cart" => await ExecuteAddToCartAsync(toolCall.Arguments, cancellationToken),
                    "increase_quantity" => await ExecuteIncreaseQuantityAsync(toolCall.Arguments, cancellationToken),
                    "decrease_quantity" => await ExecuteDecreaseQuantityAsync(toolCall.Arguments, cancellationToken),
                    "clear_cart" => await ExecuteClearCartAsync(cancellationToken),
                    _ => JsonSerializer.Serialize(new { error = $"Unknown tool: {toolCall.Name}" }, _jsonOptions)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.Name);
                return JsonSerializer.Serialize(new { error = $"Error executing {toolCall.Name}: {ex.Message}" }, _jsonOptions);
            }
        }

        // Tool execution methods - ALL delegate to existing services

        private async Task<string> ExecuteSearchProductsAsync(string arguments, CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<JsonElement>(arguments);
            var query = args.GetProperty("query").GetString() ?? "";

            // Use existing service - respects cache boundaries
            var products = await _productCacheService.GetFilteredProductsAsync(query, null, null, null);

            return JsonSerializer.Serialize(new
            {
                success = true,
                count = products.Count,
                products = products.Select(p => new { id = p.Id, name = p.Name, price = p.Price })
            }, _jsonOptions);
        }

        private async Task<string> ExecuteGetProductDetailsAsync(string arguments, CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<JsonElement>(arguments);
            var productId = args.GetProperty("productId").GetInt32();

            // Use existing service - respects cache boundaries
            var product = await _productCacheService.GetProductDetailsAsync(productId);

            if (product == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Product not found" }, _jsonOptions);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                product = new
                {
                    id = product.Id,
                    name = product.Name,
                    description = product.Description,
                    price = product.Price,
                    stock = product.Stock,
                    inStock = product.InStock
                }
            }, _jsonOptions);
        }

        private async Task<string> ExecuteGetCartAsync(CancellationToken cancellationToken)
        {
            // Use existing service - respects Redis cache
            var cart = await _cartService.GetCartAsync();

            return JsonSerializer.Serialize(new
            {
                success = true,
                itemCount = cart.Items.Count,
                total = cart.Items.Sum(i => i.Price * i.Quantity),
                items = cart.Items.Select(i => new
                {
                    productId = i.ProductId,
                    productName = i.ProductName,
                    price = i.Price,
                    quantity = i.Quantity,
                    subtotal = i.Price * i.Quantity
                })
            }, _jsonOptions);
        }

        private async Task<string> ExecuteAddToCartAsync(string arguments, CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<JsonElement>(arguments);
            var productId = args.GetProperty("productId").GetInt32();

            // Get product details first (uses cache)
            var product = await _productCacheService.GetProductDetailsAsync(productId);
            if (product == null)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Product not found" }, _jsonOptions);
            }

            if (!product.InStock)
            {
                return JsonSerializer.Serialize(new { success = false, error = "Product is out of stock" }, _jsonOptions);
            }

            // Use existing service - respects Redis cache and cart logic
            await _cartService.AddToCartAsync(product.Id, product.Name, product.Price, product.ImageUrl);

            return JsonSerializer.Serialize(new
            {
                success = true,
                message = $"{product.Name} added to cart",
                productId = product.Id,
                productName = product.Name
            }, _jsonOptions);
        }

        private async Task<string> ExecuteIncreaseQuantityAsync(string arguments, CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<JsonElement>(arguments);
            var productId = args.GetProperty("productId").GetInt32();

            // Use existing service - respects Redis cache
            await _cartService.IncreaseAsync(productId);

            return JsonSerializer.Serialize(new { success = true, message = "Quantity increased" }, _jsonOptions);
        }

        private async Task<string> ExecuteDecreaseQuantityAsync(string arguments, CancellationToken cancellationToken)
        {
            var args = JsonSerializer.Deserialize<JsonElement>(arguments);
            var productId = args.GetProperty("productId").GetInt32();

            // Use existing service - respects Redis cache
            await _cartService.DecreaseAsync(productId);

            return JsonSerializer.Serialize(new { success = true, message = "Quantity decreased" }, _jsonOptions);
        }

        private async Task<string> ExecuteClearCartAsync(CancellationToken cancellationToken)
        {
            // Use existing service - respects Redis cache
            await _cartService.ClearCartAsync();

            return JsonSerializer.Serialize(new { success = true, message = "Cart cleared" }, _jsonOptions);
        }

        // Helper classes for LLM API responses
        private class LlmResponse
        {
            public string? Content { get; set; }
            public List<ToolCall>? ToolCalls { get; set; }
        }

        private class ToolCall
        {
            public string Name { get; set; } = default!;
            public string Arguments { get; set; } = default!;
        }

        private class LlmApiResponse
        {
            [JsonPropertyName("choices")]
            public List<LlmChoice>? Choices { get; set; }
        }

        private class LlmChoice
        {
            [JsonPropertyName("message")]
            public LlmMessage? Message { get; set; }
        }

        private class LlmMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }

            [JsonPropertyName("tool_calls")]
            public List<LlmToolCall>? ToolCalls { get; set; }
        }

        private class LlmToolCall
        {
            [JsonPropertyName("function")]
            public LlmFunction? Function { get; set; }
        }

        private class LlmFunction
        {
            [JsonPropertyName("name")]
            public string? Name { get; set; }

            [JsonPropertyName("arguments")]
            public string? Arguments { get; set; }
        }
    }
}

