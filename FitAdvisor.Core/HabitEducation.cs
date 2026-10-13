namespace FitAdvisor.Core;

public sealed record HabitEducationCard(string Kind, string Title, string GreekTitle, string English, string Greek,
    string SourceTitle, string Date, string Url);

public static class HabitEducation
{
    // Source review: 2026-10-13. Original summaries, not individual predictions.
    public static IReadOnlyList<HabitEducationCard> Cards { get; } = [
        new("smoking", "Health and support", "Υγεία και υποστήριξη",
            "Established: tobacco causes cardiovascular and lung disease and cancer; no exposure level is safe. Quitting helps health. A clinician or cessation service can discuss counselling and medicines. You can ask for support again after a setback.",
            "Τεκμηριωμένο: ο καπνός προκαλεί καρδιαγγειακές και πνευμονικές παθήσεις και καρκίνο· δεν υπάρχει ασφαλές επίπεδο έκθεσης. Η διακοπή ωφελεί την υγεία. Ιατρός ή υπηρεσία διακοπής μπορεί να συζητήσει συμβουλευτική και φάρμακα. Μπορείτε να ζητήσετε ξανά υποστήριξη μετά από μια δυσκολία.",
            "WHO — Tobacco and nicotine", "2026-06-26", "https://www.who.int/news-room/fact-sheets/detail/tobacco"),
        new("smoking", "Training and endurance", "Προπόνηση και αντοχή",
            "Established mechanisms: smoking reduces oxygen delivery and harms lung function, making exercise harder, even with a few cigarettes daily. Your log cannot predict performance loss or recovery time. A short comfortable walk may help you manage a craving.",
            "Τεκμηριωμένοι μηχανισμοί: το κάπνισμα μειώνει την παροχή οξυγόνου και βλάπτει τη λειτουργία των πνευμόνων, δυσκολεύοντας την άσκηση ακόμη και με λίγα τσιγάρα καθημερινά. Η καταγραφή δεν προβλέπει απώλεια απόδοσης ή χρόνο αποκατάστασης. Ένας σύντομος άνετος περίπατος μπορεί να βοηθήσει με την επιθυμία.",
            "US National Cancer Institute, Smokefree — How Smoking Affects Your Workout", "No publication/update date shown; checked 2026-10-13", "https://smokefree.gov/quit-smoking/why-you-should-quit/how-smoking-affects-your-workout"),
        new("smoking", "Smoking and sleep", "Κάπνισμα και ύπνος",
            "Association: a review of cohort studies linked regular smoking with insomnia. Observational studies cannot prove that smoking explains an individual's sleep difficulty; the evidence was limited. If sleep problems persist, talk with a clinician.",
            "Συσχέτιση: ανασκόπηση μελετών κοόρτης συνέδεσε το τακτικό κάπνισμα με αϋπνία. Οι παρατηρητικές μελέτες δεν αποδεικνύουν ότι το κάπνισμα εξηγεί τη δυσκολία ύπνου ενός ατόμου· τα στοιχεία ήταν περιορισμένα. Αν τα προβλήματα επιμένουν, συζητήστε με ιατρό.",
            "Smoking and incidence of insomnia: a systematic review and meta-analysis of cohort studies", "2021 (publication year)", "https://pubmed.ncbi.nlm.nih.gov/34507139/"),
        new("alcohol", "Amount and context matter", "Η ποσότητα και το πλαίσιο μετρούν",
            "Established: alcohol causes several cancers and can cause dependence. No consumption is risk-free; most harm comes from heavy episodes or sustained heavy consumption. Amount, frequency, health and context matter. A personal grams goal is not a safe limit or an individual risk prediction.",
            "Τεκμηριωμένο: το αλκοόλ προκαλεί διάφορους καρκίνους και μπορεί να προκαλέσει εξάρτηση. Καμία κατανάλωση δεν είναι χωρίς κίνδυνο· οι περισσότερες βλάβες προέρχονται από επεισόδια μεγάλης ή συνεχή μεγάλη κατανάλωση. Μετρούν η ποσότητα, η συχνότητα, η υγεία και το πλαίσιο. Ο προσωπικός στόχος γραμμαρίων δεν είναι ασφαλές όριο ή ατομική πρόβλεψη κινδύνου.",
            "WHO — Alcohol", "2024-06-28", "https://www.who.int/news-room/fact-sheets/detail/alcohol"),
        new("alcohol", "Alcohol and sleep", "Αλκοόλ και ύπνος",
            "Experimental evidence: a 27-study review in healthy adults found delayed and reduced REM sleep after alcohol, with greater disruption at higher doses. Low-dose study conditions also showed effects. Falling asleep faster does not mean better sleep. Dose and timing vary; this log cannot predict your night's sleep.",
            "Πειραματικά στοιχεία: ανασκόπηση 27 μελετών σε υγιείς ενήλικες βρήκε καθυστέρηση και μείωση του ύπνου REM μετά από αλκοόλ, με μεγαλύτερη διαταραχή σε υψηλότερες δόσεις. Επιδράσεις υπήρχαν και σε συνθήκες χαμηλής δόσης. Το να αποκοιμηθείτε γρηγορότερα δεν σημαίνει καλύτερο ύπνο. Δόση και χρονισμός διαφέρουν· η καταγραφή δεν προβλέπει τον ύπνο σας.",
            "The effect of alcohol on subsequent sleep in healthy adults: A systematic review and meta-analysis", "2025-04 (journal issue)", "https://pubmed.ncbi.nlm.nih.gov/39631226/"),
        new("alcohol", "Recovery after strength training", "Αποκατάσταση μετά από προπόνηση δύναμης",
            "Limited evidence: small, short-term studies, often using large alcohol doses after exercise, found reduced muscle protein synthesis. Strength, power and soreness findings were not consistently worse. Long-term adaptations and effects of smaller amounts remain uncertain. No precise performance loss or recovery deadline can be inferred.",
            "Περιορισμένα στοιχεία: μικρές βραχυχρόνιες μελέτες, συχνά με μεγάλες δόσεις αλκοόλ μετά την άσκηση, βρήκαν μειωμένη μυϊκή πρωτεϊνοσύνθεση. Τα ευρήματα για δύναμη, ισχύ και πόνο δεν ήταν σταθερά χειρότερα. Οι μακροχρόνιες προσαρμογές και οι επιδράσεις μικρότερων ποσοτήτων παραμένουν αβέβαιες. Δεν προκύπτει ακριβής απώλεια απόδοσης ή προθεσμία αποκατάστασης.",
            "The Effects of Alcohol Consumption on Recovery Following Resistance Exercise: A Systematic Review", "2019-06-26", "https://pmc.ncbi.nlm.nih.gov/articles/PMC7739274/"),
        new("alcohol", "Get help to stop safely", "Υποστήριξη για ασφαλή διακοπή",
            "Clinical guidance: if you may be dependent, or shake, sweat or feel sick when reducing alcohol, seek medical advice before stopping or reducing; sudden withdrawal can be dangerous. Confusion, hallucinations, severe shaking or seizures need emergency help. Contact local emergency services. A clinician or alcohol service can help without judgement; this app does not provide detox treatment.",
            "Κλινική καθοδήγηση: αν μπορεί να υπάρχει εξάρτηση, ή έχετε τρέμουλο, εφίδρωση ή ναυτία όταν μειώνετε το αλκοόλ, ζητήστε ιατρική συμβουλή πριν τη διακοπή ή μείωση· η απότομη στέρηση μπορεί να είναι επικίνδυνη. Σύγχυση, παραισθήσεις, έντονο τρέμουλο ή σπασμοί χρειάζονται επείγουσα βοήθεια. Επικοινωνήστε με τις τοπικές υπηρεσίες έκτακτης ανάγκης. Ιατρός ή υπηρεσία εξαρτήσεων μπορεί να βοηθήσει χωρίς επίκριση· η εφαρμογή δεν παρέχει θεραπεία αποτοξίνωσης.",
            "NHS — Alcohol-use disorder", "Reviewed 2026-04-24", "https://www.nhs.uk/conditions/alcohol-use-disorder/"),
        new("eating", "Patterns, portions and frequency", "Μοτίβα, μερίδες και συχνότητα",
            "Public-health guidance: an adequate, balanced and varied dietary pattern supports health. Consider frequency and portions of sugary drinks, snacks or takeaway in the whole pattern. One meal does not erase progress; takeaway and snacks vary in composition. Choose a manageable goal that fits your budget and preferences, without labelling every food good or bad.",
            "Καθοδήγηση δημόσιας υγείας: ένα επαρκές, ισορροπημένο και ποικίλο διατροφικό μοτίβο στηρίζει την υγεία. Δείτε τη συχνότητα και τις μερίδες ζαχαρούχων ποτών, σνακ ή έτοιμου φαγητού στο συνολικό μοτίβο. Ένα γεύμα δεν ακυρώνει την πρόοδο· η σύστασή τους ποικίλλει. Επιλέξτε εφικτό στόχο που ταιριάζει στις προτιμήσεις και τον προϋπολογισμό σας, χωρίς χαρακτηρισμούς κάθε τροφής ως καλής ή κακής.",
            "WHO — Healthy diet", "2026-01-26", "https://www.who.int/news-room/fact-sheets/detail/healthy-diet")
    ];
}
