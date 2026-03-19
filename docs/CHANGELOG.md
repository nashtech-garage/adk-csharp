# Changelog - NTG.Adk

All notable changes to this project will be documented in this file.

## [1.8.17] - 2026-03-19

### 🐛 **BUG FIX: SSE Stream Silently Aborted by Provider Socket Timeout**

In SSE streaming mode, some LLM backends (including OpenAI-compatible endpoints) close the TCP connection after sending the last token without sending the `[DONE]` signal. The `ReadTimeoutStream` inside the OpenAI SDK would then throw `OperationCanceledException` (wrapping `SocketException 995`) while waiting for more data. This exception propagated through `LlmAgent.RunAsyncImpl`, aborting execution before the final `Partial=false` consolidation event could be yielded — so neither `partial=false` nor the operator-level `done` event ever reached the client.

#### Changes

- **LlmAgent.cs (Operators)**
  - Replaced `await foreach` over `GenerateStreamAsync` with a manual `IAsyncEnumerator` loop.
  - `MoveNextAsync()` is called inside an inner `try/catch` that catches `OperationCanceledException when (!cancellationToken.IsCancellationRequested)` — distinguishing a provider-side socket abort from a genuine user cancellation.
  - On provider abort: `break` out of the loop; accumulated `finalText` is intact, execution continues normally to emit `Partial=false` and signal completion.
  - On user cancellation (`cancellationToken.IsCancellationRequested`): exception re-throws as before.
  - `IAsyncEnumerator.DisposeAsync()` is always called in a `finally` block.

#### Breaking Changes

- **NONE** — 100% backward compatible. Behavior is identical for providers that send `[DONE]`; only providers that close the socket early are affected.

---

## [1.8.16] - 2026-03-16

### 🐛 **BUG FIX: Compaction event ignored by BuildContents**

`LlmEventSummarizer` stores the summary in `Actions.Compaction.CompactedContent` but `BuildContents` only reads `evt.Content`, so compaction events were silently skipped — ADK session grew unbounded and eventually hit context overflow.

#### Changes

- **LlmAgent.cs (Operators)**
  - `BuildContents`: when a compaction event is encountered (`Actions.Compaction != null`), clear accumulated contents and seed from `CompactedContent`.
  - Events after the compaction point continue appending normally.

---

## [1.8.15] - 2026-03-14

### ✨ **FEATURE: Streaming `<think>` Block Parsing**

For OpenAI-compatible models that embed reasoning in `<think>...</think>` blocks within text, the streaming pipeline now automatically routes thinking content into `part.Reasoning` and delivers clean answer text in `part.Text` — no manual parsing required in application code.

#### Changes

- **OpenAILlm.cs (Implementations)**
  - Added `ThinkParseState` to track parse state across streaming chunks.
  - Added `ProcessTextChunk`: routes text outside `<think>` tags to `SimplePart { Text }`, content inside to `SimplePart { Reasoning }`.
  - Correctly handles blocks split across multiple stream chunks.
  - Unclosed block at stream end is flushed as a Reasoning part.
  - Native reasoning (o1/o3 via `GetReasoningContent`) is unaffected.

---

## [1.8.14] - 2026-03-11

### 🐛 **BUG FIX: Multimodal Image Handling in OpenAILlm**

Fix two defects in inline image processing for OpenAI API calls.

#### Bug Fixes

- **OpenAILlm.cs (Implementations)**
  - Fixed message filter to retain content parts containing only inline images (`InlineData`). Previously, image-only parts were incorrectly skipped due to missing `InlineData` null-check.
  - Fixed image encoding: switched from base64 data URI (`new Uri(...)`) to `BinaryData.FromBytes()` overload. `Uri` constructor normalizes/corrupts base64 strings, causing API rejection.

---

## [1.8.12] - 2026-03-01

### 🐛 **BUG FIX: Tool Call Accumulation & Event Processing**

Fix critical defects in tool call argument streaming and event part handling.

#### Bug Fixes

