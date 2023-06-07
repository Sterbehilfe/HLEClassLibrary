using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HLE.Collections;
using HLE.Collections.Concurrent;
using HLE.Memory;
using HLE.Strings;
using HLE.Twitch.Models;

namespace HLE.Twitch;

/// <summary>
/// Represents a Twitch chat client.
/// </summary>
public sealed class TwitchClient : IDisposable, IEquatable<TwitchClient>
{
    /// <summary>
    /// The username of the client.
    /// </summary>
    public string Username => _client.Username;

    /// <summary>
    /// Indicates whether the client is connected anonymously or not.
    /// </summary>
    public bool IsAnonymousLogin { get; }

    /// <summary>
    /// Indicates whether the client is connected or not.
    /// </summary>
    public bool IsConnected => _client.IsConnected;

    /// <summary>
    /// Indicates whether the connection uses SSL or not.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public bool UseSSL => _client.UseSSL;

    /// <summary>
    /// The list of channels the client is connected to. Channels can be retrieved by the owner's username or user id in order to read the room state, e.g. if slow-mode is on.
    /// </summary>
    public ChannelList Channels { get; } = new();

    #region Events

    /// <summary>
    /// Is invoked if the client connects.
    /// </summary>W
    public event EventHandler? OnConnected;

    /// <summary>
    /// Is invoked if the client disconnects.
    /// </summary>
    public event EventHandler? OnDisconnected;

    /// <summary>
    /// Is invoked if a user joins a channel.
    /// </summary>
    public event EventHandler<JoinedChannelArgs>? OnJoinedChannel;

    /// <summary>
    /// Is invoked if a user leaves a channel.
    /// </summary>
    public event EventHandler<LeftChannelArgs>? OnLeftChannel;

    /// <summary>
    /// Is invoked if a room state has been received.
    /// </summary>
    public event EventHandler<RoomstateArgs>? OnRoomstateReceived;

    /// <summary>
    /// Is invoked if a chat message has been received.
    /// </summary>
    public event EventHandler<ChatMessage>? OnChatMessageReceived;

    /// <summary>
    /// Is invoked if data is received from the chat server. If this event is subscribed to, the <see cref="ReceivedData"/> instance has to be manually disposed.
    /// Read more in the documentation of the <see cref="ReceivedData"/> class.
    /// </summary>
    public event EventHandler<ReceivedData>? OnDataReceived;

    #endregion Events

    private readonly WebSocketIrcClient _client;
    private readonly IrcHandler _ircHandler = new();
    private readonly ConcurrentPoolBufferList<string> _ircChannels = new();
    private readonly SemaphoreSlim _reconnectionLock = new(1);

