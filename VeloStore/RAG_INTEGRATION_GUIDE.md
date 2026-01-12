# 🤖 RAG Integration Guide - VeloStore AI Shopping Assistant

## 📋 Overview

The VeloStore platform now includes a **Retrieval-Augmented Generation (RAG)** service that provides an AI-powered shopping assistant. This integration strictly adheres to the existing enterprise architecture, acting as an **orchestration layer** that delegates all operations to existing services.

---

## 🏗️ Architecture Compliance

### **Core Principles**

✅ **NO Direct Database Access**: RAG service never touches `DbContext` or SQL  
✅ **NO Direct Redis Access**: RAG service never touches `IDistributedCache` directly  
✅ **Service-Only Access**: Uses `IProductCacheService` and `ICartService` exclusively  
✅ **Cache Respect**: All operations respect existing multi-layer cache strategy  
✅ **SOLID Compliance**: Interface-based design with dependency injection  

### **Architectural Layer**

```
┌─────────────────────────────────────────────────────┐
│         PRESENTATION LAYER                          │
│    (Razor Pages: Assistant.cshtml)                  │
└────────────────────┬────────────────────────────────┘
                     │
┌────────────────────┴────────────────────────────────┐
│         RAG ORCHESTRATION LAYER                      │
│    (RagService - AI + Tool Execution)              │
└────────────────────┬────────────────────────────────┘
                     │ Delegates to
┌────────────────────┴────────────────────────────────┐
│         SERVICE LAYER                               │
│    (IProductCacheService, ICartService)            │
└────────────────────┬────────────────────────────────┘
                     │
┌────────────────────┴────────────────────────────────┐
│         CACHING LAYER                               │
│    (Memory → Redis → Database)                     │
└─────────────────────────────────────────────────────┘
```

---

## 🔧 Implementation Details

### **Service Location**

- **Interface**: `VeloStore/Services/IRagService.cs`
- **Implementation**: `VeloStore/Services/RagService.cs`
- **ViewModels**: `VeloStore/ViewModels/Rag*.cs`
- **UI**: `VeloStore/Pages/Assistant.cshtml` and `Assistant.cshtml.cs`

### **Service Registration**

Registered in `Program.cs`:

```csharp
// HttpClient Factory (for RAG service LLM calls)
builder.Services.AddHttpClient();

// RAG Service (AI Shopping Assistant)
builder.Services.AddScoped<IRagService, RagService>();
```

---

## 🛠️ Available Tools (Function Calling)

The RAG service supports the following tools that delegate to existing services:

### **1. search_products**
- **Service**: `IProductCacheService.GetFilteredProductsAsync()`
- **Purpose**: Search products by name or description
- **Respects**: Cache bypass for filtered queries (ensures accuracy)
- **Parameters**: `{ "query": "string" }`

### **2. get_product_details**
- **Service**: `IProductCacheService.GetProductDetailsAsync(productId)`
- **Purpose**: Get detailed product information
- **Respects**: Multi-layer cache (Memory → Redis → Database)
- **Parameters**: `{ "productId": int }`

### **3. get_cart**
- **Service**: `ICartService.GetCartAsync()`
- **Purpose**: Get current shopping cart contents
- **Respects**: Redis cache with sliding expiration
- **Parameters**: `{}`

### **4. add_to_cart**
- **Service**: `ICartService.AddToCartAsync(...)`
- **Purpose**: Add product to cart
- **Respects**: Redis cache, guest/user cart logic, stock validation
- **Parameters**: `{ "productId": int }`

### **5. increase_quantity**
- **Service**: `ICartService.IncreaseAsync(productId)`
- **Purpose**: Increase product quantity in cart
- **Respects**: Redis cache, sliding expiration reset
- **Parameters**: `{ "productId": int }`

### **6. decrease_quantity**
- **Service**: `ICartService.DecreaseAsync(productId)`
- **Purpose**: Decrease product quantity in cart
- **Respects**: Redis cache, automatic removal if quantity reaches 0
- **Parameters**: `{ "productId": int }`

### **7. clear_cart**
- **Service**: `ICartService.ClearCartAsync()`
- **Purpose**: Clear all items from cart
- **Respects**: Redis cache deletion
- **Parameters**: `{}`

---

## ⚙️ Configuration

### **appsettings.json**

```json
{
  "RagSettings": {
    "ApiKey": "your-openai-api-key",
    "Endpoint": "https://api.openai.com/v1",
    "Model": "gpt-4o-mini",
    "MaxTokens": 500,
    "Temperature": 0.7
  }
}
```

### **Configuration Options**

- **ApiKey**: Your OpenAI API key (or Azure OpenAI key)
- **Endpoint**: API endpoint URL
  - OpenAI: `https://api.openai.com/v1`
  - Azure OpenAI: `https://your-resource.openai.azure.com`
- **Model**: LLM model to use (default: `gpt-4o-mini`)
- **MaxTokens**: Maximum response length (default: 500)
- **Temperature**: Response creativity (0.0-1.0, default: 0.7)

### **Azure OpenAI Configuration**

For Azure OpenAI, update the endpoint:

