// Copyright 2025 NTG
// Licensed under the Apache License, Version 2.0

using NTG.Adk.Boundary.Events;
using NTG.Adk.CoreAbstractions.Events;

namespace NTG.Adk.Implementations.Events;

/// <summary>
/// Adapter wrapping Boundary.Event to implement IEvent port.
/// Follows A.D.D V3: Implementations depend only on CoreAbstractions.
/// </summary>
public class EventAdapter : IEvent
{
    private readonly Event _event;

    public EventAdapter(Event @event)
    {
        _event = @event ?? throw new ArgumentNullException(nameof(@event));
    }

    public string Author => _event.Author;
    public IContent? Content => _event.Content != null ? new ContentAdapter(_event.Content) : null;
    public IEventActions? Actions => _event.Actions != null ? new EventActionsAdapter(_event.Actions) : null;
    public IReadOnlyDictionary<string, object>? Metadata => _event.Metadata;
    public DateTimeOffset Timestamp => _event.Timestamp;
    public bool Partial => _event.Partial;

    /// <summary>
    /// Get the underlying boundary DTO
    /// </summary>
    public Event ToDto() => _event;

    /// <summary>
    /// Create from boundary DTO
    /// </summary>
    public static EventAdapter FromDto(Event dto) => new(dto);

    /// <summary>
    /// Create a text event
    /// </summary>
    public static EventAdapter FromText(string author, string text) => new(new Event
    {
        Author = author,
        Content = Boundary.Events.Content.FromText(text)
    });
}

/// <summary>
/// Content adapter
/// </summary>
internal class ContentAdapter : IContent
{
    private readonly Boundary.Events.Content _content;

    public ContentAdapter(Boundary.Events.Content content)
    {
        _content = content;
    }

    public string? Role => _content.Role;
    public IReadOnlyList<IPart> Parts => _content.Parts.Select(p => new PartAdapter(p)).ToList();
}

/// <summary>
/// Part adapter
/// </summary>
internal class PartAdapter : IPart
{
    private readonly Part _part;

    public PartAdapter(Part part)
    {
        _part = part;
    }

    public string? Text => _part.Text;
    public IFunctionCall? FunctionCall => _part.FunctionCall != null ? new FunctionCallAdapter(_part.FunctionCall) : null;
    public IFunctionResponse? FunctionResponse => _part.FunctionResponse != null ? new FunctionResponseAdapter(_part.FunctionResponse) : null;
    public byte[]? InlineData => _part.InlineData;
    public string? MimeType => _part.MimeType;
}

/// <summary>
/// Function call adapter
/// </summary>
internal class FunctionCallAdapter : IFunctionCall
{
    private readonly Boundary.Events.FunctionCall _functionCall;

    public FunctionCallAdapter(Boundary.Events.FunctionCall functionCall)
    {
        _functionCall = functionCall;
    }

    public string Name => _functionCall.Name;
    public IReadOnlyDictionary<string, object>? Args => _functionCall.Args;
    public string? Id => _functionCall.Id;
}

/// <summary>
/// Function response adapter
/// </summary>
internal class FunctionResponseAdapter : IFunctionResponse
{
    private readonly Boundary.Events.FunctionResponse _functionResponse;

    public FunctionResponseAdapter(Boundary.Events.FunctionResponse functionResponse)
    {
        _functionResponse = functionResponse;
    }

    public string Name => _functionResponse.Name;
    public object Response => _functionResponse.Response;
    public string? Id => _functionResponse.Id;
    public string? Error => _functionResponse.Error;
}

/// <summary>
/// Event actions adapter
/// </summary>
internal class EventActionsAdapter : IEventActions
{
    private readonly EventActions _actions;

    public EventActionsAdapter(EventActions actions)
    {
        _actions = actions;
    }

    public bool Escalate => _actions.Escalate;
    public string? TransferTo => _actions.TransferTo;
    public IReadOnlyDictionary<string, object>? StateDelta => _actions.StateDelta;
    public IReadOnlyDictionary<string, object>? CustomActions => _actions.CustomActions;
}
