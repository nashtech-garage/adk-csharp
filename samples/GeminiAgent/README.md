# GeminiAgent Sample - Real Gemini LLM Integration

Demonstrates using **GeminiLlm** adapter with Google's Gemini API for real LLM-powered agents.

## Features Demonstrated

- ✅ Google Cloud AI Platform V1 integration
- ✅ Basic agent usage with real LLM
- ✅ Function calling with tools
- ✅ Streaming responses
- ✅ Environment variable configuration

## Prerequisites

### Option 1: Google API Key (Recommended for development)

```bash
export GOOGLE_API_KEY="your-api-key-here"
export GOOGLE_CLOUD_PROJECT="your-project-id"  # Optional
```

Get your API key from: https://aistudio.google.com/apikey

### Option 2: Application Default Credentials (Production)

```bash
# Install gcloud CLI and authenticate
gcloud auth application-default login

export GOOGLE_CLOUD_PROJECT="your-project-id"
export GOOGLE_CLOUD_LOCATION="us-central1"  # Optional
```

## Running the Sample

```bash
cd E:/repos/adk-csharp/samples/GeminiAgent
dotnet run
```

### Without Credentials (MockLlm fallback)

If no credentials are found, the sample automatically falls back to **MockLlm** for demonstration:

```bash
dotnet run
# Output: Using MockLlm instead for demonstration...
```

## Code Examples

### Example 1: Basic Agent

```csharp
var llm = new GeminiLlm("gemini-2.0-flash-exp", apiKey);
var agent = new LlmAgent(llm, "gemini-2.0-flash-exp")
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

var agent = new LlmAgent(llm, "gemini-2.0-flash-exp")
{
    Name = "WeatherAssistant",
    Instruction = "Help users get weather. Use get_weather when needed.",
    Tools = [weatherTool]
};

var result = await new Runner(agent).RunAsync("What's the weather in Paris?");
// LLM calls the get_weather function automatically
```

### Example 3: Streaming Responses

```csharp
var agent = new LlmAgent(llm, "gemini-2.0-flash-exp")
{
    Name = "StreamAgent",
    Instruction = "You are helpful."
};

await foreach (var evt in new Runner(agent).RunStreamAsync("Tell me about ADK"))
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

## Architecture

```
GeminiAgent (Bootstrap)
    ↓
Runner (Bootstrap)
    ↓
LlmAgent (Operators)
    ↓
GeminiLlm (Implementations)
    ↓
Google Cloud AI Platform V1 SDK
    ↓
Gemini API
```

## Supported Models

- `gemini-2.0-flash-exp` (Latest experimental)
- `gemini-1.5-flash` (Fast, efficient)
- `gemini-1.5-pro` (Advanced reasoning)

## Configuration Options

```csharp
var agent = new LlmAgent(llm, "gemini-2.0-flash-exp")
{
    Name = "MyAgent",
    Instruction = "System prompt here",
    Tools = [tool1, tool2],
    // Config options passed to LLM:
    // - Temperature (0.0-2.0)
    // - TopP (0.0-1.0)
    // - TopK
    // - MaxOutputTokens
};
```

## Troubleshooting

### Error: "No Google credentials found"
- Set `GOOGLE_API_KEY` environment variable
- Or run `gcloud auth application-default login`

### Error: "Project not found"
- Set `GOOGLE_CLOUD_PROJECT` environment variable

### Build errors
```bash
cd E:/repos/adk-csharp
dotnet build
```

## Next Steps

- See [STATUS.md](../../STATUS.md) for roadmap
- Check [GETTING_STARTED.md](../../GETTING_STARTED.md) for tutorials
- Explore [StoryFlowAgent](../StoryFlowAgent) for multi-agent workflows

## Version

NTG.Adk **v0.2.0-alpha** - First release with real LLM integration 🎉
