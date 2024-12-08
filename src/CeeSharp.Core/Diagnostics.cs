namespace CeeSharp.Core;

public record Diagnostic(DiagnosticSeverity Severity, int Position, string Message);

public sealed class Diagnostics
{
    private readonly List<Diagnostic> diagnostics = [];

    public IReadOnlyList<Diagnostic> AllDiagnostics => diagnostics;

    public void ReportWarning(int position, string message)
    {
        diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, position, message));
    }

    public void ReportError(int position, string message)
    {
        diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, position, message));
    }
}