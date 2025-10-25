// Copyright 2025 NTG
// Custom StoryFlowAgent demonstrating multi-agent orchestration
// Equivalent to Python ADK's StoryFlowAgent example

using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.Operators.Agents;
using NTG.Adk.Operators.Workflows;

namespace StoryFlowSample;

/// <summary>
/// Custom agent for story generation and refinement workflow.
/// Orchestrates: Generate → Critique/Revise Loop → Grammar/Tone Check → Conditional Regen
///
/// Equivalent to Python:
/// class StoryFlowAgent(BaseAgent):
///     async def _run_async_impl(self, ctx):
///         # 1. Generate story
///         # 2. Loop: Critique → Revise
///         # 3. Sequential: Grammar Check → Tone Check
///         # 4. If tone negative, regenerate
/// </summary>
public class StoryFlowAgentExample : BaseAgent
{
    public IAgent StoryGenerator { get; init; }
    public IAgent Critic { get; init; }
    public IAgent Reviser { get; init; }
    public IAgent GrammarCheck { get; init; }
    public IAgent ToneCheck { get; init; }

    private readonly LoopAgent _criticReviserLoop;
    private readonly SequentialAgent _postProcessing;

    public StoryFlowAgentExample(
        string name,
        IAgent storyGenerator,
        IAgent critic,
        IAgent reviser,
        IAgent grammarCheck,
        IAgent toneCheck)
    {
        Name = name;
        Description = "Custom story workflow: generate → critique loop → checks → conditional regen";

        StoryGenerator = storyGenerator;
        Critic = critic;
        Reviser = reviser;
        GrammarCheck = grammarCheck;
        ToneCheck = toneCheck;

        // Create internal workflow agents
        _criticReviserLoop = new LoopAgent(
            "CriticReviserLoop",
            [critic, reviser],
            maxIterations: 2)
        {
            Name = "CriticReviserLoop"
        };

        _postProcessing = new SequentialAgent(
            "PostProcessing",
            [grammarCheck, toneCheck])
        {
            Name = "PostProcessing"
        };

        // Register top-level sub-agents
        AddSubAgents(storyGenerator, _criticReviserLoop, _postProcessing);
    }

    protected override async IAsyncEnumerable<IEvent> RunAsyncImpl(
        IInvocationContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[{Name}] Starting story generation workflow...");

        // Step 1: Generate initial story
        Console.WriteLine($"[{Name}] Running StoryGenerator...");
        await foreach (var evt in StoryGenerator.RunAsync(context, cancellationToken))
        {
            yield return evt;
        }

        // Verify story was generated
        if (!context.Session.State.Contains("current_story"))
        {
            Console.WriteLine($"[{Name}] ERROR: Failed to generate story!");
            yield break;
        }

        var story = context.Session.State.Get<string>("current_story");
        var preview = story?.Length > 50 ? story[..50] : story ?? "";
        Console.WriteLine($"[{Name}] Initial story: {preview}...");

        // Step 2: Critic-Reviser Loop (2 iterations)
        Console.WriteLine($"[{Name}] Running CriticReviserLoop (max 2 iterations)...");
        await foreach (var evt in _criticReviserLoop.RunAsync(context, cancellationToken))
        {
            yield return evt;
        }

        story = context.Session.State.Get<string>("current_story");
        preview = story?.Length > 50 ? story[..50] : story ?? "";
        Console.WriteLine($"[{Name}] Story after revision: {preview}...");

        // Step 3: Post-Processing (Grammar → Tone)
        Console.WriteLine($"[{Name}] Running PostProcessing (grammar + tone check)...");
        await foreach (var evt in _postProcessing.RunAsync(context, cancellationToken))
        {
            yield return evt;
        }

        // Step 4: Conditional regeneration based on tone
        var toneResult = context.Session.State.Get<string>("tone_check_result");
        Console.WriteLine($"[{Name}] Tone check result: {toneResult}");

        if (toneResult == "negative")
        {
            Console.WriteLine($"[{Name}] Tone is negative! Regenerating story...");
            await foreach (var evt in StoryGenerator.RunAsync(context, cancellationToken))
            {
                yield return evt;
            }
        }
        else
        {
            Console.WriteLine($"[{Name}] Tone is acceptable. Workflow complete!");
        }

        Console.WriteLine($"[{Name}] Final story: {context.Session.State.Get<string>("current_story")}");
    }
}
