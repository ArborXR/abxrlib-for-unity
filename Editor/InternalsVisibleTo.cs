using System.Runtime.CompilerServices;

// The EditMode tests pin the wizard's pure helpers: version parsing and comparison, and the duplicate-copy
// survivor selection. Both had review findings on the 3.0 branch, so they stay tested.
[assembly: InternalsVisibleTo("AbxrLib.Tests.EditMode")]
