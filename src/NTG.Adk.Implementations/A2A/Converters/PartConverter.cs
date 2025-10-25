// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using System.Text.Json;
using NTG.Adk.Boundary.Events;

namespace NTG.Adk.Implementations.A2A.Converters;

/// <summary>
/// Bidirectional converter between A2A Part and ADK Part.
/// Based on Google ADK Python implementation.
/// </summary>
public static class PartConverter
{
    // Metadata keys for DataPart conversions
    public const string DataPartMetadataTypeKey = "type";
    public const string DataPartMetadataIsLongRunningKey = "is_long_running";
    public const string DataPartMetadataTypeFunctionCall = "function_call";
    public const string DataPartMetadataTypeFunctionResponse = "function_response";
    public const string DataPartMetadataTypeCodeExecutionResult = "code_execution_result";
    public const string DataPartMetadataTypeExecutableCode = "executable_code";

    /// <summary>
    /// Converts an A2A Part to an ADK Part.
    /// Returns null if the part type is unsupported.
    /// </summary>
    public static Part? ConvertA2APartToAdkPart(global::A2A.Part a2aPart)
    {
        // TextPart → ADK Text
        if (a2aPart is global::A2A.TextPart textPart)
        {
            return Part.FromText(textPart.Text ?? string.Empty);
        }

        // FilePart → ADK InlineData
        if (a2aPart is global::A2A.FilePart filePart)
        {
            // URI-based file
            if (filePart.File?.Uri != null)
            {
                // ADK doesn't have FileUri yet
                return null;
            }

            // Bytes-based file
            if (filePart.File?.Bytes != null && filePart.File.MimeType != null)
            {
                var bytes = Convert.FromBase64String(filePart.File.Bytes);
                return Part.FromBytes(bytes, filePart.File.MimeType);
            }

            return null;
        }

        // DataPart → Function calls/responses based on metadata
        if (a2aPart is global::A2A.DataPart dataPart)
        {
            var metadata = dataPart.Metadata;
            if (metadata != null)
            {
                var typeKey = MetadataUtilities.GetAdkMetadataKey(DataPartMetadataTypeKey);
                if (metadata.TryGetValue(typeKey, out var typeValue))
                {
                    var typeStr = typeValue.ToString();

                    // Convert JsonElement dictionary to JSON string for deserialization
                    var dataJson = JsonSerializer.Serialize(dataPart.Data);

                    // Function call
                    if (typeStr == DataPartMetadataTypeFunctionCall)
                    {
                        var functionCall = JsonSerializer.Deserialize<FunctionCall>(dataJson);
                        return functionCall != null ? Part.FromFunctionCall(functionCall) : null;
                    }

                    // Function response
                    if (typeStr == DataPartMetadataTypeFunctionResponse)
                    {
                        var functionResponse = JsonSerializer.Deserialize<FunctionResponse>(dataJson);
                        return functionResponse != null ? Part.FromFunctionResponse(functionResponse) : null;
                    }

                    // Code execution result - convert to text (ADK doesn't have this type yet)
                    if (typeStr == DataPartMetadataTypeCodeExecutionResult)
                    {
                        return Part.FromText(dataJson);
                    }

                    // Executable code - convert to text (ADK doesn't have this type yet)
                    if (typeStr == DataPartMetadataTypeExecutableCode)
                    {
                        return Part.FromText(dataJson);
                    }
                }
            }

            // Generic DataPart without special metadata - convert to text
            return Part.FromText(JsonSerializer.Serialize(dataPart.Data));
        }

        return null;
    }

    /// <summary>
    /// Converts an ADK Part to an A2A Part.
    /// Returns null if the part type is unsupported.
    /// </summary>
    public static global::A2A.Part? ConvertAdkPartToA2APart(Part adkPart)
    {
        // Text → TextPart
        if (adkPart.Text != null)
        {
            return new global::A2A.TextPart { Text = adkPart.Text };
        }

        // InlineData → FilePart with bytes
        if (adkPart.InlineData != null && adkPart.MimeType != null)
        {
            var base64String = Convert.ToBase64String(adkPart.InlineData);
            return new global::A2A.FilePart
            {
                File = new global::A2A.FileContent(base64String)
                {
                    MimeType = adkPart.MimeType
                }
            };
        }

        // FunctionCall → DataPart with metadata
        if (adkPart.FunctionCall != null)
        {
            var jsonString = JsonSerializer.Serialize(adkPart.FunctionCall);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);

            // Convert metadata dictionary to JsonElement dictionary
            var metadata = new Dictionary<string, JsonElement>
            {
                [MetadataUtilities.GetAdkMetadataKey(DataPartMetadataTypeKey)] =
                    JsonSerializer.SerializeToElement(DataPartMetadataTypeFunctionCall)
            };

            return new global::A2A.DataPart
            {
                Data = data!,
                Metadata = metadata
            };
        }

        // FunctionResponse → DataPart with metadata
        if (adkPart.FunctionResponse != null)
        {
            var jsonString = JsonSerializer.Serialize(adkPart.FunctionResponse);
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonString);

            // Convert metadata dictionary to JsonElement dictionary
            var metadata = new Dictionary<string, JsonElement>
            {
                [MetadataUtilities.GetAdkMetadataKey(DataPartMetadataTypeKey)] =
                    JsonSerializer.SerializeToElement(DataPartMetadataTypeFunctionResponse)
            };

            return new global::A2A.DataPart
            {
                Data = data!,
                Metadata = metadata
            };
        }

        return null;
    }

    /// <summary>
    /// Converts a list of A2A Parts to ADK Parts.
    /// Filters out null results from unsupported conversions.
    /// </summary>
    public static List<Part> ConvertA2APartsToAdkParts(IEnumerable<global::A2A.Part> a2aParts)
    {
        return a2aParts
            .Select(ConvertA2APartToAdkPart)
            .Where(p => p != null)
            .Cast<Part>()
            .ToList();
    }

    /// <summary>
    /// Converts a list of ADK Parts to A2A Parts.
    /// Filters out null results from unsupported conversions.
    /// </summary>
    public static List<global::A2A.Part> ConvertAdkPartsToA2AParts(IEnumerable<Part> adkParts)
    {
        return adkParts
            .Select(ConvertAdkPartToA2APart)
            .Where(p => p != null)
            .Cast<global::A2A.Part>()
            .ToList();
    }
}