    private static readonly Regex _channelPattern = RegexPool.Shared.GetOrAdd(@"^#?\w{3,25}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(250));
    private static readonly Regex _anonymousLoginPattern = RegexPool.Shared.GetOrAdd(@"^justinfan[0-9]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));

    private const string _anonymousUsername = "justinfan123";
    private const char _channelPrefix = '#';
    private const string _pongPrefix = "PONG :";

    /// <summary>
    /// The constructor for an anonymous chat client. An anonymous chat client can only receive messages, but cannot send any messages.
    /// Connects with the username "justinfan123".
    /// <param name="options">The client options. If null, uses default options that can be found on the documentation of <see cref="ClientOptions"/>.</param>
    /// </summary>
    public TwitchClient(ClientOptions options = default)
    {
        _client = new(_anonymousUsername, OAuthToken.Empty, options);
        IsAnonymousLogin = true;
        SetEvents();
    }

    /// <summary>
    /// The constructor for a normal chat client.
    /// </summary>
    /// <param name="username">The username of the client.</param>
    /// <param name="oAuthToken">The OAuth token of the client.</param>
    /// <param name="options">The client options. If null, uses default options that can be found on the documentation of <see cref="ClientOptions"/>.</param>
    /// <exception cref="FormatException">Throws a <see cref="FormatException"/> if <paramref name="username"/> or <paramref name="oAuthToken"/> are in a wrong format.</exception>
    public TwitchClient(string username, OAuthToken oAuthToken, ClientOptions options = default)
    {
        username = FormatChannel(username, false);
        _client = new(username, oAuthToken, options);
        IsAnonymousLogin = false;
        SetEvents();
    }

    /// <summary>
    /// The constructor for a chat client witch an already created <see cref="IrcClient"/>.
    /// </summary>
    /// <param name="ircClient">The IRC client.</param>
    public TwitchClient(WebSocketIrcClient ircClient)
    {
        IsAnonymousLogin = _anonymousLoginPattern.IsMatch(ircClient.Username);
        _client = ircClient;
        SetEvents();
    }

    ~TwitchClient()
    {
        _client.Dispose();
        _ircChannels.Dispose();
    }

    private void SetEvents()
    {
        _client.OnConnected += (_, e) => OnConnected?.Invoke(this, e);
        _client.OnDisconnected += (_, e) => OnDisconnected?.Invoke(this, e);
        _client.OnDataReceived += IrcClient_OnDataReceived;
        _client.OnConnectionException += async (_, _) => await ReconnectAfterConnectionException();

        _ircHandler.OnJoinedChannel += (_, e) => OnJoinedChannel?.Invoke(this, e);
        _ircHandler.OnLeftChannel += (_, e) => OnLeftChannel?.Invoke(this, e);
        _ircHandler.OnRoomstateReceived += IrcHandler_OnRoomstateReceived;
        _ircHandler.OnChatMessageReceived += IrcHandler_OnChatMessageReceived;
        _ircHandler.OnPingReceived += async (_, e) => await IrcHandler_OnPingReceived(e);
        _ircHandler.OnReconnectReceived += async (_, _) => await _client.ReconnectAsync(_ircChannels.AsMemory());
    }

    /// <inheritdoc cref="SendAsync(ReadOnlyMemory{char},ReadOnlyMemory{char})"/>
    public async ValueTask SendAsync(string channel, string message)
    {
        await SendAsync(channel.AsMemory(), message.AsMemory());
    }

    /// <inheritdoc cref="SendAsync(ReadOnlyMemory{char},ReadOnlyMemory{char})"/>
    public async ValueTask SendAsync(string channel, ReadOnlyMemory<char> message)
    {
        await SendAsync(channel.AsMemory(), message);
    }

    /// <inheritdoc cref="SendAsync(ReadOnlyMemory{char},ReadOnlyMemory{char})"/>
    public async ValueTask SendAsync(ReadOnlyMemory<char> channel, string message)
    {
        await SendAsync(channel, message.AsMemory());
    }

    /// <summary>
    /// Asynchronously sends a chat message.
    /// </summary>
    /// <param name="channel">The username of the channel owner</param>
    /// <param name="message">The message that will be sent</param>
    public async ValueTask SendAsync(ReadOnlyMemory<char> channel, ReadOnlyMemory<char> message)
    {
        if (!IsConnected)
        {
            throw new ClientNotConnectedException();
        }

        if (IsAnonymousLogin)
        {
            throw new AnonymousClientException();
        }

        string prefixedChannel = (Channels[channel.Span]?._prefixedName) ?? throw new NotConnectedToTheChannelException(new string(channel.Span));
        await _client.SendMessageAsync(prefixedChannel.AsMemory(), message);
    }

    /// <inheritdoc cref="SendAsync(long,System.ReadOnlyMemory{char})"/>
    public async ValueTask SendAsync(long channelId, string message)
    {
        await SendAsync(channelId, message.AsMemory());
    }

    /// <summary>
    /// Asynchronously sends a chat message.
    /// </summary>
    /// <param name="channelId">The user id of the channel owner</param>
    /// <param name="message">The message that will be sent</param>
    public async ValueTask SendAsync(long channelId, ReadOnlyMemory<char> message)
    {
        if (!IsConnected)
        {
            throw new ClientNotConnectedException();
        }

        if (IsAnonymousLogin)
        {
            throw new AnonymousClientException();
        }

        string prefixedChannel = (Channels[channelId]?._prefixedName) ?? throw new NotConnectedToTheChannelException(channelId);
        await _client.SendMessageAsync(prefixedChannel.AsMemory(), message);
    }

    /// <inheritdoc cref="SendRawAsync(System.ReadOnlyMemory{char})"/>
    public async ValueTask SendRawAsync(string rawMessage)
    {
        await SendRawAsync(rawMessage.AsMemory());
    }

    /// <summary>
    /// Asynchronously sends a raw message to the chat server.
    /// </summary>
    /// <param name="rawMessage">The raw message</param>
    public async ValueTask SendRawAsync(ReadOnlyMemory<char> rawMessage)
    {
        if (!IsConnected)
        {
            throw new ClientNotConnectedException();
        }

        await _client.SendRawAsync(rawMessage);
    }

    /// <summary>
    /// Asynchronously connects the client to the chat server. This method will be exited after the client has joined all channels.
    /// </summary>
    public async ValueTask ConnectAsync()
    {
        if (IsConnected)
        {
            return;
        }

        await ConnectAsync(_ircChannels.AsMemory());
    }

    private async ValueTask ConnectAsync(ReadOnlyMemory<string> ircChannels)
    {
        await _client.ConnectAsync(ircChannels);
    }

    /// <inheritdoc cref="JoinChannelsAsync(ReadOnlyMemory{string})"/>
    public async ValueTask JoinChannelsAsync(IEnumerable<string> channels)
    {
        if (channels.TryGetReadOnlyMemory<string>(out ReadOnlyMemory<string> channelsMemory))
        {
            await JoinChannelsAsync(channelsMemory);
        }

        await JoinChannelsAsync(channels.ToArray());
    }

    /// <inheritdoc cref="JoinChannelsAsync(ReadOnlyMemory{string})"/>
    public async ValueTask JoinChannelsAsync(List<string> channels)
    {
        await JoinChannelsAsync(CollectionsMarshal.AsSpan(channels).AsMemoryDangerous());
    }

    /// <inheritdoc cref="JoinChannelsAsync(ReadOnlyMemory{string})"/>
    public async ValueTask JoinChannelsAsync(params string[] channels)
    {
        await JoinChannelsAsync(channels.AsMemory());
    }

    /// <summary>
    /// If the client is not connected, adds the channels to the channel list, otherwise asynchronously connects the client to the channels.
    /// </summary>
    /// <param name="channels">The channels</param>
    /// // <exception cref="FormatException">Throws a <see cref="FormatException"/> if any of <paramref name="channels"/> is in the wrong format.</exception>
    public async ValueTask JoinChannelsAsync(ReadOnlyMemory<string> channels)
    {
        for (int i = 0; i < channels.Length; i++)
        {
            await JoinChannelAsync(channels.Span[i]);
        }
    }

    /// <inheritdoc cref="JoinChannelAsync(ReadOnlyMemory{char})"/>
    public async ValueTask JoinChannelAsync(string channel)
    {
        await JoinChannelAsync(channel.AsMemory());
    }

    /// <summary>
    /// If the client is not connected, adds the channel to the channel list, otherwise asynchronously connects the client to the channel.
    /// </summary>
    /// <param name="channel">The channel</param>
    /// <exception cref="FormatException">Throws a <see cref="FormatException"/> if the <paramref name="channel"/> is in the wrong format.</exception>
    public async ValueTask JoinChannelAsync(ReadOnlyMemory<char> channel)
    {
        string channelString = FormatChannel(channel.Span);
        _ircChannels.Add(channelString);
        if (IsConnected)
        {
            await _client.JoinChannelAsync(channelString.AsMemory());
        }
    }

    /// <inheritdoc cref="LeaveChannelAsync(ReadOnlyMemory{char})"/>
    public async ValueTask LeaveChannelAsync(string channel)
    {
        await LeaveChannelAsync(channel.AsMemory());
    }

    /// <summary>
    /// If the client is not connected, removes the channel from the channel list, otherwise asynchronously leaves the channel.
    /// </summary>
    /// <param name="channel">The channel</param>
    public async ValueTask LeaveChannelAsync(ReadOnlyMemory<char> channel)
    {
        if (_ircChannels.Count == 0)
        {
            return;
        }

        string channelString = FormatChannel(channel.Span);
        _ircChannels.Remove(channelString);
        Channels.Remove(channel.Span);
        if (IsConnected)
        {
            await _client.LeaveChannelAsync(channel);
        }
    }

    /// <inheritdoc cref="LeaveChannelsAsync(ReadOnlyMemory{string})"/>
    public async ValueTask LeaveChannelsAsync(IEnumerable<string> channels)
    {
        if (channels.TryGetReadOnlyMemory<string>(out ReadOnlyMemory<string> channelsMemory))
        {
            await LeaveChannelsAsync(channelsMemory);
        }

        await LeaveChannelsAsync(channels.ToArray());
    }

    /// <inheritdoc cref="LeaveChannelsAsync(ReadOnlyMemory{string})"/>
    public async ValueTask LeaveChannelsAsync(List<string> channels)
    {
        await LeaveChannelsAsync(CollectionsMarshal.AsSpan(channels).AsMemoryDangerous());
    }

    /// <inheritdoc cref="LeaveChannelsAsync(ReadOnlyMemory{string})"/>
    public async ValueTask LeaveChannelsAsync(params string[] channels)
    {
        await LeaveChannelsAsync(channels.AsMemory());
    }

    /// <summary>
    /// If the client is not connected, removes the channels from the channel list, otherwise leaves the channels.
    /// </summary>
    /// <param name="channels">The channels</param>
    public async ValueTask LeaveChannelsAsync(ReadOnlyMemory<string> channels)
    {
        for (int i = 0; i < channels.Length; i++)
        {
            await LeaveChannelAsync(channels.Span[i]);
        }
    }

    /// <summary>
    /// If the client is not connected, clears the channel list, otherwise also asynchronously leaves all channels.
    /// </summary>
    public async ValueTask LeaveChannelsAsync()
    {
        if (IsConnected)
        {
            await LeaveChannelsAsync(_ircChannels.AsMemory());
        }

        Channels.Clear();
        _ircChannels.Clear();
    }

    /// <summary>
    /// Asynchronously disconnects the client from the chat server.
    /// </summary>
    public async ValueTask DisconnectAsync()
    {
        if (!IsConnected)
        {
            return;
        }

        await _client.DisconnectAsync();
        Channels.Clear();
    }

    private void IrcClient_OnDataReceived(object? sender, ReceivedData data)
    {
        _ircHandler.Handle(data.Span);
        if (OnDataReceived is null)
        {
            data.Dispose();
            return;
        }

        OnDataReceived.Invoke(this, data);
    }

    private void IrcHandler_OnChatMessageReceived(object? sender, ChatMessage msg)
    {
        OnChatMessageReceived?.Invoke(this, msg);
    }

    private void IrcHandler_OnRoomstateReceived(object? sender, RoomstateArgs roomstateArgs)
    {
        Channels.Update(in roomstateArgs);
        OnRoomstateReceived?.Invoke(this, roomstateArgs);
    }

    private async ValueTask IrcHandler_OnPingReceived(ReceivedData data)
    {
        try
        {
            using PoolBufferStringBuilder builder = new(50);
            builder.Append(_pongPrefix, data.Span);
            await SendRawAsync(builder.WrittenMemory);
        }
        finally
        {
            data.Dispose();
        }
    }

    private async Task ReconnectAfterConnectionException()
    {
        await _reconnectionLock.WaitAsync();

        try
        {
            if (_client.IsConnected || _client.IsConnecting)
            {
                return;
            }

            _client.CancelTasks();
            await Task.Delay(TimeSpan.FromSeconds(10));
            await _client.ConnectAsync(_ircChannels.AsMemory());
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
        finally
        {
            _reconnectionLock.Release();
        }
    }

    private static string FormatChannel(ReadOnlySpan<char> channel, bool withHashtag = true)
    {
        Span<char> result = stackalloc char[channel.Length + 1];
        int length = FormatChannel(channel, withHashtag, result);
        return new(result[..length]);
    }

    private static int FormatChannel(ReadOnlySpan<char> channel, bool withHashtag, Span<char> result)
    {
        if (!_channelPattern.IsMatch(channel))
        {
            throw new FormatException($"The channel name (\"{channel}\") is in an invalid format.");
        }

        if (withHashtag)
        {
            if (channel[0] == _channelPrefix)
            {
                channel.ToLowerInvariant(result);
                return channel.Length;
            }

            result[0] = _channelPrefix;
            channel.ToLowerInvariant(result[1..]);
            return channel.Length + 1;
        }

        if (channel[0] == _channelPrefix)
        {
            channel[1..].ToLowerInvariant(result);
            return channel.Length - 1;
        }

        channel.ToLowerInvariant(result);
        return channel.Length;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _client.Dispose();
        _ircChannels.Dispose();
    }

    public bool Equals(TwitchClient? other)
    {
        return ReferenceEquals(this, other);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj);
    }

    public override int GetHashCode()
    {
        return MemoryHelper.GetRawDataPointer(this).GetHashCode();
    }
}
