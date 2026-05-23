using Postgrest.Attributes;
using Postgrest.Models;
using System.Text.Json.Serialization;
using BeauOuPas.Resources.Strings;

namespace BeauOuPas.Models;

[Table("credit_transactions")]
public class CreditTransaction : BaseModel
{
    [PrimaryKey("id", false)]
    public string Id { get; set; } = string.Empty;

    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("amount")]
    public int Amount { get; set; }

    [Column("type")]
    public string Type { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    // ─── Propriétés calculées - pas en DB ────────────────────────────
    [JsonIgnore]
    public bool IsGain => Amount > 0;

    [JsonIgnore]
    public string AmountLabel => IsGain
        ? $"+{Amount} crédit{(Amount > 1 ? "s" : "")}"
        : $"{Amount} crédit{(Math.Abs(Amount) > 1 ? "s" : "")}";

    // ─── Libellé du type (i18n) ──────────────────────────────────────
    // Le libellé est désormais TRADUIT via AppResources (cohérent avec
    // le reste de l'app, 7 langues), au lieu d'être codé en dur en FR.
    //
    // Mapping : chaque `Type` (valeur de credit_transactions.type) est
    // mappé vers une clé de ressource "Credit_Type_<Type>".
    //
    // Fallback ROBUSTE en cascade :
    //   1) clé de traduction "Credit_Type_<Type>" si elle existe
    //   2) sinon, la Description de la transaction (déjà lisible,
    //      ex. "Création d'une partie rapide")
    //   3) sinon, le Type brut (dernier recours, ne devrait pas arriver)
    //
    // Ainsi un type inconnu (futur) n'affichera JAMAIS de code technique
    // brut tant que la transaction a une description, et restera
    // correct dès qu'on ajoute la clé .resx correspondante.
    [JsonIgnore]
    public string TypeLabel
    {
        get
        {
            // 1) Tentative de traduction via la clé Credit_Type_<Type>
            if (!string.IsNullOrWhiteSpace(Type))
            {
                try
                {
                    var rm = AppResources.ResourceManager;
                    var culture = AppResources.Culture;
                    var key = "Credit_Type_" + Type;
                    var translated = rm.GetString(key, culture);
                    if (!string.IsNullOrWhiteSpace(translated))
                        return translated;
                }
                catch
                {
                    // ResourceManager indisponible : on passe au fallback
                }
            }

            // 2) Fallback : description de la transaction (déjà lisible)
            if (!string.IsNullOrWhiteSpace(Description))
                return Description!;

            // 3) Dernier recours : le type brut
            return Type;
        }
    }
}