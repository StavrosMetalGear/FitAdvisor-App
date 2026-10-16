using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;

namespace FitAdvisor.Mobile;

[Activity(Name = "com.stavros.fitadvisor.HealthPrivacyActivity", Exported = true,
    Permission = "android.permission.START_VIEW_PERMISSION_USAGE")]
[IntentFilter(new[] { "android.intent.action.VIEW_PERMISSION_USAGE" },
    Categories = new[] { "android.intent.category.HEALTH_PERMISSIONS" })]
public sealed class HealthPrivacyActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        content.SetPadding(32, 32, 32, 32);
        content.AddView(new TextView(this) { Text = "FitAdvisor privacy", TextSize = 24 });
        content.AddView(new TextView(this) { Text =
            "FitAdvisor reads exercise sessions when you tap Import and grant Health Connect read access. After explicitly linking an imported gym session, optional heart-rate and active-calorie switches request only the measurement permissions you enable. Readings use the linked source and overlapping times. Missing records are unavailable; no live monitoring. " +
            "If you select the optional older-history request, supported devices can also ask for access before the standard first-grant history cutoff. Denying it keeps standard exercise access. This does not grant access to other health record types. " +
            "It saves session dates, duration, type, title, notes and source identifiers in its private local database. " +
            "It does not read routes, location, weight or steps, write to Health Connect, or run background imports. " +
            "INTERNET enables optional Open Food Facts lookup. Only your product query/barcode and app identifier are sent; the service sees your IP. Diary, health records and backups are never sent to product services. No analytics, remote crash reporting or automatic cloud backup is used. " +
            "You can revoke access in Health Connect. Imported copies remain locally until you delete them. " +
            "Encrypted backups leave app-private storage only when you choose Export and a document destination. " +
            "The selected document provider controls file storage; choose on-device storage to keep backups on the phone.", TextSize = 18 });
        var close = new Android.Widget.Button(this) { Text = "Close" }; close.Click += (_, _) => Finish(); content.AddView(close);
        var scroll = new Android.Widget.ScrollView(this); scroll.AddView(content); SetContentView(scroll);
    }
}
