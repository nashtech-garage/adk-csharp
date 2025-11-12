// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using System.Text.RegularExpressions;
using NTG.Adk.Boundary.Events;
using NTG.Adk.CoreAbstractions.Agents;
using NTG.Adk.CoreAbstractions.Events;
using NTG.Adk.CoreAbstractions.Models;
using NTG.Adk.CoreAbstractions.Sessions;
using NTG.Adk.CoreAbstractions.Tools;
using NTG.Adk.Implementations.Tools;

namespace NTG.Adk.Operators.Agents;

/// <summary>
/// Full-featured LLM agent with tools, state templating, and sub-agent delegation.
/// Equivalent to google.adk.agents.LlmAgent in Python.
/// </summary>
public class LlmAgent : BaseAgent
{
    private readonly ILlm _llm;

    /// <summary>
    /// System instruction for the agent.
    /// Supports template variables: {var} for state, {artifact.var} for artifacts.
    /// </summary>
    public string? Instruction { get; init; }

    /// <summary>
    /// State key to save the agent's final output
    /// </summary>
    public string? OutputKey { get; init; }

    /// <summary>
    /// Tools available to this agent
    /// </summary>
    public IReadOnlyList<ITool>? Tools { get; init; }

    /// <summary>
    /// Whether to enable AutoFlow (automatic transfer_to_agent tool when SubAgents exist).
    /// Default: true
    /// </summary>
    public bool EnableAutoFlow { get; init; } = true;

    /// <summary>
    /// Callbacks for agent lifecycle hooks
    /// </summary>
    public IAgentCallbacks? Callbacks { get; init; }

    /// <summary>
    /// Dynamic tool providers (executed at runtime)
    /// </summary>
    public IReadOnlyList<IToolProvider>? ToolProviders { get; init; }

    /// <summary>
    /// Request processors (applied before LLM call)
    /// </summary>
    public IReadOnlyList<IRequestProcessor>? RequestProcessors { get; init; }

    /// <summary>
    /// Model name/identifier
    /// </summary>
    public string Model { get; init; }

    /// <summary>
    /// Input schema for structured input (optional)
    /// </summary>
    public object? InputSchema { get; init; }

    /// <summary>
    /// Output schema for structured output (optional)
    /// </summary>
    public object? OutputSchema { get; init; }

