using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json; /* TODO: Remove Dependency */

namespace ComfyLoot; 

/// <summary>
/// Http request helper
/// </summary>
/* TODO: rewrite */
/* SMELL: vibecode */
/* WARN: AI generated, security risk */
[Obsolete("Most likely based on AI generated code")]
public static class HttpHelper {

	private static readonly HttpClient _client = new HttpClient();

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
	/// Sends a http POST request
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

	/// <summary>
	/// Creates a streamed post request
	/// </summary>
	/// <param name="uri"></param>
	/// <param name="payload"></param>
	/// <param name="apiKey"></param>
	/// <param name="bearerToken"></param>
	/// <param name="customHeaders"></param>
	/// <param name="onEventReceived"></param>
	/// <param name="cancel"></param>
	/// <returns></returns>
	/// <exception cref="OperationCanceledException"></exception>
	/* SMELL: Exceeds line lenght */
	public static async Task
	PostStreamAsync(
		string uri,
		object? payload = null,
		string? apiKey = null,
		string? bearerToken = null,
		Dictionary<string, string>? customHeaders = null,
		Func<string, string, Task>? onEventReceived = null,
		CancellationToken cancel = default)
	{
		string json;
		string data;
		string? line;
		string? curEvent;
		string temp;  /* temp buffer for substrings */
		object? jsonPayload;
		StringContent content;
		StringBuilder dataBuilder;
		Task<string?> curTask;
		Task completed;

		if (onEventReceived == null)
			return;

		jsonPayload = payload;
		if (jsonPayload == null)
			jsonPayload = new { };
		json = JsonConvert.SerializeObject(jsonPayload);
		content = new StringContent(json,
			Encoding.UTF8,
			"application/json");

		using HttpRequestMessage request = CreateRequest(
			HttpMethod.Post,
			uri,
			apiKey,
			bearerToken,
			customHeaders,
			content);

		using HttpResponseMessage response = await _client.SendAsync(
			request,
			HttpCompletionOption.ResponseHeadersRead,
			cancel);
		response.EnsureSuccessStatusCode();

		using StreamReader reader = new StreamReader(await response
			.Content
			.ReadAsStreamAsync(cancel));

		curEvent = null;
		dataBuilder = new StringBuilder();
		while (!reader.EndOfStream && !cancel.IsCancellationRequested) {

			curTask = reader.ReadLineAsync(cancel).AsTask();
			completed = await Task.WhenAny(curTask,
				Task.Delay(30000, cancel));

			if (curTask != completed)
				throw new OperationCanceledException(cancel);

			line = await curTask;
			if (string.IsNullOrWhiteSpace(line)) {
				if ((dataBuilder.Length > 0)
				&& (curEvent != null)) {
					data = dataBuilder
						.ToString()
						.TrimEnd('\n');
					if ((curEvent == "done")
					&& (data == "[DONE]"))
						break;

					await onEventReceived(curEvent, data);
				}
				curEvent = null;
				dataBuilder.Clear();
				continue;
			}

			if (line.StartsWith("event:")) {
				temp = line.Substring("event:".Length);
				curEvent = temp.Trim();
			} else if (line.StartsWith("data:")) {
				temp = line.Substring("data:".Length);
				dataBuilder.AppendLine(temp.Trim());
			}
		}
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
}
