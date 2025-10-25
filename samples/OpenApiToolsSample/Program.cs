// Copyright 2025 NTG
// Licensed under Apache License, Version 2.0

using NTG.Adk.Implementations.Models;
using NTG.Adk.Implementations.Tools.OpenApi;
using NTG.Adk.Operators.Agents;
using NTG.Adk.Operators.Runners;

Console.WriteLine("========================================");
Console.WriteLine("  NTG.Adk - OpenAPI Toolset Demo");
Console.WriteLine("========================================\n");

// Simple JSONPlaceholder OpenAPI spec
var openApiSpec = """
{
  "openapi": "3.0.0",
  "info": {
    "title": "JSONPlaceholder API",
    "version": "1.0.0"
  },
  "servers": [
    { "url": "https://jsonplaceholder.typicode.com" }
  ],
  "paths": {
    "/posts": {
      "get": {
        "operationId": "listPosts",
        "summary": "Get all posts",
        "responses": { "200": { "description": "Success" } }
      }
    },
    "/posts/{id}": {
      "get": {
        "operationId": "getPost",
        "summary": "Get post by ID",
        "parameters": [
          {
            "name": "id",
            "in": "path",
            "required": true,
            "schema": { "type": "integer" }
          }
        ],
        "responses": { "200": { "description": "Success" } }
      }
    }
  }
}
""";

Console.WriteLine("==> Demo: OpenAPIToolset with JSONPlaceholder API\n");

// Create toolset from OpenAPI spec
var toolset = new OpenAPIToolset(openApiSpec, "json");
var tools = toolset.GetTools();

Console.WriteLine($"Generated {tools.Count} tools from OpenAPI spec:");
foreach (var tool in tools)
{
    Console.WriteLine($"  - {tool.Name}: {tool.Description}");
}

// Use tools with agent
var llm = new MockLlm();
var agent = new LlmAgent(llm, "mock-llm")
{
    Name = "ApiAssistant",
    Instruction = "You are an API assistant with access to JSONPlaceholder API",
    Tools = tools
};

var runner = new InMemoryRunner(agent, appName: "OpenApiApp");

Console.WriteLine("\nAgent with OpenAPI tools ready!");
Console.WriteLine("Tools can be called by LLM with proper parameters.\n");

Console.WriteLine("========================================");
Console.WriteLine("  Demo completed!");
Console.WriteLine("========================================");
