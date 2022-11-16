﻿using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace HLE.Twitch;

/// <summary>
/// Provides a Twitch IRC server connection via a <see cref="TcpClient"/>.
/// </summary>
public sealed class TcpIrcClient : IrcClient
{
    /// <summary>
    /// Indicates whether the client is connected or not.
    /// </summary>
    public override bool IsConnected => _tcpClient.Connected && !_token.IsCancellationRequested;

    private readonly TcpClient _tcpClient = new();
    private StreamReader? _reader;
    private StreamWriter? _writer;

    /// <summary>
    /// The default constructor of <see cref="TcpIrcClient"/>.
    /// </summary>
    /// <param name="username">The username of the client.</param>
    /// <param name="oAuthToken">The OAuth token of the client.</param>
    public TcpIrcClient(string username, string? oAuthToken = null) : base(username, oAuthToken)
    {
    }

    ~TcpIrcClient()
    {
        _reader?.Dispose();
        _writer?.Dispose();
        _tcpClient.Dispose();
    }

    private protected override async Task Send(string message)
    {
        if (_writer is null)
        {
            throw new ArgumentNullException(nameof(_writer));
        }

        char[] chars = message.ToCharArray();
        await _writer.WriteLineAsync(chars, _token);
        await _writer.FlushAsync();
        InvokeDataSent(this, message);
    }

    private protected override void StartListening()
    {
        async Task StartListeningLocal()
        {
            while (!_tokenSource.IsCancellationRequested && IsConnected)
            {
                Memory<char> buffer = new char[1024]; // is this enough?
                int count = await _reader.ReadAsync(buffer, _token);
                ReadOnlyMemory<char> message = buffer[..count];
                InvokeDataReceived(this, message);
            }
        }

        if (_reader is null)
        {
            throw new ArgumentNullException(nameof(_reader));
        }

        Task.Run(StartListeningLocal, _token);
    }

    private protected override async Task ConnectClient()
    {
        await _tcpClient.ConnectAsync(_url.Url, _url.Port, _token);
        Stream stream;
        if (UseSSL)
        {
            SslStream sslStream = new(_tcpClient.GetStream(), false);
            await sslStream.AuthenticateAsClientAsync(_url.Url);
            stream = sslStream;
        }
        else
        {
            stream = _tcpClient.GetStream();
        }

        _reader = new(stream);
        _writer = new(stream);
    }

    private protected override Task DisconnectClient(string closeMessage)
    {
        _tcpClient.Close();
        return Task.CompletedTask;
    }

    private protected override (string Url, int Port) GetUrl()
    {
        return UseSSL ? ("irc.chat.twitch.tv", 6697) : ("irc.chat.twitch.tv", 6667);
    }
}
