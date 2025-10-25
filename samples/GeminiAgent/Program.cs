// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.Bootstrap;
using NTG.Adk.Implementations.Models;
using NTG.Adk.Implementations.Tools;
using NTG.Adk.Operators.Agents;

namespace GeminiAgent;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== NTG.Adk - Gemini LLM Demo ===\n");

        // Check for API key or Google Cloud credentials
        var apiKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");

        if (string.IsNullOrEmpty(apiKey) && string.IsNullOrEmpty(projectId))
        {
            Console.WriteLine("⚠️  No Google credentials found!");
            Console.WriteLine("Set GOOGLE_API_KEY or configure GOOGLE_CLOUD_PROJECT + Application Default Credentials");
            Console.WriteLine("\nUsing MockLlm instead for demonstration...\n");

            // Fallback to MockLlm for demo
            var mockLlm = new MockLlm();
            var mockAgent = new LlmAgent(mockLlm, "mock-model")
            {
                Name = "Assistant",
                Instruction = "You are a helpful assistant."
            };

            var mockRunner = new Runner(mockAgent);
            var mockResult = await mockRunner.RunAsync("Hello! Tell me about C# and ADK.");
            Console.WriteLine($"MockLlm Response: {mockResult}\n");
            return;
        }

        // Create GeminiLlm instance
        GeminiLlm llm;
        if (!string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("✅ Using Google API Key authentication\n");
            llm = new GeminiLlm("gemini-2.0-flash-exp", apiKey, projectId ?? "default");
        }
        else
        {
            Console.WriteLine("✅ Using Application Default Credentials\n");
            llm = new GeminiLlm("gemini-2.0-flash-exp");
        }

        // Example 1: Basic LLM Agent
        Console.WriteLine("--- Example 1: Basic Agent ---");
        var basicAgent = new LlmAgent(llm, "gemini-2.0-flash-exp")
        {
            Name = "Assistant",
            Instruction = "You are a helpful AI assistant. Provide concise, accurate answers."
        };

        var runner = new Runner(basicAgent);
        var result = await runner.RunAsync("What is the ADK (Agent Development Kit)?");
        Console.WriteLine($"Response: {result}\n");

        // Example 2: Agent with Tools
        Console.WriteLine("--- Example 2: Agent with Function Calling ---");
        var weatherTool = FunctionTool.Create(
            (string city) => $"Weather in {city}: 22°C, Sunny",
            "get_weather",
            "Get current weather for a city"
        );

        var toolAgent = new LlmAgent(llm, "gemini-2.0-flash-exp")
        {
            Name = "WeatherAssistant",
            Instruction = "You help users get weather information. Use the get_weather function when needed.",
            Tools = [weatherTool]
        };

        var toolRunner = new Runner(toolAgent);
        var weatherResult = await toolRunner.RunAsync("What's the weather like in Paris?");
        Console.WriteLine($"Response: {weatherResult}\n");

        // Example 3: Streaming Responses
        Console.WriteLine("--- Example 3: Streaming Responses ---");
        var streamAgent = new LlmAgent(llm, "gemini-2.0-flash-exp")
        {
            Name = "StreamAgent",
            Instruction = "You are a helpful assistant. Provide concise responses."
        };

        var streamRunner = new Runner(streamAgent);
        Console.Write("Streaming: ");

        await foreach (var evt in streamRunner.RunStreamAsync("Tell me three benefits of using ADK"))
        {
            if (evt.Content?.Parts != null)
            {
                foreach (var part in evt.Content.Parts)
                {
                    if (part.Text != null)
                    {
                        Console.Write(part.Text);
                    }
                }
            }
        }
        Console.WriteLine("\n");

        Console.WriteLine("=== Demo Complete ===");
    }
}
