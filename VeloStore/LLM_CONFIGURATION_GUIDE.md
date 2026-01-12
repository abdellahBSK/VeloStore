# 🤖 LLM Configuration Guide - VeloStore AI Assistant

## 📋 Overview

The VeloStore AI Shopping Assistant supports **three major LLM providers**:
- **OpenAI/ChatGPT** (gpt-4o-mini, gpt-4, gpt-3.5-turbo)
- **Google Gemini** (gemini-pro, gemini-ultra)
- **Anthropic Claude** (claude-3-haiku, claude-3-sonnet, claude-3-opus)

---

## ⚙️ Configuration

### **Step 1: Choose Your Provider**

Edit `appsettings.json` and set the `Provider` field:

```json
{
  "RagSettings": {
    "Provider": "OpenAI"  // Options: "OpenAI", "Gemini", "Claude"
  }
}
```

### **Step 2: Configure Your Chosen Provider**

Add your API key and settings for the provider you selected.

---

## 🔵 OpenAI/ChatGPT Configuration

### **Get Your API Key**

1. Go to [https://platform.openai.com/api-keys](https://platform.openai.com/api-keys)
2. Sign up or log in
3. Create a new API key
4. Copy the key (starts with `sk-...`)

### **Configuration**

```json
{
  "RagSettings": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "sk-your-api-key-here",
      "Endpoint": "https://api.openai.com/v1",
      "Model": "gpt-4o-mini"
    }
  }
}
```

### **Available Models**

- `gpt-4o-mini` (Recommended - Fast & Cost-effective)
- `gpt-4o`
- `gpt-4-turbo`
- `gpt-4`
- `gpt-3.5-turbo`

### **Azure OpenAI**

If using Azure OpenAI, update the endpoint:

```json
{
  "RagSettings": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "your-azure-key",
      "Endpoint": "https://your-resource.openai.azure.com",
      "Model": "gpt-4o-mini"
    }
  }
}
```

---

## 🟢 Google Gemini Configuration

### **Get Your API Key**

1. Go to [https://makersuite.google.com/app/apikey](https://makersuite.google.com/app/apikey)
2. Sign in with your Google account
3. Click "Create API Key"
4. Copy the key

### **Configuration**

```json
{
  "RagSettings": {
    "Provider": "Gemini",
    "Gemini": {
      "ApiKey": "your-gemini-api-key-here",
      "Endpoint": "https://generativelanguage.googleapis.com/v1beta",
      "Model": "gemini-pro"
    }
  }
}
```

### **Available Models**

- `gemini-pro` (Recommended)
- `gemini-ultra`
- `gemini-pro-vision`

---

## 🟣 Anthropic Claude Configuration

### **Get Your API Key**

1. Go to [https://console.anthropic.com/](https://console.anthropic.com/)
2. Sign up or log in
3. Navigate to API Keys
4. Create a new API key
5. Copy the key (starts with `sk-ant-...`)

### **Configuration**

```json
{
  "RagSettings": {
    "Provider": "Claude",
    "Claude": {
      "ApiKey": "sk-ant-your-api-key-here",
      "Endpoint": "https://api.anthropic.com/v1",
      "Model": "claude-3-haiku-20240307",
      "Version": "2023-06-01"
    }
  }
}
```

### **Available Models**

- `claude-3-haiku-20240307` (Recommended - Fast & Cost-effective)
- `claude-3-sonnet-20240229` (Balanced)
- `claude-3-opus-20240229` (Most capable)

---

## 🔧 Complete Configuration Example

### **Example 1: OpenAI (Recommended for Start)**

```json
{
  "RagSettings": {
    "Provider": "OpenAI",
    "OpenAI": {
      "ApiKey": "sk-your-openai-key",
      "Endpoint": "https://api.openai.com/v1",
      "Model": "gpt-4o-mini"
    },
    "MaxTokens": 500,
    "Temperature": 0.7
  }
}
```

### **Example 2: Gemini**

```json
{
  "RagSettings": {
    "Provider": "Gemini",
    "Gemini": {
      "ApiKey": "your-gemini-key",
      "Endpoint": "https://generativelanguage.googleapis.com/v1beta",
      "Model": "gemini-pro"
    },
    "MaxTokens": 500,
    "Temperature": 0.7
  }
}
```

### **Example 3: Claude**

```json
{
  "RagSettings": {
    "Provider": "Claude",
    "Claude": {
      "ApiKey": "sk-ant-your-claude-key",
      "Endpoint": "https://api.anthropic.com/v1",
      "Model": "claude-3-haiku-20240307",
      "Version": "2023-06-01"
    },
    "MaxTokens": 500,
    "Temperature": 0.7
  }
}
```

---

## 🔒 Security Best Practices

### **Development (User Secrets)**

Never commit API keys to source control! Use User Secrets:

```bash
# For OpenAI
dotnet user-secrets set "RagSettings:OpenAI:ApiKey" "sk-your-key"

# For Gemini
dotnet user-secrets set "RagSettings:Gemini:ApiKey" "your-key"

# For Claude
dotnet user-secrets set "RagSettings:Claude:ApiKey" "sk-ant-your-key"

# Set provider
dotnet user-secrets set "RagSettings:Provider" "OpenAI"
```

### **Production**

Use environment variables or Azure Key Vault:

```bash
# Environment Variables
export RagSettings__OpenAI__ApiKey="sk-your-key"
export RagSettings__Provider="OpenAI"
```

Or in `appsettings.Production.json` (excluded from git):

```json
{
  "RagSettings": {
    "OpenAI": {
      "ApiKey": "{{SECRET_FROM_KEY_VAULT}}"
    }
  }
}
```

---

## 🧪 Testing Your Configuration

### **1. Check Configuration**

After configuring, restart your application:

```bash
dotnet run
```

### **2. Test the Assistant**

1. Navigate to `/Assistant`
2. Try asking: "What's in my cart?"
3. If configured correctly, you should get an AI response
4. If not configured, you'll see a helpful error message

### **3. Verify Provider**

Check application logs to see which provider is being used:

```
[Information] Processing RAG request: {Message}
[Information] Executing tool: {ToolName}
```

---

## 📊 Provider Comparison

| Feature | OpenAI | Gemini | Claude |
|---------|--------|--------|--------|
| **Cost** | $$ | $ | $$$ |
| **Speed** | Fast | Very Fast | Medium |
| **Function Calling** | ✅ Excellent | ✅ Good | ✅ Excellent |
| **Response Quality** | Excellent | Very Good | Excellent |
| **Best For** | General use | Cost-effective | Complex reasoning |

### **Recommendations**

- **Starting Out**: OpenAI `gpt-4o-mini` (best balance)
- **Cost-Conscious**: Google Gemini `gemini-pro`
- **Best Quality**: Claude `claude-3-opus` or OpenAI `gpt-4o`

---

## 🔄 Switching Providers

To switch providers, simply change the `Provider` field:

```json
{
  "RagSettings": {
    "Provider": "Gemini"  // Change from "OpenAI" to "Gemini"
  }
}
```

**No code changes required!** The service automatically uses the correct provider.

---

## 🐛 Troubleshooting

### **Error: "API key is not configured"**

**Solution**: Add your API key to `appsettings.json` or User Secrets.

### **Error: "Unsupported provider"**

**Solution**: Check that `Provider` is exactly one of: `"OpenAI"`, `"Gemini"`, or `"Claude"` (case-sensitive).

### **Error: "I'm having trouble connecting to [Provider]"**

**Possible Causes**:
1. Invalid API key
2. Network connectivity issues
3. API rate limits exceeded
4. Incorrect endpoint URL

**Solutions**:
1. Verify API key is correct
2. Check internet connection
3. Wait a few minutes and try again
4. Verify endpoint URL matches provider

### **Error: "I couldn't generate a response"**

**Possible Causes**:
1. API quota exceeded
2. Invalid model name
3. Request format issue

**Solutions**:
1. Check API usage/quota
2. Verify model name is correct
3. Check application logs for details

---

## 📝 Configuration Parameters

### **Common Settings**

- **MaxTokens**: Maximum response length (default: 500)
- **Temperature**: Response creativity 0.0-1.0 (default: 0.7)
  - Lower = More focused/deterministic
  - Higher = More creative/varied

### **Provider-Specific**

Each provider has its own:
- **ApiKey**: Your API key
- **Endpoint**: API endpoint URL
- **Model**: Model name to use
- **Version**: (Claude only) API version

---

## ✅ Quick Start Checklist

- [ ] Choose a provider (OpenAI, Gemini, or Claude)
- [ ] Get API key from provider's website
- [ ] Update `appsettings.json` with provider settings
- [ ] Set `Provider` field to your chosen provider
- [ ] Restart application
- [ ] Test at `/Assistant` page
- [ ] Verify AI responses work correctly

---

## 🎯 Summary

The VeloStore AI Assistant supports **three major LLM providers** with a simple configuration switch. Just:

1. **Choose** your provider
2. **Add** your API key
3. **Set** the Provider field
4. **Done!** No code changes needed

**Recommended for beginners**: Start with OpenAI `gpt-4o-mini` for the best balance of cost, speed, and quality.

---

**Last Updated**: 2024  
**Maintained By**: VeloStore Development Team

