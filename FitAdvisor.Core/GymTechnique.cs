namespace FitAdvisor.Core;

public sealed record TechniqueGuide(string English, string Greek, string Source, string? Video, string Illustration);
public static class GymTechnique
{
    // Original concise paraphrases, checked against the linked exact variation on 2026-10-14.
    public static TechniqueGuide? For(GymExercise e) => e.Custom ? null : e.Key == GymCatalogue.Starter.Single(x => x.Id == "bw-squat").Key ? new(
        "Setup: Feet slightly wider than hips, toes slightly outward. Brace your trunk.\nMovement: Send hips back, bend knees, keep heels down and knees following toes. Rise with hips and chest together.\nBreathing: Inhale down; exhale up.\nMistakes: Heels lifting, knees collapsing inward, back rounding. Reduce depth if alignment changes.\nSafety: Move slowly within a comfortable range; stop if painful.",
        "Στήσιμο: Πέλματα λίγο πιο ανοιχτά από τους γοφούς, δάχτυλα ελαφρά έξω. Σταθεροποίησε τον κορμό.\nΚίνηση: Γοφοί πίσω, λύγισε γόνατα, φτέρνες κάτω και γόνατα προς τα δάχτυλα. Ανέβασε μαζί γοφούς και στήθος.\nΑναπνοή: Εισπνοή κάτω, εκπνοή πάνω.\nΛάθη: Σηκωμένες φτέρνες, γόνατα προς τα μέσα, καμπύλωση πλάτης. Μείωσε το βάθος αν χάνεται η ευθυγράμμιση.\nΑσφάλεια: Αργή, άνετη κίνηση. Σταμάτησε αν πονάς.",
        "https://www.acefitness.org/resources/everyone/exercise-library/135/bodyweight-squat/", null, "technique_squat.png") :
        e.Key == GymCatalogue.Starter.Single(x => x.Id == "push-up").Key ? new(
        "Setup: Hands slightly wider than shoulders; straight body from head to heels, trunk braced.\nMovement: Lower chest toward floor with elbows roughly 45° from torso. Press back up, keeping alignment.\nBreathing: Breathe continuously; exhale during the press.\nMistakes: Sagging hips, flared elbows, bouncing. Use only a comfortable range.\nSafety: Controlled movement; stop if painful. Ask a qualified trainer for an easier variation if needed.",
        "Στήσιμο: Χέρια λίγο πιο ανοιχτά από ώμους, σώμα ευθύ από κεφάλι έως φτέρνες, σταθερός κορμός.\nΚίνηση: Κατέβασε στήθος προς έδαφος, αγκώνες περίπου 45° από κορμό. Πίεσε πάνω διατηρώντας ευθυγράμμιση.\nΑναπνοή: Συνεχής αναπνοή, εκπνοή στην πίεση.\nΛάθη: Γοφοί που βυθίζονται, ανοιχτοί αγκώνες, αναπήδηση. Κινήσου σε άνετο εύρος.\nΑσφάλεια: Ελεγχόμενη κίνηση, σταμάτησε αν πονάς. Ζήτησε ευκολότερη παραλλαγή από καταρτισμένο προπονητή.",
        "https://www.nasm.org/resource-center/exercise-library/push-up", "https://www.youtube.com/watch?v=WDIpL0pjun0", "technique_pushup.png") : null;
}
