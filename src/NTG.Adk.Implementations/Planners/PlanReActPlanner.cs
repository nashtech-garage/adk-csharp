// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.CoreAbstractions.Planners;

namespace NTG.Adk.Implementations.Planners;

/// <summary>
/// Plan-ReAct planner that structures LLM responses with explicit planning, reasoning, and action phases.
/// Equivalent to google.adk.planners.PlanReActPlanner in Python.
///
/// This planner uses structured tags to guide the LLM through a multi-step reasoning process:
/// 1. /*PLANNING*/ - Create a detailed plan before taking action
/// 2. /*ACTION*/ - Execute tool calls based on the plan
/// 3. /*REASONING*/ - Explain what was learned and what to do next
/// 4. /*REPLANNING*/ - Revise the plan if needed based on results
/// 5. /*FINAL_ANSWER*/ - Provide the final response
///
/// **Benefits:**
/// - Structured reasoning makes the agent's thought process visible
/// - Planning before action leads to better tool usage
/// - Reasoning between steps improves multi-step tasks
/// - Replanning enables recovery from failed approaches
///
/// **Use cases:**
/// - Complex multi-step tasks requiring coordination
/// - Research and analysis workflows
/// - Debugging and troubleshooting
/// - Any task where "thinking out loud" improves results
///
/// Note: Works with any LLM - no special model features required.
/// </summary>
public class PlanReActPlanner : IPlanner
{
    // Structured tags for response formatting
    public const string PlanningTag = "/*PLANNING*/";
    public const string ReplanningTag = "/*REPLANNING*/";
    public const string ReasoningTag = "/*REASONING*/";
    public const string ActionTag = "/*ACTION*/";
    public const string FinalAnswerTag = "/*FINAL_ANSWER*/";

    /// <summary>
    /// Creates a new PlanReActPlanner instance.
    /// </summary>
    public PlanReActPlanner()
    {
    }

    /// <summary>
    /// Builds the structured planning instruction.
    /// This instruction guides the LLM to follow the Plan-ReAct pattern.
    /// </summary>
    public string? BuildPlanningInstruction()
    {
        return BuildPlanReActInstruction();
    }

    /// <summary>
    /// Planner type identifier.
    /// </summary>
    public string PlannerType => "plan-react";

    /// <summary>
    /// Builds the complete Plan-ReAct instruction with all requirements.
    /// </summary>
    private static string BuildPlanReActInstruction()
    {
        var highLevelPreamble = $@"
When answering the question, try to leverage the available tools to gather the information instead of your memorized knowledge.

Follow this process when answering the question: (1) first come up with a plan in natural language text format; (2) Then use tools to execute the plan and provide reasoning between tool calls to make a summary of current state and next step. Tool calls and reasoning should be interleaved with each other. (3) In the end, return one final answer.

Follow this format when answering the question: (1) The planning part should be under {PlanningTag}. (2) The tool calls should be under {ActionTag}, and the reasoning parts should be under {ReasoningTag}. (3) The final answer part should be under {FinalAnswerTag}.
";

        var planningPreamble = $@"
Below are the requirements for the planning:
The plan is made to answer the user query if following the plan. The plan is coherent and covers all aspects of information from user query, and only involves the tools that are accessible by the agent. The plan contains the decomposed steps as a numbered list where each step should use one or multiple available tools. By reading the plan, you can intuitively know which tools to trigger or what actions to take.
If the initial plan cannot be successfully executed, you should learn from previous execution results and revise your plan. The revised plan should be under {ReplanningTag}. Then use tools to follow the new plan.
";

        var reasoningPreamble = @"
Below are the requirements for the reasoning:
The reasoning makes a summary of the current trajectory based on the user query and tool outputs. Based on the tool outputs and plan, the reasoning also comes up with instructions to the next steps, making the trajectory closer to the final answer.
";

        var finalAnswerPreamble = @"
Below are the requirements for the final answer:
The final answer should be precise and follow query formatting requirements. Some queries may not be answerable with the available tools and information. In those cases, inform the user why you cannot process their query and ask for more information.
";

        var toolUsagePreamble = @"
Below are the requirements for using tools:
- Use the available tools described in the context
- You cannot use any parameters or fields that are not explicitly defined in the tool descriptions
- Tool calls should be readable, efficient, and directly relevant to the user query and reasoning steps
- When using tools, call them by their defined names
";

        var userInputPreamble = @"
VERY IMPORTANT instruction that you MUST follow in addition to the above instructions:

You should ask for clarification if you need more information to answer the question.
You should prefer using the information available in the context instead of repeated tool use.
";

        return string.Join("\n\n", new[]
        {
            highLevelPreamble,
            planningPreamble,
            reasoningPreamble,
            finalAnswerPreamble,
            toolUsagePreamble,
            userInputPreamble
        });
    }

