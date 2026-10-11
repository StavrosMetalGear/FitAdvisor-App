namespace FitAdvisor.Core;

/// <summary>Original paired messages. Selection uses the caller's local calendar date.</summary>
public static class DailyEncouragement
{
    public sealed record Message(string English, string Greek);
    private static readonly Message[] Messages =
    [
        new("Small steps can be part of a steady rhythm. Choose something manageable today.", "Τα μικρά βήματα μπορούν να γίνουν ένας σταθερός ρυθμός. Διάλεξε κάτι εφικτό σήμερα."),
        new("Rest belongs in your routine too. Give yourself room to recover.", "Η ξεκούραση έχει κι αυτή θέση στη ρουτίνα σου. Δώσε στον εαυτό σου χώρο για ανάκαμψη."),
        new("Find a little enjoyment in movement, at a pace that feels comfortable.", "Βρες λίγη χαρά στην κίνηση, με έναν ρυθμό που σου είναι άνετος."),
        new("You can return to a habit gently. There is no need to make up for yesterday.", "Μπορείς να επιστρέψεις σε μια συνήθεια ήρεμα. Δεν χρειάζεται να αναπληρώσεις το χθες."),
        new("Consistency can be flexible. Let today's routine fit today's energy.", "Η συνέπεια μπορεί να είναι ευέλικτη. Προσάρμοσε τη σημερινή ρουτίνα στη σημερινή σου ενέργεια."),
        new("Notice what you enjoy, and make a little space for it.", "Παρατήρησε τι σου αρέσει και δώσε του λίγο χώρο."),
        new("A quieter day can still be a caring choice for yourself.", "Μια πιο ήσυχη μέρα μπορεί κι αυτή να είναι μια επιλογή φροντίδας για τον εαυτό σου."),
        new("Gradual progress leaves room to learn what works for you.", "Η σταδιακή πρόοδος αφήνει χώρο να μάθεις τι σου ταιριάζει."),
        new("Choose a routine you can enjoy, rather than a day you have to perfect.", "Διάλεξε μια ρουτίνα που μπορείς να απολαύσεις, χωρίς να χρειάζεται να τελειοποιήσεις τη μέρα σου."),
        new("Listening to your need for rest is part of looking after yourself.", "Το να ακούς την ανάγκη σου για ξεκούραση είναι μέρος της φροντίδας σου."),
        new("A familiar small habit can be a comfortable place to begin again.", "Μια γνώριμη μικρή συνήθεια μπορεί να είναι μια άνετη αρχή για να επιστρέψεις."),
        new("Leave room for music, fresh air, or whatever makes your day more enjoyable.", "Άφησε χώρο για μουσική, καθαρό αέρα ή ό,τι κάνει τη μέρα σου πιο ευχάριστη.")
    ];

    public static Message ForDay(DateOnly localDate) => Messages[localDate.DayNumber % Messages.Length];

    // A persisted pair keeps the same wording even if the bundled catalog changes mid-day.
    public static string Snapshot(DateOnly localDate)
    {
        var message = ForDay(localDate);
        return $"{localDate:yyyy-MM-dd}|{message.English}|{message.Greek}";
    }

    public static bool TryRead(string snapshot, DateOnly localDate, out Message message)
    {
        var parts = snapshot.Split('|');
        if (parts.Length == 3 && parts[0] == localDate.ToString("yyyy-MM-dd") &&
            !string.IsNullOrWhiteSpace(parts[1]) && !string.IsNullOrWhiteSpace(parts[2]))
        {
            message = new(parts[1], parts[2]);
            return true;
        }
        message = ForDay(localDate);
        return false;
    }
}
