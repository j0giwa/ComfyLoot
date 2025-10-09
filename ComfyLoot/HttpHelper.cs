using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ComfyLoot; 

/// <summary>
/// Generic Http request helper
/// </summary>
/* WARN: Likely AI generated, but has no obvious deficicenfys */
public static class HttpHelper {

	private static readonly HttpClient _client = new HttpClient();

	/// <summary>
	/// Creates a HttpRequest
	/// </summary>
	/// <param name="method">request method</param>
	/// <param name="uri">request uri</param>
	/// <param name="apiKey">api key (if needed)</param>
	/// <param name="bearerToken">jwt token (if needed)</param>
	/// <param name="customHeaders">additional headers (if needed)</param>
	/// <param name="content">requestbody (if needed)</param>
	/// <returns>HttpRequestMessage</returns>
	private static HttpRequestMessage
	CreateRequest(
		HttpMethod method,
		string uri,
		string? apiKey = null,
		string? bearerToken = null,
		Dictionary<string, string>? customHeaders = null,
		HttpContent? content = null)
	{
		AuthenticationHeaderValue authHeader;
		HttpRequestMessage request;

		request = new HttpRequestMessage(method, uri);

		if (!string.IsNullOrWhiteSpace(apiKey))
			request.Headers.Add("api-key", apiKey);

		if (!string.IsNullOrWhiteSpace(bearerToken)){
			authHeader = new AuthenticationHeaderValue("Bearer",
				bearerToken);
			request.Headers.Authorization = authHeader;
		}

		if (customHeaders != null)
			foreach (KeyValuePair<string,string> kvp in customHeaders)
				request.Headers.TryAddWithoutValidation(kvp.Key, kvp.Value);

		if (content != null)
			request.Content = content;

		return request;
	}

	/// <summary>
	/// Sends a http DELETE request
	/// </summary>
	/// <typeparam name="TResponse">Desired response type</typeparam>
	/// <param name="uri">destination uri</param>
	/// <param name="apiKey">api key, if applicable</param>
	/// <param name="bearerToken">access token, if applicable</param>
	/// <param name="customHeaders">headers</param>
	/// <returns>Api response</returns>	
	public static async Task<TResponse?>
	DeleteAsync<TResponse>(
		string uri,
		string? apiKey = null,
		string? bearerToken = null,
		Dictionary<string, string>? customHeaders = null)
	{
		string result;
		HttpRequestMessage request;
		HttpResponseMessage response;

		request = CreateRequest(HttpMethod.Delete,
			uri,
			apiKey,
			bearerToken,
			customHeaders);
		response = await _client.SendAsync(request);
		response.EnsureSuccessStatusCode();

		result = await response.Content.ReadAsStringAsync();
		return JsonConvert.DeserializeObject<TResponse>(result);
	}

	/// <summary>
	/// Sends a http GET request
	/// </summary>
	/// <typeparam name="TResponse">Desired response type</typeparam>
	/// <param name="uri">destination uri</param>
	/// <param name="apiKey">api key, if applicable</param>
	/// <param name="bearerToken">access token, if applicable</param>
	/// <param name="customHeaders">headers</param>
	/// <returns>Api response</returns>
	public static async Task<TResponse?>
	GetAsync<TResponse>(
		string uri,
		string? apiKey = null,
		string? bearerToken = null,
		Dictionary<string, string>? customHeaders = null)
	{
		string result;
		HttpRequestMessage request;
		HttpResponseMessage response;

		request = CreateRequest(HttpMethod.Get,
			uri,
			apiKey,
			bearerToken,
			customHeaders);
		response = await _client.SendAsync(request);
		response.EnsureSuccessStatusCode();

		result = await response.Content.ReadAsStringAsync();
		return JsonConvert.DeserializeObject<TResponse>(result);
	}

	/// <summary>
	/// Sends a HTTP POST request
	/// </summary>
	/// <typeparam name="TResponse">Desired response type</typeparam>
	/// <param name="uri">destination uri</param>
	/// <param name="payload">request payload</param>
	/// <param name="apiKey">api key, if applicable</param>
	/// <param name="bearerToken">access token, if applicable</param>
	/// <param name="customHeaders">headers</param>
	/// <returns>Api response</returns>
	public static async Task<TResponse?>
	PostAsync<TResponse>(
		string uri,
		object? payload = null,
		string? apiKey = null,
		string? bearerToken = null,
		Dictionary<string, string>? customHeaders = null)
	{
		string json;
		string result;
		object? jsonPayload;
		StringContent content;
		HttpRequestMessage request;
		HttpResponseMessage response;

		jsonPayload = payload;
		if (jsonPayload == null)
			jsonPayload = new { };
		json = JsonConvert.SerializeObject(jsonPayload);
		content = new StringContent(json,
			Encoding.UTF8,
			"application/json");

		request = CreateRequest(HttpMethod.Post,
			uri,
			apiKey,
			bearerToken,
			customHeaders,
			content);
		response = await _client.SendAsync(request);
		response.EnsureSuccessStatusCode();

		result = await response.Content.ReadAsStringAsync();
		return JsonConvert.DeserializeObject<TResponse>(result);
	}	
}
