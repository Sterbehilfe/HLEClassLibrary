﻿using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace HLE.Http;

/// <summary>
/// A class that performs a Http GET request on creation of the object.
/// </summary>
public sealed class HttpGet
{
    /// <summary>
    /// The URL of the request.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// The complete answer as a string.
    /// </summary>
    public string? Result { get; private set; }

    /// <summary>
    /// The answer stored in a <see cref="JsonElement"/>, if the answer was a json compatible string.
    /// </summary>
    public JsonElement Data { get; }

    /// <summary>
    /// True, if the answer was a json compatible string, otherwise false.<br />
    /// If true, the JSON result has been stored in the property <see cref="Data"/>.
    /// </summary>
    public bool IsValidJsonData { get; } = true;

    private static readonly HttpClient _httpClient = new();

    /// <summary>
    /// The main constructor of <see cref="HttpGet"/>.<br />
    /// The request will be executed in the constructor.
    /// </summary>
    /// <param name="url">The URL to which the request will be sent to.</param>
    public HttpGet(string url)
    {
        Url = url;
        Task.Run(async () => Result = await GetRequest()).Wait();
        try
        {
            if (string.IsNullOrEmpty(Result))
            {
                throw new JsonException($"{nameof(Result)} is null or empty");
            }

            Data = JsonSerializer.Deserialize<JsonElement>(Result);
        }
        catch (JsonException)
        {
            IsValidJsonData = false;
        }
    }

    private async Task<string> GetRequest()
    {
        HttpResponseMessage response = await _httpClient.GetAsync(Url);
        return await response.Content.ReadAsStringAsync();
    }
}
