// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using NTG.Adk.Implementations.Models;
using NTG.Adk.Implementations.Tools.BuiltIn;
using NTG.Adk.Operators.Agents;
using NTG.Adk.Operators.Runners;

Console.WriteLine("========================================");
Console.WriteLine("  NTG.Adk - Built-in Tools Demo");
Console.WriteLine("========================================\n");

// Demo 1: Agent with Code Execution Tool
Console.WriteLine("==> Demo 1: Agent with CodeExecutionTool\n");

var codeExecutor = new CodeExecutionTool();
var llm = new MockLlm();

var codeAgent = new LlmAgent(llm, "mock-llm")
{
    Name = "CodeAssistant",
    Instruction = "You are a helpful coding assistant with code execution capabilities.",
    Tools = [codeExecutor]
};

var runner1 = new InMemoryRunner(codeAgent, appName: "BuiltInToolsApp");

Console.WriteLine("Agent: CodeAssistant with CodeExecutionTool");
Console.WriteLine("Query: Calculate fibonacci(10)\n");

await foreach (var evt in runner1.RunAsync(
    userId: "demo_user",
    sessionId: "demo_session_1",
    userInput: "Calculate fibonacci(10)"))
{
    if (evt.Content?.Parts != null)
    {
        foreach (var part in evt.Content.Parts)
        {
            if (part.Text != null)
            {
                Console.WriteLine($"[{evt.Author}] {part.Text}");
            }
        }
    }
}

// Demo 2: Agent with Google Search Tool (requires API key)
Console.WriteLine("\n==> Demo 2: Agent with GoogleSearchTool\n");

var googleApiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
var searchEngineId = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID");

if (!string.IsNullOrEmpty(googleApiKey) && !string.IsNullOrEmpty(searchEngineId))
{
    var searchTool = new GoogleSearchTool(googleApiKey, searchEngineId);

    var searchAgent = new LlmAgent(llm, "mock-llm")
    {
        Name = "SearchAssistant",
        Instruction = "You are a search assistant with web search capabilities.",
        Tools = [searchTool]
    };

    var runner2 = new InMemoryRunner(searchAgent, appName: "BuiltInToolsApp");

    Console.WriteLine("Agent: SearchAssistant with GoogleSearchTool");
    Console.WriteLine("Query: Search for C# Agent Development Kit\n");

    await foreach (var evt in runner2.RunAsync(
        userId: "demo_user",
        sessionId: "demo_session_2",
        userInput: "Search for C# Agent Development Kit"))
    {
        if (evt.Content?.Parts != null)
        {
            foreach (var part in evt.Content.Parts)
            {
                if (part.Text != null)
                {
                    Console.WriteLine($"[{evt.Author}] {part.Text}");
                }
            }
        }
    }
}
else
{
    Console.WriteLine("Skipped: Set GOOGLE_API_KEY and GOOGLE_SEARCH_ENGINE_ID");
    Console.WriteLine("Get API key: https://developers.google.com/custom-search/v1/overview");
}

// Demo 3: Agent with Multiple Tools
Console.WriteLine("\n==> Demo 3: Agent with Multiple Tools\n");

var tools = new List<NTG.Adk.CoreAbstractions.Tools.ITool> { codeExecutor };

if (!string.IsNullOrEmpty(googleApiKey) && !string.IsNullOrEmpty(searchEngineId))
{
    tools.Add(new GoogleSearchTool(googleApiKey, searchEngineId));
}

var multiToolAgent = new LlmAgent(llm, "mock-llm")
{
    Name = "MultiToolAgent",
    Instruction = "You are an assistant with multiple tool capabilities.",
    Tools = tools
};

var runner3 = new InMemoryRunner(multiToolAgent, appName: "BuiltInToolsApp");

Console.WriteLine($"Agent: MultiToolAgent with {multiToolAgent.Tools.Count} tool(s)");
Console.WriteLine("Demonstrates agent with multiple built-in tools\n");

Console.WriteLine("\n========================================");
Console.WriteLine("  Demo completed!");
Console.WriteLine("========================================");
