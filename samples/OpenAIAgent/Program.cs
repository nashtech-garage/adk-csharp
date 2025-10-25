// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.Bootstrap;
using NTG.Adk.Implementations.Models;
using NTG.Adk.Implementations.Tools;
using NTG.Adk.Operators.Agents;

namespace OpenAIAgent;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== NTG.Adk - OpenAI LLM Demo ===\n");

        // Check for API key
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("⚠️  No OpenAI API key found!");
            Console.WriteLine("Set OPENAI_API_KEY environment variable");
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

        // Create OpenAILlm instance
        Console.WriteLine("✅ Using OpenAI API Key authentication\n");
        var llm = new OpenAILlm("gpt-4o-mini", apiKey);

        // Example 1: Basic LLM Agent
        Console.WriteLine("--- Example 1: Basic Agent ---");
        var basicAgent = new LlmAgent(llm, "gpt-4o-mini")
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

        var calculatorTool = FunctionTool.Create(
            (int a, int b) => a + b,
            "add_numbers",
            "Add two numbers together"
        );

        var toolAgent = new LlmAgent(llm, "gpt-4o-mini")
        {
            Name = "ToolAssistant",
            Instruction = "You help users with weather and calculations. Use the available tools when needed.",
            Tools = [weatherTool, calculatorTool]
        };

        var toolRunner = new Runner(toolAgent);
        var weatherResult = await toolRunner.RunAsync("What's the weather like in Paris?");
        Console.WriteLine($"Weather Response: {weatherResult}");

        var calcResult = await toolRunner.RunAsync("What is 25 + 17?");
        Console.WriteLine($"Calculator Response: {calcResult}\n");

        // Example 3: Streaming Responses
        Console.WriteLine("--- Example 3: Streaming Responses ---");
        var streamAgent = new LlmAgent(llm, "gpt-4o-mini")
        {
            Name = "StreamAgent",
            Instruction = "You are a helpful assistant. Provide concise responses."
        };

        var streamRunner = new Runner(streamAgent);
        Console.Write("Streaming: ");

        await foreach (var evt in streamRunner.RunStreamAsync("Explain the benefits of multi-agent systems in 3 points"))
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

        // Example 4: Multi-turn Conversation
        Console.WriteLine("--- Example 4: Multi-turn Conversation ---");
        var conversationAgent = new LlmAgent(llm, "gpt-4o-mini")
        {
            Name = "ConversationAgent",
            Instruction = "You are a friendly assistant. Remember the conversation context."
        };

        var convRunner = new Runner(conversationAgent);

        var msg1 = await convRunner.RunAsync("My name is Alice and I love programming.");
        Console.WriteLine($"Agent: {msg1}");

        var msg2 = await convRunner.RunAsync("What is my name?");
        Console.WriteLine($"Agent: {msg2}");

        Console.WriteLine("=== Demo Complete ===");
    }
}
