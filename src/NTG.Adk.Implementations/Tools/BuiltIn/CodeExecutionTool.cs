// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using NTG.Adk.Boundary.Tools;
using NTG.Adk.CoreAbstractions.Tools;

namespace NTG.Adk.Implementations.Tools.BuiltIn;

/// <summary>
/// Code execution tool for running code snippets safely.
/// Supports C# code execution via dotnet script.
/// Equivalent to google.adk.executors.BaseCodeExecutor in Python.
/// </summary>
public sealed class CodeExecutionTool : ITool
{
    private readonly string _workingDirectory;
    private readonly int _timeoutSeconds;

    public string Name => "execute_code";
    public string? Description => "Execute C# code and return the output. Use Console.WriteLine to print results.";

    /// <summary>
    /// Creates a new code execution tool.
    /// </summary>
    /// <param name="workingDirectory">Working directory for code execution</param>
    /// <param name="timeoutSeconds">Timeout in seconds (default 30)</param>
    public CodeExecutionTool(string? workingDirectory = null, int timeoutSeconds = 30)
    {
        _workingDirectory = workingDirectory ?? Path.GetTempPath();
        _timeoutSeconds = timeoutSeconds;
    }

    public IFunctionDeclaration GetDeclaration()
    {
        var schema = new Schema
        {
            Type = "object",
            Properties = new Dictionary<string, SchemaProperty>
            {
                ["code"] = new SchemaProperty
                {
                    Type = "string",
                    Description = "The C# code to execute. Use Console.WriteLine for output."
                },
                ["language"] = new SchemaProperty
                {
                    Type = "string",
                    Description = "Programming language (currently only 'csharp' supported)",
                    Enum = new List<string> { "csharp" }
                }
            },
            Required = new List<string> { "code" }
        };

        var declaration = new FunctionDeclaration
        {
            Name = Name,
            Description = Description,
            Parameters = schema
        };

        return new CodeExecutionFunctionDeclarationAdapter(declaration);
    }

    public async Task<object> ExecuteAsync(
        IReadOnlyDictionary<string, object> args,
        IToolContext context,
        CancellationToken cancellationToken = default)
    {
        // Extract code parameter
        if (!args.TryGetValue("code", out var codeObj) || codeObj == null)
        {
            return new { success = false, error = "Missing required parameter: code" };
        }

        var code = codeObj.ToString()!;

        // Extract language parameter (default csharp)
        var language = "csharp";
        if (args.TryGetValue("language", out var langObj) && langObj != null)
        {
            language = langObj.ToString()!.ToLowerInvariant();
        }

        if (language != "csharp")
        {
            return new
            {
                success = false,
                error = $"Unsupported language: {language}. Only 'csharp' is currently supported."
            };
        }

        try
        {
            // Execute C# code using dotnet-script or inline compilation
            var result = await ExecuteCSharpCodeAsync(code, cancellationToken);
            return result;
        }
        catch (Exception ex)
        {
            return new
            {
                success = false,
                error = $"Execution failed: {ex.Message}",
                code = code
            };
        }
    }

    private async Task<object> ExecuteCSharpCodeAsync(string code, CancellationToken cancellationToken)
    {
        // Create temporary script file
        var tempFile = Path.Combine(_workingDirectory, $"script_{Guid.NewGuid()}.csx");

        try
        {
            // Write code to temp file
            await File.WriteAllTextAsync(tempFile, code, cancellationToken);

            // Try to execute using dotnet-script if available
            var (success, output, error) = await TryExecuteWithDotnetScript(tempFile, cancellationToken);

            if (success)
            {
                return new
                {
                    success = true,
                    output = output,
                    error = !string.IsNullOrEmpty(error) ? error : null
                };
            }

            // Fallback: compile and run as console app
            return await ExecuteAsConsoleApp(code, cancellationToken);
        }
        finally
        {
            // Clean up temp file
            try
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private async Task<(bool success, string output, string error)> TryExecuteWithDotnetScript(
        string scriptFile,
        CancellationToken cancellationToken)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"script \"{scriptFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = _workingDirectory
                }
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    errorBuilder.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(_timeoutSeconds), cancellationToken);
            var processTask = process.WaitForExitAsync(cancellationToken);

