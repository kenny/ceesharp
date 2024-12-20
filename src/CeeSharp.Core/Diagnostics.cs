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

    public Suppression Suppress()
    {
        return new Suppression(this, diagnostics.Count);
    }

    private void RestoreTo(int suppressionStart)
    {
        diagnostics.RemoveRange(suppressionStart, diagnostics.Count - suppressionStart);
    }

    public readonly struct Suppression(Diagnostics diagnostics, int suppressionStart)
    {
        public void Restore()
        {
            diagnostics.RestoreTo(suppressionStart);
        }
    }
}