```json
{
  "RagSettings": {
    "ApiKey": "your-azure-openai-key",
    "Endpoint": "https://your-resource.openai.azure.com",
    "Model": "gpt-4o-mini"
  }
}
```

---

## 🚀 Usage

### **Accessing the Assistant**

Navigate to: `/Assistant` or `/Assistant.cshtml`

### **Example Interactions**

1. **Product Search**:
   - User: "Show me all products"
   - Assistant: Calls `search_products` → Returns product list

2. **Product Details**:
   - User: "Tell me about product 1"
   - Assistant: Calls `get_product_details` → Returns product info

3. **Cart Management**:
   - User: "What's in my cart?"
   - Assistant: Calls `get_cart` → Returns cart contents

4. **Add to Cart**:
   - User: "Add product 1 to my cart"
   - Assistant: Calls `add_to_cart` → Confirms addition

5. **Quantity Management**:
   - User: "Increase quantity of product 1"
   - Assistant: Calls `increase_quantity` → Updates cart

---

## 🔒 Security & Best Practices

### **Security**

- ✅ API keys stored in `appsettings.json` (use User Secrets in development)
- ✅ No sensitive data exposed in responses
- ✅ All operations go through existing service layer
- ✅ Session-based conversation storage (no persistence)

### **Error Handling**

- ✅ Graceful fallback if LLM service unavailable
- ✅ Tool execution errors logged but don't crash the service
- ✅ User-friendly error messages
- ✅ Comprehensive logging for debugging

### **Performance**

- ✅ All tool calls respect existing cache layers
- ✅ No additional database queries beyond existing services
- ✅ Conversation history stored in session (lightweight)
- ✅ Async/await throughout for non-blocking operations

---

## 📊 How It Works

### **Request Flow**

```
1. User sends message → Assistant.cshtml.cs
2. Assistant.cshtml.cs → IRagService.ProcessMessageAsync()
3. RagService builds context from existing services:
   - Gets cart state (via ICartService)
   - Prepares system prompt
4. RagService calls LLM with tool definitions
5. LLM returns tool calls (if needed)
6. RagService executes tools by calling existing services:
   - IProductCacheService methods
   - ICartService methods
7. Tool results sent back to LLM
8. LLM generates final response
9. Response returned to user
```

### **Context Building**

The RAG service builds context from existing services:

```csharp
// Gets cart state (respects Redis cache)
var cart = await _cartService.GetCartAsync();

// Builds context string for LLM
var context = $"Current cart has {cart.Items.Count} item(s)...";
```

This ensures the AI has current information without bypassing cache layers.

---

## 🧪 Testing

### **Test Scenarios**

1. **Without LLM Configuration**:
   - Service should return fallback message
   - No exceptions thrown

2. **With Invalid API Key**:
   - Error logged
   - User-friendly message displayed

3. **Tool Execution**:
   - All tools delegate to existing services
   - Cache layers respected
   - Errors handled gracefully

### **Manual Testing**

1. Configure API key in `appsettings.json`
2. Navigate to `/Assistant`
3. Try various queries:
   - "Show me products"
   - "What's in my cart?"
   - "Add product 1 to cart"
   - "Increase quantity of product 1"

---

## 🔍 Monitoring & Debugging

### **Logging**

The RAG service logs:
- Request processing
- Tool execution
- LLM API calls
- Errors and warnings

Check application logs for:
```
[Information] Processing RAG request: {Message}
[Information] Executing tool: {ToolName}
[Error] Error processing RAG request
```

### **Session Storage**

Conversation history stored in session:
- Key: `AssistantMessages_{conversationId}`
- Format: JSON array of `RagMessageVM`
- Expires: With session (6 hours default)

---

## 📝 Extension Points

### **Adding New Tools**

To add a new tool:

1. Add tool definition to `_availableTools` in `RagService.cs`
2. Add execution method: `Execute{ToolName}Async()`
3. Add case to `ExecuteToolAsync()` switch statement
4. Ensure tool delegates to existing services only

### **Conversation Persistence**

Currently, conversations are session-based. To persist:

1. Create `IConversationService` interface
2. Implement with database storage
3. Inject into `RagService`
4. Update `GetConversationHistoryAsync()` implementation

**Important**: Still use existing services for all business operations.

---

## ✅ Architecture Compliance Checklist

- ✅ No direct database access
- ✅ No direct Redis access
- ✅ Uses existing service interfaces only
- ✅ Respects cache boundaries
- ✅ Interface-based design
- ✅ Dependency injection
- ✅ Error handling
- ✅ Logging
- ✅ Async/await
- ✅ SOLID principles

---

## 🎯 Summary

The RAG integration provides:

- 🤖 **AI-powered shopping assistant**
- 🔒 **Architecture-compliant** (no violations)
- ⚡ **Performance-preserving** (respects caches)
- 🛡️ **Production-safe** (error handling, logging)
- 🔧 **Maintainable** (clean separation of concerns)
- 📈 **Scalable** (delegates to existing services)

**Key Takeaway**: The RAG layer is a **non-intrusive orchestration layer** that enhances user experience while preserving all existing architectural guarantees.

---

**Last Updated**: 2024  
**Maintained By**: VeloStore Development Team

