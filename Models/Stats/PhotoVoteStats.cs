namespace BeauOuPas.Models.Stats;

public class PhotoVoteStats
{
    public string ProjectId { get; set; } = string.Empty;

    // Scores globaux
    public int TotalVotes { get; set; }
    public double ScoreMoyen { get; set; }
    public int TotalJaime { get; set; }
    public int TotalMoyen { get; set; }
    public int TotalPasFan { get; set; }

    // Par genre
    public int VotesHommes { get; set; }
    public int VotesFemmes { get; set; }
    public int VotesAutres { get; set; }

    // Par tranche d'âge
    public int Age13_17 { get; set; }
    public int Age18_24 { get; set; }
    public int Age25_34 { get; set; }
    public int Age35_49 { get; set; }
    public int Age50_64 { get; set; }
    public int Age65Plus { get; set; }

    // Pourcentages calculés
    public double PctJaime => TotalVotes > 0 ? (TotalJaime * 100.0 / TotalVotes) : 0;
    public double PctMoyen => TotalVotes > 0 ? (TotalMoyen * 100.0 / TotalVotes) : 0;
    public double PctPasFan => TotalVotes > 0 ? (TotalPasFan * 100.0 / TotalVotes) : 0;
    public double PctHommes => TotalVotes > 0 ? (VotesHommes * 100.0 / TotalVotes) : 0;
    public double PctFemmes => TotalVotes > 0 ? (VotesFemmes * 100.0 / TotalVotes) : 0;
}