            var completedTask = await Task.WhenAny(processTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                try { process.Kill(); } catch { }
                return (false, "", "Execution timed out");
            }

            var output = outputBuilder.ToString().Trim();
            var error = errorBuilder.ToString().Trim();

            return (process.ExitCode == 0, output, error);
        }
        catch
        {
            return (false, "", "dotnet-script not available");
        }
    }

    private async Task<object> ExecuteAsConsoleApp(string code, CancellationToken cancellationToken)
    {
        // Simple fallback: create minimal console app and compile
        var projectDir = Path.Combine(_workingDirectory, $"exec_{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(projectDir);

            // Create minimal .csproj
            var csprojContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
  </PropertyGroup>
</Project>";

            await File.WriteAllTextAsync(
                Path.Combine(projectDir, "Program.csproj"),
                csprojContent,
                cancellationToken);

            // Wrap code in Program class if not already wrapped
            var wrappedCode = code.Contains("class Program") || code.Contains("static void Main")
                ? code
                : $@"using System;
using System.Linq;
using System.Collections.Generic;

{code}";

            await File.WriteAllTextAsync(
                Path.Combine(projectDir, "Program.cs"),
                wrappedCode,
                cancellationToken);

            // Compile and run
            var runProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "run --verbosity quiet",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = projectDir
                }
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            runProcess.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    outputBuilder.AppendLine(e.Data);
            };

            runProcess.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                    errorBuilder.AppendLine(e.Data);
            };

            runProcess.Start();
            runProcess.BeginOutputReadLine();
            runProcess.BeginErrorReadLine();

            await runProcess.WaitForExitAsync(cancellationToken);

            var output = outputBuilder.ToString().Trim();
            var error = errorBuilder.ToString().Trim();

            return new
            {
                success = runProcess.ExitCode == 0,
                output = output,
                error = !string.IsNullOrEmpty(error) ? error : null
            };
        }
        finally
        {
            // Clean up project directory
            try
            {
                if (Directory.Exists(projectDir))
                {
                    Directory.Delete(projectDir, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

// Adapter for FunctionDeclaration
internal sealed class CodeExecutionFunctionDeclarationAdapter : IFunctionDeclaration
{
    private readonly FunctionDeclaration _dto;

    public CodeExecutionFunctionDeclarationAdapter(FunctionDeclaration dto)
    {
        _dto = dto;
    }

    public string Name => _dto.Name;
    public string? Description => _dto.Description;
    public ISchema? Parameters => _dto.Parameters != null
        ? new CodeExecutionSchemaAdapter(_dto.Parameters)
        : null;
    public ISchema? Response => _dto.Response != null
        ? new CodeExecutionSchemaAdapter(_dto.Response)
        : null;
}

internal sealed class CodeExecutionSchemaAdapter : ISchema
{
    private readonly Schema _dto;

    public CodeExecutionSchemaAdapter(Schema dto)
    {
        _dto = dto;
    }

    public string Type => _dto.Type;

    public IReadOnlyDictionary<string, ISchemaProperty>? Properties =>
        _dto.Properties?.ToDictionary(
            kvp => kvp.Key,
            kvp => (ISchemaProperty)new CodeExecutionSchemaPropertyAdapter(kvp.Value));

    public IReadOnlyList<string>? Required => _dto.Required;
}

internal sealed class CodeExecutionSchemaPropertyAdapter : ISchemaProperty
{
    private readonly SchemaProperty _dto;

    public CodeExecutionSchemaPropertyAdapter(SchemaProperty dto)
    {
        _dto = dto;
    }

    public string Type => _dto.Type;
    public string? Description => _dto.Description;
    public IReadOnlyList<string>? Enum => _dto.Enum;
    public ISchemaProperty? Items => _dto.Items != null ? new CodeExecutionSchemaPropertyAdapter(_dto.Items) : null;

    public IReadOnlyDictionary<string, ISchemaProperty>? Properties =>
        _dto.Properties?.ToDictionary(
            kvp => kvp.Key,
            kvp => (ISchemaProperty)new CodeExecutionSchemaPropertyAdapter(kvp.Value));

    public IReadOnlyList<string>? Required => _dto.Required;
}
