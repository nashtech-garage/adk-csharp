# OpenAIAgent Sample - Real OpenAI LLM Integration

Demonstrates using **OpenAILlm** adapter with OpenAI's GPT models for real LLM-powered agents.

## Features Demonstrated

- ✅ OpenAI Chat Completions API integration
- ✅ Basic agent usage with GPT-4o-mini
- ✅ Function calling with multiple tools
- ✅ Streaming responses
- ✅ Multi-turn conversations
- ✅ Environment variable configuration

## Prerequisites

### OpenAI API Key (Required)

```bash
export OPENAI_API_KEY="sk-..."
```

Get your API key from: https://platform.openai.com/api-keys

## Running the Sample

```bash
cd E:/repos/adk-csharp/samples/OpenAIAgent
dotnet run
```

### Without API Key (MockLlm fallback)

If no API key is found, the sample automatically falls back to **MockLlm** for demonstration:

```bash
dotnet run
# Output: Using MockLlm instead for demonstration...
```

## Code Examples

### Example 1: Basic Agent

```csharp
var llm = new OpenAILlm("gpt-4o-mini", apiKey);
var agent = new LlmAgent(llm, "gpt-4o-mini")
{
    Name = "Assistant",
    Instruction = "You are a helpful AI assistant."
};

var runner = new Runner(agent);
var result = await runner.RunAsync("What is ADK?");
Console.WriteLine(result);
```

### Example 2: Function Calling

```csharp
var weatherTool = FunctionTool.Create(
    (string city) => $"Weather in {city}: 22°C, Sunny",
    "get_weather",
    "Get current weather for a city"
);

var calculatorTool = FunctionTool.Create(
    (int a, int b) => a + b,
    "add_numbers",
    "Add two numbers together"
);

var agent = new LlmAgent(llm, "gpt-4o-mini")
{
    Name = "ToolAssistant",
    Instruction = "Use the available tools when needed.",
    Tools = [weatherTool, calculatorTool]
};

var result = await new Runner(agent).RunAsync("What's the weather in Paris?");
// LLM calls the get_weather function automatically
```

### Example 3: Streaming Responses

```csharp
var agent = new LlmAgent(llm, "gpt-4o-mini")
{
    Name = "StreamAgent",
    Instruction = "You are helpful."
};

await foreach (var evt in new Runner(agent).RunStreamAsync("Explain multi-agent systems"))
{
    if (evt.Content?.Parts != null)
    {
        foreach (var part in evt.Content.Parts)
        {
            Console.Write(part.Text);
        }
    }
}
```

### Example 4: Multi-turn Conversation

```csharp
var agent = new LlmAgent(llm, "gpt-4o-mini")
{
    Name = "ConversationAgent",
    Instruction = "Remember the conversation context."
};

var runner = new Runner(agent);

await runner.RunAsync("My name is Alice.");
var response = await runner.RunAsync("What is my name?");
// LLM should remember and respond with "Alice"
```

## Architecture

```
OpenAIAgent (Bootstrap)
    ↓
Runner (Bootstrap)
    ↓
LlmAgent (Operators)
    ↓
OpenAILlm (Implementations)
    ↓
OpenAI SDK 2.5.0
    ↓
OpenAI Chat Completions API
```

## Supported Models

- `gpt-4o-mini` (Fast, cost-effective)
- `gpt-4o` (Most capable)
- `gpt-4-turbo` (Extended context)
- `gpt-3.5-turbo` (Legacy, fast)

## Key Differences from Google Gemini

| Feature | OpenAI | Google Gemini |
|---------|--------|---------------|
| **Message Format** | ChatMessage list (User/Assistant/System) | Content with Parts |
| **Function Calling** | ChatToolCall with JSON args | FunctionCall in Parts |
| **Streaming** | StreamingChatCompletionUpdate | AsyncResponseStream |
| **Authentication** | API key only | API key + Application Default Credentials |
| **Model Names** | `gpt-4o-mini`, `gpt-4o` | `gemini-2.0-flash-exp`, `gemini-1.5-pro` |

## Configuration Options

```csharp
var agent = new LlmAgent(llm, "gpt-4o-mini")
{
    Name = "MyAgent",
    Instruction = "System prompt here",
    Tools = [tool1, tool2],
    // Config options passed to LLM:
    // - Temperature (0.0-2.0)
    // - TopP (0.0-1.0)
    // - MaxOutputTokens
    // - StopSequences
};
```

## Troubleshooting

### Error: "No OpenAI API key found"
- Set `OPENAI_API_KEY` environment variable
- Get API key from https://platform.openai.com/api-keys

### Error: "Insufficient quota"
- Check your OpenAI account billing
- Ensure you have credits available

### Build errors
```bash
cd E:/repos/adk-csharp
dotnet build
```

## Cost Considerations

OpenAI API charges per token:
- **gpt-4o-mini**: $0.150 / 1M input tokens, $0.600 / 1M output tokens
- **gpt-4o**: $2.50 / 1M input tokens, $10.00 / 1M output tokens

For testing, start with `gpt-4o-mini` for cost efficiency.

## Next Steps

- See [STATUS.md](../../STATUS.md) for roadmap
- Check [GETTING_STARTED.md](../../GETTING_STARTED.md) for tutorials
- Compare with [GeminiAgent](../GeminiAgent) for Google Gemini usage

## Version

NTG.Adk **v0.2.0-alpha** - OpenAI LLM integration ✅
