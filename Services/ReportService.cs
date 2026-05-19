namespace BeauOuPas.Services;

public class ReportService
{
    private readonly Supabase.Client _supabase;

    public ReportService(Supabase.Client supabase)
    {
        _supabase = supabase;
    }

    public async Task<bool> ReportProjectAsync(string projectId, string reason)
    {
        try
        {
            var result = await _supabase.Rpc<bool>(
                "report_project",
                new Dictionary<string, object>
                {
                    { "p_project_id", projectId },
                    { "p_reason", reason }
                });

            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ReportService] error: {ex.Message}");
            return false;
        }
    }
}