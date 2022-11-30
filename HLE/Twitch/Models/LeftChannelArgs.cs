﻿using System;

namespace HLE.Twitch.Models;

/// <summary>
/// <see cref="EventArgs"/> used whe a user left a channel.
/// </summary>
public sealed class LeftChannelArgs : EventArgs
{
    /// <summary>
    /// The username of the user that left the channel. All lower case.
    /// </summary>
    public string Username { get; }

    /// <summary>
    /// The chanel the user left. All lower case.
    /// </summary>
    public string Channel { get; }

    /// <summary>
    /// The default constructor of <see cref="LeftChannelArgs"/>.
    /// </summary>
    /// <param name="ircMessage">The IRC message.</param>
    /// <param name="ircRanges">Ranges that represent the message split on whitespaces.</param>
    public LeftChannelArgs(ReadOnlySpan<char> ircMessage, Span<Range> ircRanges)
    {
        ReadOnlySpan<char> split0 = ircMessage[ircRanges[0]];
        int idxExcl = split0.IndexOf('!');
        Username = new(split0[1..idxExcl]);
        Channel = new(ircMessage[ircRanges[^1]][1..]);
    }
}
