namespace WeihnachtsfeierBit.AdminClient.Models;

public static class JeopardyDefaults
{
    public static object[] Categories() =>
    [
        Category("Bünting & Ostfriesland",
            ("In welcher ostfriesischen Stadt hat die Bünting-Gruppe ihren Sitz?", "Leer"),
            ("Wie heißt der Kandiszucker, der in Ostfriesland in die Teetasse kommt?", "Kluntje"),
            ("Was kommt in Ostfriesland als \"Wulkje\" in den Tee?", "Sahne"),
            ("Welche beiden Supermarkt-Marken gehören zur Bünting-Gruppe?", "Famila und Combi"),
            ("Seit welchem Jahr gibt es den Teehandel Bünting?", "Seit 1806")),
        Category("IT-Alltag",
            ("Wofür steht die Abkürzung \"PC\"?", "Personal Computer"),
            ("Welche Tastenkombination kopiert unter Windows markierten Text?", "Strg + C"),
            ("Wie heißt das Verfahren, bei dem zusätzlich zum Passwort ein zweiter Nachweis nötig ist?", "Zwei-Faktor-Authentifizierung (2FA)"),
            ("Wofür steht die Abkürzung \"VPN\"?", "Virtual Private Network"),
            ("Wofür steht das \"S\" in \"HTTPS\"?", "Secure (sicher, verschlüsselt)")),
        Category("Allgemeinwissen",
            ("Wie viele Tage hat ein Schaltjahr?", "366"),
            ("Wie heißt der größte Planet unseres Sonnensystems?", "Jupiter"),
            ("Welches chemische Symbol hat Gold?", "Au"),
            ("Wer malte die Mona Lisa?", "Leonardo da Vinci"),
            ("Wie viele Knochen hat ein erwachsener Mensch?", "206")),
        Category("Geografie",
            ("Welches Meer liegt nördlich von Ostfriesland?", "Die Nordsee"),
            ("Welcher Fluss fließt durch Köln?", "Der Rhein"),
            ("Wie heißt der höchste Berg Deutschlands?", "Zugspitze"),
            ("Wie heißt die Hauptstadt von Australien?", "Canberra"),
            ("Welches ist das flächenmäßig größte Land der Erde?", "Russland")),
        Category("Weihnachten",
            ("Wie viele Kerzen hat ein klassischer Adventskranz?", "Vier"),
            ("Wie heißt das Rentier mit der roten Nase?", "Rudolph"),
            ("In welchem Land entstand der Brauch des geschmückten Weihnachtsbaums?", "Deutschland"),
            ("In welchem Land wurde \"Stille Nacht, heilige Nacht\" erstmals aufgeführt?", "Österreich (1818, Oberndorf bei Salzburg)"),
            ("Wer schrieb \"A Christmas Carol\" (Eine Weihnachtsgeschichte)?", "Charles Dickens")),
        Category("Essen & Trinken",
            ("Aus welcher Frucht wird Wein gemacht?", "Aus Weintrauben"),
            ("Welche Frucht ist die Hauptzutat von Guacamole?", "Avocado"),
            ("Aus welchem Land stammt Sushi?", "Japan"),
            ("Woraus wird Tofu hergestellt?", "Aus Sojabohnen"),
            ("Welches Gewürz gilt als das teuerste der Welt?", "Safran")),
        Category("Technik & Wissenschaft",
            ("Wie nennt man die kleinste Einheit digitaler Information?", "Bit"),
            ("Wie viele Bit sind ein Byte?", "Acht"),
            ("Wie heißt die Hauptplatine eines Computers?", "Mainboard (Motherboard)"),
            ("Wer gilt als Begründer der Relativitätstheorie?", "Albert Einstein"),
            ("Wofür steht die Abkürzung \"KI\" im Englischen?", "AI – Artificial Intelligence"))
    ];

    private static object Category(string name, params (string Question, string Answer)[] clues) => new
    {
        name,
        clues = clues.Select((clue, index) => new
        {
            value = (index + 1) * 100,
            question = clue.Question,
            answer = clue.Answer
        }).ToArray()
    };
}
