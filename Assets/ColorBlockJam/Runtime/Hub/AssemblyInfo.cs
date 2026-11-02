using System.Runtime.CompilerServices;

// HubSceneInstaller's startup wait takes the project task as an internal overload, so the Hub PlayMode tests can
// run its failed-project-scope branch with a faulted or cancelled task (contract AC-10).
[assembly: InternalsVisibleTo("ColorBlockJam.Hub.PlayModeTests")]

// HubNavigationCatalog.SetTabs lets EditMode fixtures build a tab catalog without an asset (DEC-169).
[assembly: InternalsVisibleTo("ColorBlockJam.Hub.EditModeTests")]
