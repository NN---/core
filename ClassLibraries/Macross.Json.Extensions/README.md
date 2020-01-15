# Macross Software JSON Extensions

[![nuget](https://img.shields.io/nuget/v/Macross.Json.Extensions.svg)](https://www.nuget.org/packages/Macross.Json.Extensions/)

[Macross.Json.Extensions](https://www.nuget.org/packages/Macross.Json.Extensions/) is a .NET Standard 2.0+ library for augmenting what is provided out of the box by the System.Text.Json API.

Hopefully by .NET 5 this library will no longer be needed.

## Enumerations

[JsonStringEnumMemberConverter](./Code/JsonStringEnumMemberConverter.cs) is similar to the official [JsonStringEnumConverter](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.serialization.jsonstringenumconverter) but it adds two features and fixes one bug.

* [EnumMemberAttribute](https://docs.microsoft.com/en-us/dotnet/api/system.runtime.serialization.enummemberattribute) Support

	Blog: https://blog.macrosssoftware.com/index.php/2019/10/09/net-core-3-system-text-json-enummemberattribute-serialization/

	When serializing and deserializing an Enum as a string the value specified by `EnumMember` will be used.

	```csharp
	[JsonConverter(typeof(JsonStringEnumMemberConverter))]
	public enum DefinitionType
	{
		[EnumMember(Value = "UNKNOWN_DEFINITION_000")]
		DefinitionUnknown
	}

	[TestMethod]
	public void ExampleTest()
	{
		string Json = JsonSerializer.Serialize(DefinitionType.DefinitionUnknown);

		Assert.AreEqual("\"UNKNOWN_DEFINITION_000\"", Json);

		DefinitionType ParsedDefinitionType = JsonSerializer.Deserialize<DefinitionType>(Json);

		Assert.AreEqual(DefinitionType.DefinitionUnknown, ParsedDefinitionType);
	}
	```

* Nullable&lt;Enum&gt; Support

	If you try to use the built-in `JsonStringEnumConverter` with a nullable enum you will get an exception. This scenario is supported by `JsonStringEnumMemberConverter`.

	```csharp
	public class JsonObject
	{
		public int? Value { get; set; }

		[JsonConverter(typeof(JsonStringEnumMemberConverter))]
		public DayOfWeek? DayOfWeek { get; set; }
	}

	[TestMethod]
	public void ExampleTest()
	{
		string Json = JsonSerializer.Serialize(new JsonObject());

		Assert.AreEqual("{\"Value\":null,\"DayOfWeek\":null}", Json);

		JsonObject ParsedObject = JsonSerializer.Deserialize<JsonObject>(Json);

		Assert.IsFalse(ParsedObject.DayOfWeek.HasValue);
	}
	```

* Naming Policy Deserialization Support

	If a custom naming policy is used in conjunction with the built-in `JsonStringEnumConverter` during serialization, and it makes changes bigger than just casing, it won't deserialize back again. This is a rare bug that no one will probably care about, but it is fixed in `JsonStringEnumMemberConverter` nonetheless.

	```csharp
	private class CustomJsonNamingPolicy : JsonNamingPolicy
	{
		public override string ConvertName(string name) => $"_{name}";
	}

	[TestMethod]
	public void ExampleTest()
	{
		JsonSerializerOptions Options = new JsonSerializerOptions();

		Options.Converters.Add(new JsonStringEnumMemberConverter(new CustomJsonNamingPolicy()));

		string Json = JsonSerializer.Serialize(DayOfWeek.Friday, Options);

		Assert.AreEqual("\"_Friday\"", Json);

		DayOfWeek ParsedDayOfWeek = JsonSerializer.Deserialize<DayOfWeek>(Json, Options);

		Assert.AreEqual(DayOfWeek.Friday, ParsedDayOfWeek);
	}
	```