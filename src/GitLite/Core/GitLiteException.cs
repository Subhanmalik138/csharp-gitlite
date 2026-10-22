namespace GitLite.Core;

/// <summary>Domain exception for user-facing GitLite errors.</summary>
sealed class GitLiteException : Exception
{
    public GitLiteException(string message) : base(message) { }
}
