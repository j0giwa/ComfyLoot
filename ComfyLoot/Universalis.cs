/* See LICENSE file for copyright and license details. */
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

using ComfyLoot.Models;

namespace ComfyLoot; 

/// <summary>
/// Deals with universalis.app api
/// </summary>
public static class Universalis {

	/// <summary>
	/// Extracts Itemvalue from Unveralis response
	/// </summary>
	/// <param name="data">Universalis response</param>
	/// <param name="hq">HQ item or not</param>
	/// <returns>Itemvalue in gil</returns>
	private static int
	ExtractValue(MarketBoardData data, bool hq)
	{
		double price;
		AggregatedResult? result;
		QualityData? qualityData;

		if (data == null
		|| data.Results == null
		|| data.Results.Count == 0)
			return 0;

		result = data.Results[0];
		if (result == null)
			return 0;

		if (hq)
			qualityData = result.HQ;
		else
			qualityData = result.NQ;

		if (qualityData == null)
			return 0;

		price = 0;
		if (qualityData.MinListing != null
		&& qualityData.MinListing.World != null)
			price = qualityData.MinListing.World.Price;

		ComfyLoot.Log.Debug(
			"[Universalis] ItemId: {itemId} Value: {price}",
			result.ItemId,
			price);

		return (int)price;
	}

	/// <summary>
	/// Fetches itmes marketboard value from universalis
	/// </summary>
	/// <param name="itemId">Item identifier</param>
	/// <param name="worldname">World to fetch marketboarddata</param>
	/// <param name="hq">high quality or no</param>
	/// <returns>The items markerboardvalue, will return 0 on errors or invalid data</returns>
	public static async Task<int>
	GetValue(uint itemId, string worldname, bool hq)
	{
		const string endpoint = "https://universalis.app/api/v2";

		string uri;
		HttpRequestMessage request;
		HttpResponseMessage response;
		string result;
		MarketBoardData? data;

		ComfyLoot.Log.Verbose(
			"[Universalis] Attemting to get data for ItemId: {itemId} ({wordname})",
			itemId,
			worldname);

		uri = $"{endpoint}/aggregated/{worldname}/{itemId}";
		using (HttpClient client = new HttpClient()) {
			request = new HttpRequestMessage(HttpMethod.Get, uri);
			response = await client.SendAsync(request);

			if (!response.IsSuccessStatusCode) {
				ComfyLoot.Log.Error(
				"[Universalis] Cannot recieve data for ItemId: {itemId}.",
				itemId);
			}

			result = await response.Content.ReadAsStringAsync();
			data = JsonConvert.DeserializeObject<MarketBoardData>(result);
		}

		if (data == null
		|| data.Results == null
		|| data.Results.Count == 0) {
			ComfyLoot.Log.Error("[Universalis] Failed to retrieve data: Invalid response");
			return 0;
		}

		return ExtractValue(data, hq);
	}
}
