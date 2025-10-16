/* See LICENSE file for copyright and license details. */
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ComfyLoot.Data;

public class MarketBoardData {

	[JsonProperty("results")]
	public List<AggregatedResult>? Results { get; set; }

	[JsonProperty("failedItems")]
	public List<long>? FailedItems { get; set; }
}

public class AggregatedResult {

	[JsonProperty("itemId")]
	public int ItemId { get; set; }

	[JsonProperty("nq")]
	public QualityData NQ { get; set; }

	[JsonProperty("hq")]
	public QualityData HQ { get; set; }

	[JsonProperty("worldUploadTimes")]
	public List<WorldUploadTime> WorldUploadTimes { get; set; }
}

public class QualityData {

	[JsonProperty("minListing")]
	public Listing MinListing { get; set; }

	[JsonProperty("recentPurchase")]
	public Listing RecentPurchase { get; set; }

	[JsonProperty("averageSalePrice")]
	public Listing AverageSalePrice { get; set; }

	[JsonProperty("dailySaleVelocity")]
	public Listing DailySaleVelocity { get; set; }
}

public class Listing {

	[JsonProperty("world")]
	public ListingEntry World { get; set; }

	[JsonProperty("dc")]
	public ListingEntry Dc { get; set; }

	[JsonProperty("region")]
	public ListingEntry Region { get; set; }
}

public class ListingEntry {

	[JsonProperty("worldId")]
	public int? WorldId { get; set; }

	[JsonProperty("price")]
	public double Price { get; set; }
}

public class WorldUploadTime {
	
	[JsonProperty("worldId")]
	public int WorldId { get; set; }

	[JsonProperty("timestamp")]
	public long Timestamp { get; set; }
}