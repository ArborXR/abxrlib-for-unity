/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Internal Visibility Configuration
 * 
 * This file allows the Editor assembly to access internal members of the Runtime assembly
 * for build-time processing while keeping these APIs hidden from external user code.
 */

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AbxrLib.Editor")]

[assembly: InternalsVisibleTo("AbxrLib.Tests.Editor")]
[assembly: InternalsVisibleTo("AbxrLib.Tests.Runtime")]
