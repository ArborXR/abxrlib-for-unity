/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 * 
 * AbxrLib for Unity - Internal Visibility Configuration
 * 
 * This file allows the Editor assembly to access internal members of the Runtime assembly.
 * This is necessary for build-time processing (e.g., Android manifest post-processing)
 * while keeping these APIs hidden from external user code.
 */

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("AbxrLib.Editor")]
