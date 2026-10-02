namespace InControl.Core.UX;

/// <summary>
/// Centralized UX strings following the UX Contract.
/// All user-facing text should be defined here for consistency.
/// </summary>
public static class UXStrings
{
    /// <summary>
    /// Application identity.
    /// </summary>
    public static class App
    {
        public const string Name = "InControl";
        public const string Tagline = "Local AI Workstation";
    }

    /// <summary>
    /// Session-related strings.
    /// </summary>
    public static class Session
    {
        public const string New = "New session";
        public const string EmptyTitle = "Untitled session";
        public const string EmptyState = "This session is empty. Enter a prompt to begin.";
        public const string NoSessions = "No sessions yet. Create one to start working.";
        public const string Delete = "Delete session";
        public const string DeleteConfirm = "Delete this session? This cannot be undone.";
        public const string Duplicate = "Duplicate session";
        public const string Rename = "Rename session";
        public const string Export = "Export session";
        public const string Pin = "Pin session";
        public const string Unpin = "Unpin session";
    }

    /// <summary>
    /// Execution-related strings.
    /// </summary>
    public static class Execution
    {
        public const string Cancel = "Cancel";
        public const string Retry = "Retry";
        public const string Regenerate = "Regenerate";

        public static string RunComplete(double seconds, int tokens) =>
            $"Run complete · {seconds:F1}s · {tokens:N0} tokens";

        public static string Elapsed(TimeSpan elapsed) =>
            elapsed.TotalMinutes >= 1
                ? $"{elapsed.Minutes}:{elapsed.Seconds:D2}"
                : $"{elapsed.Seconds}.{elapsed.Milliseconds / 100}s";
    }

    /// <summary>
    /// Model-related strings.
    /// </summary>
    public static class Model
    {
        public const string NoModels = "No models available. Pull a model to get started.";
        public const string Select = "Select model";
        public const string Loading = "Loading model...";

        public static string NotFound(string modelName) =>
            $"\"{modelName}\" is not available on this device.";

        public static string PullHint(string modelName) =>
            $"Pull model with: ollama pull {modelName}";
    }

    /// <summary>
    /// Connection-related strings.
    /// </summary>
    public static class Connection
    {
        public const string Checking = "Checking connection...";
        public const string Connected = "Connected";
        public const string Disconnected = "Disconnected";
        public const string Retry = "Retry connection";

        public static string Unavailable(string endpoint) =>
            $"The inference backend at {endpoint} is not responding.";

        public const string CheckOllama = "Check that Ollama is running";
    }

    /// <summary>
    /// Context-related strings.
    /// </summary>
    public static class Context
    {
        public const string NoItems = "No context items attached. Add files or previous outputs.";
        public const string Add = "Add context";
        public const string Remove = "Remove from context";
        public const string Include = "Include in context";
        public const string Exclude = "Exclude from context";

        public static string LimitExceeded(int limit) =>
            $"The input exceeds the model's context window ({limit:N0} tokens).";

        public const string ReduceContext = "Reduce context size";
        public const string StartNew = "Start a new session";
    }

    /// <summary>
    /// Input composer strings.
    /// </summary>
    public static class Input
    {
        public const string Placeholder = "Enter a prompt...";
        public const string Send = "Send";
        public const string Attach = "Attach context";

        public static class Mode
        {
            public const string Chat = "Chat";
            public const string Analyze = "Analyze";
            public const string Draft = "Draft";
            public const string Tool = "Tool";
        }

        public static class Hint
        {
            public const string Send = "Enter to send";
            public const string Newline = "Shift+Enter for new line";
        }
    }

    /// <summary>
    /// Action labels.
    /// </summary>
    public static class Actions
    {
        public const string Copy = "Copy";
        public const string Export = "Export";
        public const string Expand = "Expand";
        public const string Collapse = "Collapse";
        public const string Settings = "Settings";
        public const string Search = "Search";
    }

    /// <summary>
    /// Inspector panel strings.
    /// </summary>
    public static class Inspector
    {
        public const string RunTab = "Run";
        public const string ContextTab = "Context";

        public static class Run
        {
            public const string Device = "Device";
            public const string Model = "Model";
            public const string Latency = "Latency";
            public const string TokensIn = "Tokens in";
            public const string TokensOut = "Tokens out";
            public const string Memory = "Memory";
        }
    }

    /// <summary>
    /// First-run experience strings.
    /// </summary>
    public static class Welcome
    {
        public const string Title = "Welcome to InControl";
        public const string WhatItIs = "A local AI workstation for your RTX GPU.";
        public const string WhatItIsNot = "Not a cloud service. Your data stays on your device.";
        public const string RunsLocally = "Everything runs locally on your machine.";
        public const string GetStarted = "Get started";
        public const string LastSession = "Continue where you left off";

        public static string LastWorking(string sessionTitle) =>
            $"You were last working on: {sessionTitle}";
    }

    /// <summary>
    /// Time formatting.
    /// </summary>
    public static class Time
    {
        public static string Relative(DateTimeOffset timestamp)
        {
            var elapsed = DateTimeOffset.UtcNow - timestamp;

            if (elapsed.TotalSeconds < 60)
                return "Just now";
            if (elapsed.TotalMinutes < 60)
                return $"{(int)elapsed.TotalMinutes} minute{((int)elapsed.TotalMinutes == 1 ? "" : "s")} ago";
            if (elapsed.TotalHours < 24)
                return $"{(int)elapsed.TotalHours} hour{((int)elapsed.TotalHours == 1 ? "" : "s")} ago";
            if (elapsed.TotalDays < 2)
                return $"Yesterday at {timestamp.LocalDateTime:h:mm tt}";

            return timestamp.LocalDateTime.ToString("MMM d, yyyy");
        }
    }
}
