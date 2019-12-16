<Query Kind="Program">
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <NuGetReference>RestSharp</NuGetReference>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Bson</Namespace>
  <Namespace>Newtonsoft.Json.Converters</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>Newtonsoft.Json.Schema</Namespace>
  <Namespace>Newtonsoft.Json.Serialization</Namespace>
  <Namespace>RestSharp</Namespace>
  <Namespace>RestSharp.Authenticators</Namespace>
  <Namespace>RestSharp.Authenticators.OAuth</Namespace>
  <Namespace>RestSharp.Deserializers</Namespace>
  <Namespace>RestSharp.Extensions</Namespace>
  <Namespace>RestSharp.Serialization</Namespace>
  <Namespace>RestSharp.Serialization.Json</Namespace>
  <Namespace>RestSharp.Serialization.Xml</Namespace>
  <Namespace>RestSharp.Serializers</Namespace>
  <Namespace>RestSharp.Validation</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

// https://developers.meethue.com/develop/get-started-2/
private const string hueUser = "aIKNGKPClUjBOD0hXlwlnPQXDu7fKNhKtavcUtCc";

private static IRestClient bridgeClient;

async Task Main()
{
	string bridgeIp = await GetHueBridgeIpAddressAsync().ConfigureAwait(false);
	string.Format("Hue bridge ip: {0}", bridgeIp).Dump();
	
	bridgeClient = new RestClient(string.Format("http://{0}/api/{1}", bridgeIp, hueUser));
	bridgeClient.UseSerializer(new JsonNetSerializer());
	//bridgeClient.Proxy = new System.Net.WebProxy("http://localhost:8888");
	
	var lights = await GetLightsAsync().ConfigureAwait(false);
	
	foreach (var light in lights)
	{
		(await light.TurnOffAsync().ConfigureAwait(false)).Dump();
	}
}

async Task<string> GetHueBridgeIpAddressAsync()
{
	IRestClient client = new RestClient("https://discovery.meethue.com");
	var response = await client.ExecuteTaskAsync<IEnumerable<HueMetadata>>(new RestRequest(Method.GET)).ConfigureAwait(false);
	
	if (response.Data == null || response.Data.Any() == false)
	{
		throw new Exception("Could not find a hue bridge on the network.");
	}
	
	return response.Data.First().InternalIpAddress;
}

async Task<IEnumerable<HueLight>> GetLightsAsync()
{
	var response = await bridgeClient.ExecuteTaskAsync(new RestRequest("lights", Method.GET)).ConfigureAwait(false);
	if (response.IsSuccessful == false)
	{
		response.Dump();
		throw new Exception("There was an error getting a collection of lights connected to the bridge.");
	}

	var jObject = JsonConvert.DeserializeObject<JObject>(response.Content);
	var lights = new List<HueLight>();
	foreach (JProperty light in (JToken)jObject)
	{
		lights.Add(new HueLight()
		{
			Index = light.Name
		});
	}
	
	string.Format("Found {0} lights", lights.Count).Dump();
	return lights;
}

class HueMetadata
{
	public string Id { get; set; }
	public string InternalIpAddress { get; set; }
}

class HueLight
{
	public string Index { get; set; }
	
	public Task<IRestResponse> TurnOffAsync()
		=> SetStateAsync(new HueLightState()
		{
			On = false
		});

	private Task<IRestResponse> SetStateAsync(HueLightState state)
	{
		var request = new RestRequest("lights/{index}/state", Method.PUT);
		request.AddUrlSegment("index", this.Index);
		request.AddJsonBody(state);   
		return bridgeClient.ExecuteTaskAsync(request);
	}
}

class HueLightState
{
	[JsonProperty("on")]
	public bool On { get; set; }
}

class JsonNetSerializer : IRestSerializer
{
	public string Serialize(object obj) =>
		JsonConvert.SerializeObject(obj);

	public string Serialize(Parameter parameter) =>
		JsonConvert.SerializeObject(parameter.Value);

	public T Deserialize<T>(IRestResponse response) =>
		JsonConvert.DeserializeObject<T>(response.Content);

	public string[] SupportedContentTypes { get; } =
	{
			"application/json", "text/json", "text/x-json", "text/javascript", "*+json"
		};

	public string ContentType { get; set; } = "application/json";

	public DataFormat DataFormat { get; } = DataFormat.Json;
}