﻿using HLE.Twitch.Args;

namespace HLE.Twitch.Models;

public class Channel
{
    public string Name { get; }

    public int Id { get; }

    public bool EmoteOnly { get; private set; }

    public int FollowerOnly { get; private set; }

    public bool R9K { get; private set; }

    public bool Rituals { get; private set; }

    public int SlowMode { get; private set; }

    public bool SubOnly { get; private set; }

    internal Channel(RoomstateArgs args)
    {
        Name = args.Channel;
        Id = args.ChannelId;
        Update(args);
    }

    internal void Update(RoomstateArgs args)
    {
        EmoteOnly = args.EmoteOnly;
        FollowerOnly = args.FollowerOnly;
        R9K = args.R9K;
        Rituals = args.Rituals;
        SlowMode = args.SlowMode;
        SubOnly = args.SubOnly;
    }
}
