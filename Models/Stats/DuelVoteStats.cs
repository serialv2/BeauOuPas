namespace BeauOuPas.Models.Stats;

public class DuelVoteStats
{
    public string ProjectId { get; set; } = string.Empty;

    public int TotalVotes { get; set; }
    public int VotesLeft { get; set; }
    public int VotesRight { get; set; }

    // Par genre
    public int VotesHommes { get; set; }
    public int VotesFemmes { get; set; }

    // Par tranche d'âge
    public int Age13_17 { get; set; }
    public int Age18_24 { get; set; }
    public int Age25_34 { get; set; }
    public int Age35_49 { get; set; }
    public int Age50_64 { get; set; }
    public int Age65Plus { get; set; }

    // Pourcentages pour les barres de progression
    public double PctLeft => TotalVotes > 0 ? (VotesLeft * 100.0 / TotalVotes) : 0;
    public double PctRight => TotalVotes > 0 ? (VotesRight * 100.0 / TotalVotes) : 0;
    public double PctHommes => TotalVotes > 0 ? (VotesHommes * 100.0 / TotalVotes) : 0;
    public double PctFemmes => TotalVotes > 0 ? (VotesFemmes * 100.0 / TotalVotes) : 0;
}