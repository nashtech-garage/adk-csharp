// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.Bootstrap;
using NTG.Adk.CoreAbstractions.Models;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.Implementations.Models;
using NTG.Adk.Implementations.Sessions;
using NTG.Adk.Implementations.Tools;
using NTG.Adk.Operators.Agents;

namespace AutoFlowAgent;

/// <summary>
/// Demonstrates AutoFlow - automatic agent delegation based on LLM decisions.
///
/// Architecture:
/// - Coordinator (root agent with AutoFlow enabled)
///   - MathSpecialist (handles calculations)
///   - StorySpecialist (handles creative writing)
///   - CodeSpecialist (handles programming questions)
///
/// When AutoFlow is enabled (default: true), the transfer_to_agent tool is
/// automatically added to the coordinator. The LLM can then decide which
/// specialist to delegate to based on the user's query.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         NTG.Adk - AutoFlow Multi-Agent Demo               ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Create LLM (try Gemini, OpenAI, or fall back to Mock)
        var llm = CreateLlm();
        Console.WriteLine($"Using LLM: {llm.GetType().Name}");
        Console.WriteLine();

        // Create specialized agents
        var mathSpecialist = new LlmAgent(llm, GetModelName())
        {
            Name = "MathSpecialist",
            Description = "Expert in mathematics, calculations, and problem solving",
            Instruction = @"You are a mathematics expert. Solve mathematical problems step-by-step.
                           Show your work and explain the reasoning.",
            OutputKey = "math_result"
        };

        var storySpecialist = new LlmAgent(llm, GetModelName())
        {
            Name = "StorySpecialist",
            Description = "Creative writer specializing in stories and narratives",
            Instruction = @"You are a creative writer. Write engaging stories with vivid details.
                           Make them interesting and age-appropriate.",
            OutputKey = "story_result"
        };

        var codeSpecialist = new LlmAgent(llm, GetModelName())
        {
            Name = "CodeSpecialist",
            Description = "Programming expert who writes clean, documented code",
            Instruction = @"You are a programming expert. Write clean, well-documented code.
                           Include comments explaining the logic and best practices used.",
            OutputKey = "code_result"
        };

        // Create coordinator with AutoFlow enabled (default)
        var coordinator = new LlmAgent(llm, GetModelName())
        {
            Name = "Coordinator",
            Instruction = @"You are a helpful coordinator that routes questions to specialists.

                           You have access to three specialists:
                           - MathSpecialist: For math problems, calculations, equations
                           - StorySpecialist: For creative writing, stories, narratives
                           - CodeSpecialist: For programming, algorithms, code examples

                           Analyze the user's query and transfer to the appropriate specialist.
                           Use the transfer_to_agent tool to delegate the task.",
            EnableAutoFlow = true  // Auto-adds transfer_to_agent tool
        };

        // Add specialists as sub-agents (this enables AutoFlow routing)
        coordinator.AddSubAgents(mathSpecialist, storySpecialist, codeSpecialist);

        // Create context
        var context = InvocationContext.Create();

        // Demonstrate AutoFlow with different types of queries
        await RunScenario(coordinator, context, "Calculate the square root of 144 and explain the steps");
        await RunScenario(coordinator, context, "Write a short story about a brave robot");
        await RunScenario(coordinator, context, "Write a function in C# to check if a number is prime");

        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    Demo Complete                          ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine("Key AutoFlow Features Demonstrated:");
        Console.WriteLine("✓ Automatic transfer_to_agent tool injection");
        Console.WriteLine("✓ LLM-driven delegation decisions");
        Console.WriteLine("✓ Hierarchical agent routing (FindAgent)");
        Console.WriteLine("✓ Seamless execution transfer between agents");
        Console.WriteLine("✓ Event streaming across agent boundaries");
        Console.WriteLine();
    }

    static async Task RunScenario(LlmAgent coordinator, IInvocationContext context, string query)
    {
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        Console.WriteLine($"Query: {query}");
        Console.WriteLine("─────────────────────────────────────────────────────────────");
        Console.WriteLine();

        // Update context with new user input
        var updatedContext = context.WithUserInput(query);

        // Run coordinator - it will automatically delegate via AutoFlow
        await foreach (var evt in coordinator.RunAsync(updatedContext))
        {
            var role = evt.Content?.Role ?? "unknown";
            var text = evt.Content?.Parts?.FirstOrDefault()?.Text;

            var functionCall = evt.Content?.Parts?.FirstOrDefault()?.FunctionCall;
            var functionResponse = evt.Content?.Parts?.FirstOrDefault()?.FunctionResponse;

            if (functionCall != null)
            {
                Console.WriteLine($"[{evt.Author}] 🔧 Calling: {functionCall.Name}");

                if (functionCall.Args != null)
                {
                    foreach (var arg in functionCall.Args)
                    {
                        Console.WriteLine($"    {arg.Key}: {arg.Value}");
                    }
                }
            }
            else if (functionResponse != null)
            {
                Console.WriteLine($"[{evt.Author}] ⚙️ Tool Response: {functionResponse.Response}");
            }
            else if (!string.IsNullOrEmpty(text))
            {
                Console.WriteLine($"[{evt.Author}] {text}");
            }

            // Show transfer action if present
            if (!string.IsNullOrEmpty(evt.Actions?.TransferTo))
            {
                Console.WriteLine($"    → Transferring to: {evt.Actions.TransferTo}");
            }
        }

        Console.WriteLine();
    }

    static ILlm CreateLlm()
    {
        // Try Gemini first
        var geminiKey = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_API_KEY")
                       ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (!string.IsNullOrEmpty(geminiKey))
        {
            try
            {
                return new GeminiLlm("gemini-2.0-flash-exp", geminiKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to initialize Gemini: {ex.Message}");
            }
        }

        // Try OpenAI
        var openaiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(openaiKey))
        {
            try
            {
                return new OpenAILlm("gpt-4o-mini", openaiKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to initialize OpenAI: {ex.Message}");
            }
        }

        // Fallback to mock
        Console.WriteLine("Warning: No API keys found. Using MockLlm for demonstration.");
        Console.WriteLine("Set GOOGLE_CLOUD_API_KEY or OPENAI_API_KEY to use real LLMs.");
        Console.WriteLine();

        return new MockLlm();
    }

    static string GetModelName()
    {
        // Check which LLM is being used
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GOOGLE_CLOUD_API_KEY"))
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GEMINI_API_KEY")))
        {
            return "gemini-2.0-flash-exp";
        }

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return "gpt-4o-mini";
        }

        return "mock-model";
    }
}
