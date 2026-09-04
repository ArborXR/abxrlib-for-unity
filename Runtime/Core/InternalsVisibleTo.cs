/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Internal Visibility Configuration
 * 
 * This file allows the Editor assembly to access internal members of the Runtime assembly.
 * This is necessary for build-time processing (e.g., Android manifest post-processing)
 * while keeping these APIs hidden from external user code.
 *
 * AbxrLib.WorldSpace is the optional world-space UI (imported from Samples~). It is a separate assembly but
 * still part of AbxrLib, and it needs the same internals the UI used when it lived in this assembly - for
 * example AbxrSubsystem and Configuration.
 */

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AbxrLib.Editor")]
[assembly: InternalsVisibleTo("AbxrLib.WorldSpace")]
[assembly: InternalsVisibleTo("AbxrLib.Tests.EditMode")]
[assembly: InternalsVisibleTo("AbxrLib.Tests.PlayMode")]
