param([string]$UsdaArchive='artifacts/usda-sr-legacy-2018-04-complete.zip')
$ErrorActionPreference='Stop'
$projects=@(
 'verification/FitAdvisor.CalculationChecks.csproj',
 'verification/Persistence/FitAdvisor.PersistenceChecks.csproj',
 'verification/Offline/FitAdvisor.OfflineChecks.csproj',
 'verification/Habits/FitAdvisor.HabitChecks.csproj',
 'verification/Catalogue/FitAdvisor.CatalogueChecks.csproj',
 'verification/Gym/FitAdvisor.GymChecks.csproj',
 'verification/Design/FitAdvisor.DesignChecks.csproj',
 'verification/PhoneFeedback/FitAdvisor.PhoneFeedbackChecks.csproj'
)
foreach($project in $projects) {
 $extra=if($project -match '/Catalogue/'){@('--',$UsdaArchive)}else{@()}
 & dotnet run --project $project -c Release -p:JsonSerializerIsReflectionEnabledByDefault=false @extra
 if($LASTEXITCODE -ne 0){throw "Failed: $project"}
}
