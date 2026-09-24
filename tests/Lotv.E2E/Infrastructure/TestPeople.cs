namespace Lotv.E2E.Infrastructure;

/// <summary>
/// Believable family names for records the E2E tests create in the local dev database, so the board
/// and lists never fill up with random-looking names. Surnames are ones the seed data doesn't use.
/// </summary>
public static class TestPeople
{
    private static readonly string[] Surnames =
    [
        "Ashworth", "Bellamy", "Castellano", "Dunleavy", "Fairbanks", "Gallagher", "Hargrove", "Iverson", "Kowalczyk",
        "Lindqvist", "Montgomery", "Nakamura", "Pemberton", "Quigley", "Rosenthal", "Sandoval", "Thackeray", "Underhill",
        "Vasquez", "Whitcomb", "Yarborough", "Abernathy", "Blackwell", "Calloway", "Delacroix", "Ellsworth", "Fontaine",
        "Grimaldi", "Holloway", "Jarvis",
    ];
    private static readonly string[] HusbandNames = ["Tom", "Andrew", "Brian", "Carlos", "Daniel", "Ethan", "Frank", "George", "Henry", "Isaac", "Jack", "Kevin"];
    private static readonly string[] WifeNames = ["Ann", "Beth", "Clara", "Diana", "Elena", "Faith", "Grace", "Hannah", "Irene", "Julia", "Karen", "Laura"];
    private static readonly string[] MotherNames = ["Rosalind", "Marguerite", "Josephine", "Cordelia", "Beatrice", "Genevieve", "Philippa", "Rosemary"];

    public record Couple(string Husband, string Wife, string Last)
    {
        public string HusbandFull => $"{Husband} {Last}";
        public string WifeFull => $"{Wife} {Last}";
    }

    public static Couple NewCouple() => new(Pick(HusbandNames), Pick(WifeNames), Pick(Surnames));

    public static string NewMother() => $"{Pick(MotherNames)} {Pick(Surnames)}";

    private static string Pick(string[] list) => list[Random.Shared.Next(list.Length)];
}
