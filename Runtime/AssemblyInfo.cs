/*
 * Copyright (c) 2024 ArborXR. All rights reserved.
 *
 * IL2CPP / managed-stripping protection for the AbxrLib runtime assembly.
 *
 * AbxrLib initialises itself through [RuntimeInitializeOnLoadMethod] (see Runtime/Core/Initialize.cs)
 * rather than being called from the host application. A consumer project can therefore contain no
 * reference to this assembly at all, in which case the managed linker is free to drop the whole
 * assembly from the player build and the automatic initialisation never runs. AlwaysLinkAssembly tells
 * Unity to include the assembly regardless of whether anything references it.
 *
 * Type-level preservation lives with the individual types ([Preserve] on components that are only ever
 * instantiated from serialized prefabs) and in link.xml at the package root.
 */

using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]
