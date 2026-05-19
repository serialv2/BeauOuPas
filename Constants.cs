namespace BeauOuPas;

public static class Constants
{
    // 🔑 Remplace par tes vraies clés Supabase
    // Dashboard Supabase → Settings → API
    public const string SupabaseUrl = "https://iybjtvwdnqnebuexesiz.supabase.co";
    public const string SupabaseKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Iml5Ymp0dndkbnFuZWJ1ZXhlc2l6Iiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzYwMDEwNjgsImV4cCI6MjA5MTU3NzA2OH0.dAImywya67QbKj1dCR3GZg4p24jxPPvQi5wGkJvdNLk";
    public const string AdMobAppId = "ca-app-pub-5814544077070305~4139208130";
    // Âge minimum pour s'inscrire
    public const int AgeMinimum = 13;

    // Tranches d'âge pour les statistiques
    public static readonly (int Min, int Max, string Label)[] TranchesDAge =
    {
        (13, 17, "13-17 ans"),
        (18, 24, "18-24 ans"),
        (25, 34, "25-34 ans"),
        (35, 49, "35-49 ans"),
        (50, 64, "50-64 ans"),
        (65, 99, "65 ans et +")
    };
}
