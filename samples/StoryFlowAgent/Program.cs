// Copyright 2025 NTG
// StoryFlowAgent Sample - Multi-Agent Workflow
// Demonstrates SequentialAgent, LoopAgent, Custom Agent orchestration

using NTG.Adk.Bootstrap;
using NTG.Adk.Implementations.Models;
using NTG.Adk.Implementations.Sessions;
using NTG.Adk.Operators.Agents;
using StoryFlowSample;

Console.WriteLine("=== NTG.Adk StoryFlow Agent ===");
Console.WriteLine("Multi-Agent Workflow: Generate → Critique Loop → Check → Regen\n");

// Setup LLM
var llm = new MockLlm();

// Define individual LLM agents
var storyGenerator = new LlmAgent(llm, "gemini-2.5-flash")
{
    Name = "StoryGenerator",
    Instruction = "Write a short story (around 100 words) about the topic in state key 'topic'",
    OutputKey = "current_story"
};

var critic = new LlmAgent(llm, "gemini-2.5-flash")
{
    Name = "Critic",
    Instruction = "Review the story in state key 'current_story'. Provide 1-2 sentences of constructive criticism.",
    OutputKey = "criticism"
};

var reviser = new LlmAgent(llm, "gemini-2.5-flash")
{
    Name = "Reviser",
    Instruction = "Revise the story in state key 'current_story' based on the criticism in state key 'criticism'. Output only the revised story.",
    OutputKey = "current_story" // Overwrites original
};

var grammarCheck = new LlmAgent(llm, "gemini-2.5-flash")
{
    Name = "GrammarCheck",
    Instruction = "Check grammar of story in state key 'current_story'. Output corrections or 'Grammar is good!'",
    OutputKey = "grammar_suggestions"
};

var toneCheck = new LlmAgent(llm, "gemini-2.5-flash")
{
    Name = "ToneCheck",
    Instruction = "Analyze tone of story in state key 'current_story'. Output one word: 'positive', 'negative', or 'neutral'",
    OutputKey = "tone_check_result"
};

// Create custom StoryFlowAgent
var storyFlowAgent = new StoryFlowAgentExample(
    "StoryFlowAgent",
    storyGenerator,
    critic,
    reviser,
    grammarCheck,
    toneCheck);

// Setup session with initial state
var session = new InMemorySession();
session.State.Set("topic", "a brave kitten exploring a haunted house");

var factory = new InvocationContextFactory();
var context = factory.Create(session, userInput: "Generate a story about the topic in state");

// Run workflow
Console.WriteLine("Starting workflow...\n");
await foreach (var evt in storyFlowAgent.RunAsync(context))
{
    // Events are logged by the agent
}

// Display final state
Console.WriteLine("\n=== Final Session State ===");
Console.WriteLine($"Story: {session.State.Get<string>("current_story")}");
Console.WriteLine($"Criticism: {session.State.Get<string>("criticism")}");
Console.WriteLine($"Grammar: {session.State.Get<string>("grammar_suggestions")}");
Console.WriteLine($"Tone: {session.State.Get<string>("tone_check_result")}");

Console.WriteLine("\n=== Workflow Components ===");
Console.WriteLine("✅ Custom BaseAgent (StoryFlowAgentExample)");
Console.WriteLine("✅ LoopAgent (CriticReviserLoop)");
Console.WriteLine("✅ SequentialAgent (PostProcessing)");
Console.WriteLine("✅ LlmAgent x5 (Generator, Critic, Reviser, Grammar, Tone)");
Console.WriteLine("✅ State passing between agents");
Console.WriteLine("✅ Conditional logic (tone-based regen)");