- **OpenAILlm.cs (Implementations)**
  - Fixed streaming tool call argument accumulation by tracking part indices.
  - Prevents argument corruption or loss during high-concurrency streaming sessions.

- **LlmAgent.cs (Operators)**
  - Added null-check guard when processing content parts.
  - Skips empty/null parts that could trigger downstream NullReferenceExceptions in complex orchestration.

- **Runner.cs (Bootstrap)**
  - Improved event persistence logic to handle parallel streaming branches more robustly.

---

## [1.8.11] - 2026-02-22

### 🐛 **BUG FIX: Persistent Session Context in Default Runner**

Fix a bug where session context and memory were lost between agentic interactions when using the Bootstrap `Runner`.

#### Bug Fix

- **Runner.cs (Bootstrap)**
  - Implemented `ConcurrentDictionary` to cache and reuse `InMemorySession` instances by `sessionId`.
  - Ensures correct conversational memory retention and agentic orchestration continuity across multiple iterative requests without explicitly injecting states from outside.

## [1.8.10] - 2026-02-21

### 🐛 **BUG FIX: Infinite Request Loops**

Fix a critical defect where unrecorded tool events triggered infinite recursive API calls when using custom orchestration.

#### Bug Fix

- **Runner.RunAsync() & RunStreamAsync() - Append non-partial events** - Prevented endless looping on tool resolutions
  - Added condition `if (!evt.Partial) session.Events.Add(evt);` into main streaming loops 
  - Prevents the agent from endlessly re-calling the same tool when utilizing the basic `Runner` wrappers
  - Addresses issue where `Bootstrap.Runner` neglected capturing tool payloads leading to repeated LLM hallucination of previously issued directives.

- **Enable Default SSE in Bootstrap Runner** - Simplified real-time consumption
  - Set default fallback structure for `RunConfig` as `StreamingMode.Sse` if absent, allowing real-time behavior without explicit user intervention.

#### Files Modified

- `src/NTG.Adk.Bootstrap/Runner.cs`

#### Breaking Changes

- **NONE** - 100% backward compatible
  - Fixes erroneous behavior, API usage and implementation signatures untouched.


## [1.8.10] - 2026-02-10

### 🔄 **PYTHON ADK SYNC**

Ported critical fixes and improvements from `adk-python` (up to commit `663cb75`).

#### Function Call & Invocation Tracking

- **Fixed Function Call ID Preserving** - Critical for modern LLMs
  - `LlmAgent` now preserves `FunctionCall.Id` and `FunctionResponse.Id` in events.
  - Ensures correct mapping between tool calls and results, preventing LLM errors.
  - Fixes `adk-python` issue #4381 (commit `663cb75`).

- **Invocation ID Tracking**
  - Added `InvocationId` to `IInvocationContext` and `Runner`.
  - Ensures accurate request tracing and event correlation across agents.
  - Aligns with `adk-python` `d2dba27`.

#### Files Modified

- `src/NTG.Adk.CoreAbstractions/Sessions/IInvocationContext.cs` - Added `InvocationId`
- `src/NTG.Adk.Implementations/Sessions/InvocationContext.cs` - Implemented `InvocationId`
- `src/NTG.Adk.Operators/Runners/Runner.cs` - Added `invocationId` parameter
- `src/NTG.Adk.Operators/Agents/LlmAgent.cs` - Updated event creation logic

#### Breaking Changes

- **NONE** - Backward compatible. `invocationId` parameter in `Runner` is optional.


## [1.8.10] - 2026-02-21

### 🐛 **BUG FIX: Tool Results Included in Message History**

Fix critical bug where tool results were excluded from conversation history, causing HTTP 400 errors from Claude API.

#### Bug Fix

- **LlmAgent.BuildContents() - Include tool role** - Fixed message history to include tool results
  - Changed condition from `if (role == "user" || role == "model")` to include `|| role == "tool"`
  - Tool results created with `role = "tool"` were previously excluded from BuildContents()
  - This caused requests with tool_calls to be sent without matching tool_results
  - Claude API rejected with error: "tool_use ids were found without tool_result blocks immediately after"
  - Now properly includes all user, model, and tool messages in conversation history

