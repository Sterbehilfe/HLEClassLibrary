﻿using System;

namespace HLE.Twitch.Helix.Models;

[Flags]
public enum EmoteImageFormats
{
    None = 0,
    Static = 1 << 0,
    Animated = 1 << 1
}