    public LlmAgent(ILlm llm, string model)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        Model = model;
        Name = "llm_agent"; // Default, should be overridden
    }

    /// <summary>
    /// Get effective tools including AutoFlow transfer tool and dynamic providers
    /// </summary>
    private IReadOnlyList<ITool> GetEffectiveTools(IInvocationContext context)
    {
        var tools = new List<ITool>();

        // Add user-provided tools
        if (Tools != null)
        {
            tools.AddRange(Tools);
        }

        // Auto-add transfer_to_agent tool if AutoFlow is enabled and sub-agents exist
        if (EnableAutoFlow && SubAgents.Count > 0)
        {
            // Check if transfer tool not already added
            if (!tools.Any(t => t.Name == "transfer_to_agent"))
            {
                tools.Add(BuiltInTools.CreateTransferToAgentTool());
            }
        }

        // Execute dynamic tool providers
        if (ToolProviders != null)
        {
            foreach (var provider in ToolProviders)
            {
                tools.AddRange(provider.GetTools(context));
            }
        }

        return tools;
    }

    protected override async IAsyncEnumerable<IEvent> RunAsyncImpl(
        IInvocationContext context,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        // 1. Build instruction with state template substitution
        var processedInstruction = ProcessInstructionTemplate(Instruction, context);

        // 2. Build message history
        var contents = BuildContents(context);

        // 3. Build tool declarations (including AutoFlow transfer tool if enabled)
        var effectiveTools = GetEffectiveTools(context);
        var toolDeclarations = effectiveTools.Count > 0
            ? effectiveTools.Select(t => t.GetDeclaration()).OfType<IFunctionDeclaration>().ToList()
            : null;

        // 4. Create request
        ILlmRequest request = new LlmRequestImpl
        {
            SystemInstruction = processedInstruction,
            Contents = contents,
            Tools = toolDeclarations,
            ToolChoice = toolDeclarations != null && toolDeclarations.Count > 0 ? "auto" : null
        };

        // Apply request processors if configured
        if (RequestProcessors != null)
        {
            var sortedProcessors = RequestProcessors.OrderBy(p => p.Priority);
            foreach (var processor in sortedProcessors)
            {
                request = await processor.ProcessAsync(request, context);
            }
        }

        // 5. Call LLM (streaming or non-streaming based on RunConfig.StreamingMode)
        // Multi-turn loop: continue calling LLM until no more tool calls
        // Limit enforced by RunConfig.MaxLlmCalls (default: 500, matches Python ADK)
        var continueProcessing = true;
        var streamingMode = context.RunConfig?.StreamingMode ?? StreamingMode.None;

        while (continueProcessing)
        {
            // Increment and enforce LLM call limit (throws if exceeded)
            context.IncrementAndEnforceLlmCallsLimit();

            if (streamingMode == StreamingMode.Sse)
            {
                // Streaming mode: yield text chunks in real-time
                var finalText = new System.Text.StringBuilder();
                var hasFunctionCalls = false;
                var functionCalls = new List<IFunctionCall>();
                IContent? assistantContent = null;

                await foreach (var response in _llm.GenerateStreamAsync(request, cancellationToken))
                {
                    // 6. Process streaming response

                    // Check if response has function calls (tools)
                    if (response.FunctionCalls?.Count > 0)
                    {
                        hasFunctionCalls = true;

                        // Collect function calls
                        foreach (var fc in response.FunctionCalls)
                        {
                            if (!functionCalls.Any(existing => existing.Id == fc.Id))
                            {
                                functionCalls.Add(fc);
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(response.Text))
                    {
                        // Stream text chunks in real-time
                        finalText.Append(response.Text);

                        // Yield streaming event immediately with Partial=true
                        var streamEvent = new Event
                        {
                            Author = Name,
                            Content = Boundary.Events.Content.FromText(response.Text, "model"),
                            Partial = true  // Mark as partial streaming chunk
                        };

                        yield return CreateEvent(streamEvent);
                    }

                    // Capture assistant content for history
                    if (response.Content != null)
                    {
                        assistantContent = response.Content;
                    }
                }

                // 7. Process function calls after streaming completes
                if (hasFunctionCalls)
                {
                    // Execute tools
                    foreach (var functionCall in functionCalls)
                    {
                        yield return CreateFunctionCallEvent(functionCall);

                        // Execute tool
                        var tool = effectiveTools.FirstOrDefault(t => t.Name == functionCall.Name);
                        if (tool != null)
                        {
                            var (toolResult, toolActions) = await ExecuteToolAsync(tool, functionCall, context, cancellationToken);

                            // Create event with actions from tool
                            var responseEvent = CreateFunctionResponseEvent(functionCall.Name, toolResult, toolActions);

                            yield return responseEvent;

                            // If transfer_to_agent was called, stop processing and let parent handle transfer
                            if (!string.IsNullOrEmpty(toolActions.TransferToAgent))
                            {
                                yield break; // Stop current agent execution
                            }
                        }
                    }

                    // Rebuild contents from session events for next turn
                    contents = BuildContentsFromEvents(context);
                    request = new LlmRequestImpl
                    {
                        SystemInstruction = processedInstruction,
                        Contents = contents,
                        Tools = toolDeclarations,
                        ToolChoice = toolDeclarations != null && toolDeclarations.Count > 0 ? "auto" : null
                    };

                    // Continue to next turn
                    continueProcessing = true;
                }
                else
                {
                    // No more tool calls - final text response
                    var finalTextStr = finalText.ToString();
                    if (OutputKey != null && !string.IsNullOrEmpty(finalTextStr))
                    {
                        context.Session.State.Set(OutputKey, finalTextStr);
                    }

                    // Stop processing
                    continueProcessing = false;
                }
            }
            else
            {
                // Non-streaming mode: single response
                var response = await _llm.GenerateAsync(request, cancellationToken);

                // 6. Process response
                string? finalText = null;

                // Check if response has function calls (tools)
                if (response.FunctionCalls?.Count > 0)
                {
                    // Execute tools
                    foreach (var functionCall in response.FunctionCalls)
                    {
                        yield return CreateFunctionCallEvent(functionCall);

                        // Execute tool
                        var tool = effectiveTools.FirstOrDefault(t => t.Name == functionCall.Name);
                        if (tool != null)
                        {
                            var (toolResult, toolActions) = await ExecuteToolAsync(tool, functionCall, context, cancellationToken);

                            // Create event with actions from tool
                            var responseEvent = CreateFunctionResponseEvent(functionCall.Name, toolResult, toolActions);

                            yield return responseEvent;

                            // If transfer_to_agent was called, stop processing and let parent handle transfer
                            if (!string.IsNullOrEmpty(toolActions.TransferToAgent))
                            {
                                yield break; // Stop current agent execution
                            }
                        }
                    }

                    // Rebuild contents from session events for next turn
                    contents = BuildContentsFromEvents(context);
                    request = new LlmRequestImpl
                    {
                        SystemInstruction = processedInstruction,
                        Contents = contents,
                        Tools = toolDeclarations,
                        ToolChoice = toolDeclarations != null && toolDeclarations.Count > 0 ? "auto" : null
                    };

                    // Continue to next turn
                    continueProcessing = true;
                }
                else
                {
                    // Regular text response - final turn
                    finalText = response.Text;

                    // 7. Save to output_key if specified
                    if (OutputKey != null && finalText != null)
                    {
                        context.Session.State.Set(OutputKey, finalText);
                    }

                    // 8. Yield final event
                    var evt = new Event
                    {
                        Author = Name,
                        Content = response.Content != null
                            ? ConvertToDto(response.Content)
                            : Boundary.Events.Content.FromText(finalText ?? "", "model")
                    };

                    yield return CreateEvent(evt);

                    // Stop processing
                    continueProcessing = false;
                }
            }
        }
    }

    /// <summary>
    /// Process instruction template, replacing {var} with state values.
    /// Python equivalent: Instruction templating with {var} and {artifact.var}
    /// </summary>
    private string? ProcessInstructionTemplate(string? instruction, IInvocationContext context)
    {
        if (string.IsNullOrEmpty(instruction))
            return instruction;

        // Replace {var} with state["var"]
        // Replace {var?} with state["var"] or empty if not exists (optional)
        var result = Regex.Replace(instruction, @"\{(\w+)(\?)?\}", match =>
        {
            var varName = match.Groups[1].Value;
            var isOptional = match.Groups[2].Success;

            if (context.Session.State.TryGetValue<string>(varName, out var value))
            {
                return value ?? "";
            }

            if (isOptional)
                return "";

            throw new InvalidOperationException(
                $"State variable '{varName}' not found in instruction template. " +
                $"Use {{var?}} for optional variables.");
        });

        // Note: {artifact.var} syntax for artifact references not yet implemented
        // Artifact service integration pending

        return result;
    }

    private List<IContent> BuildContents(IInvocationContext context)
    {
        var contents = new List<IContent>();

        // Build from session events (full conversation history)
        // Python ADK approach: include_contents='default'
        foreach (var evt in context.Session.Events)
        {
            // Skip events without content
            if (evt.Content == null || evt.Content.Parts == null || evt.Content.Parts.Count == 0)
                continue;

            // Include user/model content
            var role = evt.Content.Role;
            if (role == "user" || role == "model")
            {
                contents.Add(evt.Content);
            }
        }

        // Add current user input if not already in events
        if (context.UserInput != null)
        {
            // Check if last event is current user input
            var needToAdd = true;
            if (contents.Count > 0)
            {
                var lastContent = contents[contents.Count - 1];
                if (lastContent.Role == "user" &&
                    lastContent.Parts.Count > 0 &&
                    lastContent.Parts[0].Text == context.UserInput)
                {
                    needToAdd = false;
                }
            }

            if (needToAdd)
            {
                contents.Add(new SimpleContent
                {
                    Role = "user",
                    Parts = [new SimplePart { Text = context.UserInput }]
                });
            }
        }

        return contents;
    }

    /// <summary>
    /// Build contents from session events (for multi-turn tool execution)
    /// </summary>
    private List<IContent> BuildContentsFromEvents(IInvocationContext context)
    {
        var contents = new List<IContent>();

        // Add initial user input
        if (context.UserInput != null)
        {
            contents.Add(new SimpleContent
            {
                Role = "user",
                Parts = [new SimplePart { Text = context.UserInput }]
            });
        }

        // Add all events from session (includes assistant responses and tool results)
        foreach (var evt in context.Session.Events)
        {
            if (evt.Content != null)
            {
                contents.Add(evt.Content);
            }
        }

        return contents;
    }

    private async Task<(object result, IToolActions actions)> ExecuteToolAsync(
        ITool tool,
        IFunctionCall functionCall,
        IInvocationContext context,
        CancellationToken cancellationToken)
    {
        var toolActions = new ToolActionsImpl();
        var toolContext = new ToolContextImpl
        {
            Session = context.Session,
            State = context.Session.State,
            User = null, // User context not available in current IInvocationContext
            Actions = toolActions
        };

        var args = functionCall.Args ?? new Dictionary<string, object>();

        try
        {
            var result = await tool.ExecuteAsync(args, toolContext, cancellationToken);
            return (result, toolActions);
        }
        catch (Exception ex)
        {
            return (new { error = ex.Message }, toolActions);
        }
    }

    private IEvent CreateFunctionCallEvent(IFunctionCall functionCall)
    {
        var evt = new Event
        {
            Author = Name,
            Content = new Boundary.Events.Content
            {
                Role = "model",
                Parts =
                [
                    new Part
                    {
                        FunctionCall = new Boundary.Events.FunctionCall
                        {
                            Name = functionCall.Name,
                            Args = functionCall.Args as Dictionary<string, object>
                        }
                    }
                ]
            }
        };

        return CreateEvent(evt);
    }

    private IEvent CreateFunctionResponseEvent(string functionName, object result, IToolActions? toolActions = null)
    {
        var evt = new Event
        {
            Author = "tool",
            Content = new Boundary.Events.Content
            {
                Role = "tool",
                Parts =
                [
                    new Part
                    {
                        FunctionResponse = new Boundary.Events.FunctionResponse
                        {
                            Name = functionName,
                            Response = result
                        }
                    }
                ]
            },
            // Add actions if tool set any
            Actions = (toolActions != null && (!string.IsNullOrEmpty(toolActions.TransferToAgent) || toolActions.Escalate))
                ? new Boundary.Events.EventActions
                {
                    TransferTo = toolActions.TransferToAgent,
                    Escalate = toolActions.Escalate
                }
                : null
        };

        return CreateEvent(evt);
    }

    private Boundary.Events.Content ConvertToDto(IContent content)
    {
        var parts = content.Parts.Select(p => new Part
        {
            Text = p.Text,
            FunctionCall = p.FunctionCall != null
                ? new Boundary.Events.FunctionCall
                {
                    Name = p.FunctionCall.Name,
                    Args = p.FunctionCall.Args != null
                        ? new Dictionary<string, object>(p.FunctionCall.Args)
                        : null,
                    Id = p.FunctionCall.Id
                }
                : null,
            FunctionResponse = p.FunctionResponse != null
                ? new Boundary.Events.FunctionResponse
                {
                    Name = p.FunctionResponse.Name,
                    Response = p.FunctionResponse.Response,
                    Id = p.FunctionResponse.Id
                }
                : null
        }).ToList();

        return new Boundary.Events.Content
        {
            Role = content.Role,
            Parts = parts
        };
    }
}

// Internal implementation classes
internal class LlmRequestImpl : ILlmRequest
{
    public required string? SystemInstruction { get; init; }
    public required List<IContent> Contents { get; init; }
    IReadOnlyList<IContent> ILlmRequest.Contents => Contents;
    public IReadOnlyList<IFunctionDeclaration>? Tools { get; init; }
    public string? ToolChoice { get; init; }
    public IGenerationConfig? Config { get; init; }
}

internal class SimpleContent : IContent
{
    public required string? Role { get; init; }
    public required List<IPart> Parts { get; init; }
    IReadOnlyList<IPart> IContent.Parts => Parts;
}

internal class SimplePart : IPart
{
    public string? Text { get; init; }
    public IFunctionCall? FunctionCall { get; init; }
    public IFunctionResponse? FunctionResponse { get; init; }
    public byte[]? InlineData { get; init; }
    public string? MimeType { get; init; }
}

internal class ToolContextImpl : IToolContext
{
    public required ISession Session { get; init; }
    public required ISessionState State { get; init; }
    public string? User { get; init; }
    public IReadOnlyDictionary<string, object>? Metadata { get; init; }
    public required IToolActions Actions { get; init; }
}

internal class ToolActionsImpl : IToolActions
{
    public string? TransferToAgent { get; set; }
    public bool Escalate { get; set; }
    public bool SkipSummarization { get; set; }
}