#### Files Modified

- `src/NTG.Adk.Operators/Agents/LlmAgent.cs` (line 415) - Added `|| role == "tool"` to BuildContents() condition

#### Breaking Changes

- **NONE** - 100% backward compatible
  - Fixes broken behavior, no API changes


## [1.8.7] - 2025-12-07

### 🧠 **INTERLEAVED THINKING SUPPORT**

Stream reasoning content during multi-turn tool execution (interleaved thinking).

#### Streaming Reasoning in Agentic Loop

- **LlmAgent Reasoning Streaming** - Yield reasoning chunks during tool execution
  - Check `response.Content.Parts` for reasoning content in streaming loop
  - Yield reasoning events with `Partial = true` for real-time display
  - Works with any model that returns reasoning tokens (DeepSeek R1, OpenAI o1/o3, Claude with extended thinking)
  - Backward compatible: no-op if model does not return reasoning

- **Helper Methods** - Convenience factories for reasoning content
  - Added `Part.FromReasoning(string)` factory method
  - Added `Content.FromReasoning(string, string?)` factory method
  - Consistent API with existing `FromText()` pattern

#### Files Modified

- `src/NTG.Adk.Operators/Agents/LlmAgent.cs` - Added reasoning check in streaming loop
- `src/NTG.Adk.Boundary/Events/Part.cs` - Added FromReasoning() factory
- `src/NTG.Adk.Boundary/Events/Content.cs` - Added FromReasoning() factory

#### Breaking Changes

- **NONE** - 100% backward compatible
  - Reasoning streaming is automatic if model provides it
  - No API changes required for existing code


## [1.8.6] - 2025-12-05

### 🧠 **REASONING CONTENT SUPPORT**

Add support for chain-of-thought reasoning content from advanced LLM models (DeepSeek R1, OpenAI o1/o3).

#### Reasoning Features

- **IPart.Reasoning** - New property for model reasoning/thinking output
  - Added `Reasoning` property to IPart interface (CoreAbstractions)
  - Added `Reasoning` property to Part record (Boundary)
  - Enables separate streaming of reasoning vs answer content
  - Compatible with DeepSeek R1, OpenAI o1/o3, and future reasoning models

- **OpenAILlm Reasoning Extraction** - Runtime detection of reasoning content
  - Added `GetReasoningContent()` method with reflection-based detection
  - Checks for SDK `Reasoning` property or `Kind` property indicating reasoning
  - Future-proof for when OpenAI SDK adds native reasoning support

#### Bug Fixes

- **Fixed .gitignore blocking Artifacts folder** - Critical build fix
  - Changed `/artifacts/` rule from `artifacts/` to only match root folder
  - Previously blocked `src/NTG.Adk.CoreAbstractions/Artifacts/` from being tracked
  - Fixes "Artifacts namespace not found" build errors

#### Files Modified

- `src/NTG.Adk.CoreAbstractions/Events/IEvent.cs` - Added Reasoning to IPart
- `src/NTG.Adk.Boundary/Events/Part.cs` - Added Reasoning property
- `src/NTG.Adk.Implementations/Events/EventAdapter.cs` - Added Reasoning to PartAdapter
- `src/NTG.Adk.Implementations/Models/OpenAILlm.cs` - Added reasoning extraction
- `src/NTG.Adk.Implementations/Models/GeminiLlm.cs` - Added Reasoning to SimplePart
- `src/NTG.Adk.Implementations/Tools/Retrieval/AgenticFilesRetrievalTool.cs` - Added Reasoning to SimplePart
- `src/NTG.Adk.Operators/Internal/LlmRequestTypes.cs` - Added Reasoning to SimplePart
- `.gitignore` - Fixed artifacts/ rule

#### Breaking Changes

- **NONE** - 100% backward compatible
  - Reasoning property is nullable, existing code works unchanged



---
*For older version history, please see [archives/CHANGELOG_archive.md](archives/CHANGELOG_archive.md)*
