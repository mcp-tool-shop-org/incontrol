namespace InControl.Core.Models;

/// <summary>
/// A tool the model may call during a reply, such as a web search.
/// The app runs it and hands the result back to the model.
/// </summary>
/// <param name="Name">Name the model calls it by.</param>
/// <param name="Description">What it does, written for the model.</param>
/// <param name="Parameters">String parameters it takes.</param>
/// <param name="Run">Runs the tool with the model's arguments and returns text for the model.</param>
/// <param name="Describe">A short line for the person watching, such as "Searching the web: ollama".</param>
public sealed record ChatTool(
    string Name,
    string Description,
    IReadOnlyList<ChatToolParameter> Parameters,
    Func<IReadOnlyDictionary<string, string>, CancellationToken, Task<string>> Run,
    Func<IReadOnlyDictionary<string, string>, string> Describe);

/// <summary>
/// One string parameter of a <see cref="ChatTool"/>.
/// </summary>
public sealed record ChatToolParameter(string Name, string Description, bool Required = true);