    /// <summary>
    /// Parse a Plan-ReAct formatted response to extract structured components.
    /// Useful for logging, debugging, or custom processing.
    /// </summary>
    public static PlanReActResponse ParseResponse(string responseText)
    {
        var response = new PlanReActResponse
        {
            Planning = ExtractSection(responseText, PlanningTag, ReplanningTag, ReasoningTag, ActionTag, FinalAnswerTag),
            Replanning = ExtractSection(responseText, ReplanningTag, PlanningTag, ReasoningTag, ActionTag, FinalAnswerTag),
            Reasoning = ExtractAllSections(responseText, ReasoningTag),
            Actions = ExtractAllSections(responseText, ActionTag),
            FinalAnswer = ExtractSection(responseText, FinalAnswerTag)
        };

        return response;
    }

    private static string? ExtractSection(string text, string startTag, params string[] endTags)
    {
        var startIndex = text.IndexOf(startTag, StringComparison.Ordinal);
        if (startIndex == -1)
            return null;

        startIndex += startTag.Length;

        // Find the nearest end tag
        var endIndex = text.Length;
        foreach (var endTag in endTags)
        {
            var tagIndex = text.IndexOf(endTag, startIndex, StringComparison.Ordinal);
            if (tagIndex != -1 && tagIndex < endIndex)
            {
                endIndex = tagIndex;
            }
        }

        var section = text.Substring(startIndex, endIndex - startIndex).Trim();
        return string.IsNullOrEmpty(section) ? null : section;
    }

    private static List<string> ExtractAllSections(string text, string tag)
    {
        var sections = new List<string>();
        var searchIndex = 0;

        while (true)
        {
            var startIndex = text.IndexOf(tag, searchIndex, StringComparison.Ordinal);
            if (startIndex == -1)
                break;

            startIndex += tag.Length;

            // Find next tag or end of text
            var endIndex = text.Length;
            var nextTagIndex = text.IndexOfAny(new[] { '/', '*' }, startIndex);

            // Look for next tag marker
            while (nextTagIndex != -1 && nextTagIndex < text.Length - 1)
            {
                if (text[nextTagIndex] == '/' && text[nextTagIndex + 1] == '*')
                {
                    endIndex = nextTagIndex;
                    break;
                }
                nextTagIndex = text.IndexOfAny(new[] { '/', '*' }, nextTagIndex + 1);
            }

            var section = text.Substring(startIndex, endIndex - startIndex).Trim();
            if (!string.IsNullOrEmpty(section))
            {
                sections.Add(section);
            }

            searchIndex = endIndex;
        }

        return sections;
    }
}

/// <summary>
/// Parsed structure of a Plan-ReAct response.
/// Useful for observability, logging, and debugging.
/// </summary>
public class PlanReActResponse
{
    /// <summary>
    /// The initial plan (from /*PLANNING*/ section)
    /// </summary>
    public string? Planning { get; init; }

    /// <summary>
    /// Revised plan if initial approach failed (from /*REPLANNING*/ section)
    /// </summary>
    public string? Replanning { get; init; }

    /// <summary>
    /// All reasoning sections (from /*REASONING*/ sections)
    /// Shows the agent's thought process between actions
    /// </summary>
    public List<string> Reasoning { get; init; } = new();

    /// <summary>
    /// All action sections (from /*ACTION*/ sections)
    /// Contains tool calls and their context
    /// </summary>
    public List<string> Actions { get; init; } = new();

    /// <summary>
    /// The final answer (from /*FINAL_ANSWER*/ section)
    /// This is what should be shown to the user
    /// </summary>
    public string? FinalAnswer { get; init; }

    /// <summary>
    /// Get a human-readable summary of the response structure
    /// </summary>
    public string GetSummary()
    {
        var parts = new List<string>();

        if (Planning != null)
            parts.Add($"Planning: {Planning.Length} chars");
        if (Replanning != null)
            parts.Add($"Replanning: {Replanning.Length} chars");
        if (Reasoning.Count > 0)
            parts.Add($"Reasoning: {Reasoning.Count} steps");
        if (Actions.Count > 0)
            parts.Add($"Actions: {Actions.Count} tool calls");
        if (FinalAnswer != null)
            parts.Add($"Final Answer: {FinalAnswer.Length} chars");

        return string.Join(", ", parts);
    }

    /// <summary>
    /// Get the user-facing response (final answer + context)
    /// </summary>
    public string GetUserResponse()
    {
        if (FinalAnswer != null)
            return FinalAnswer;

        // Fallback: if no final answer, return last reasoning
        if (Reasoning.Count > 0)
            return Reasoning[^1];

        return "No response generated.";
    }
}
