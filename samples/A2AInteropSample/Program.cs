// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using NTG.Adk.Implementations.Models;
using NTG.Adk.Operators.Agents;
using NTG.Adk.Operators.Runners;
using NTG.Adk.Operators.A2A;

Console.WriteLine("=== ADK A2A Interop Sample ===\n");

// Create agent with mock LLM
var llm = new MockLlm();
var agent = new LlmAgent(llm, "mock-llm")
{
    Name = "A2AEchoAgent",
    Description = "An agent accessible via A2A protocol",
    Instruction = "Echo user messages with 'ADK says:' prefix"
};

// Create runner with in-memory services
var runner = new InMemoryRunner(agent, appName: "A2AInteropApp");

// Create A2A executor
var a2aExecutor = new A2aAgentExecutor(runner);

Console.WriteLine("Agent: HelloAgent");
Console.WriteLine("Ready to receive A2A messages\n");

// Simulate A2A message
var a2aMessage = new A2A.AgentMessage
{
    MessageId = Guid.NewGuid().ToString(),
    Role = A2A.MessageRole.User,
    Parts = new List<A2A.Part>
    {
        new A2A.TextPart { Text = "Hello from A2A!" }
    }
};

Console.WriteLine($"[A2A Request] {((A2A.TextPart)a2aMessage.Parts[0]).Text}");
Console.WriteLine();

// Execute through A2A executor
var taskId = Guid.NewGuid().ToString();
var contextId = "ADK/A2AInteropApp/user001/session001";

await foreach (var a2aEvent in a2aExecutor.ExecuteAsync(a2aMessage, taskId, contextId))
{
    if (a2aEvent is A2A.TaskStatusUpdateEvent statusEvent)
    {
        Console.WriteLine($"[A2A Event] Status: {statusEvent.Status.State}");

        if (statusEvent.Status.Message != null && statusEvent.Status.Message.Parts.Count > 0)
        {
            var firstPart = statusEvent.Status.Message.Parts[0];
            if (firstPart is A2A.TextPart textPart)
            {
                Console.WriteLine($"  Message: {textPart.Text}");
            }
        }
    }
    else if (a2aEvent is A2A.TaskArtifactUpdateEvent artifactEvent)
    {
        Console.WriteLine($"[A2A Event] Artifact:");
        foreach (var part in artifactEvent.Artifact.Parts)
        {
            if (part is A2A.TextPart textPart)
            {
                Console.WriteLine($"  {textPart.Text}");
            }
        }
    }
}

Console.WriteLine("\n=== A2A Interop Demo Complete ===");
