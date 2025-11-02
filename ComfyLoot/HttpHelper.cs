/* See LICENSE file for copyright and license details. 
 *
 * WARN: 
 * The base of this class was transfered from another project.
 * It is likely that it AI generated, but i can neither confirm or deny this. 
 * There no obvious deficencys, so for the time beeing it's concidered "acceptable",
 * from an security perspective.
 */
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ComfyLoot; 

/// <summary>
/// Generic Http request helper
/// </summary>
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
}
