using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Newtonsoft.Json.Converters;

/// <summary>
/// Compatibility implementation of the converter that shipped in Rust's
/// unsigned Newtonsoft.Json 8 assembly. Newer Newtonsoft.Json releases no
/// longer expose this type, but legacy Oxide extensions still reference it.
/// </summary>
public class HashSetConverter : JsonConverter
{
	public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
	{
	}

	public override object ReadJson(
		JsonReader reader,
		Type objectType,
		object existingValue,
		JsonSerializer serializer)
	{
		var replace = serializer.ObjectCreationHandling == ObjectCreationHandling.Replace;
		if (reader.TokenType == JsonToken.Null)
		{
			return replace ? null : existingValue;
		}

		var value = replace || existingValue == null
			? Activator.CreateInstance(objectType)
			: existingValue;
		var itemType = objectType.GetGenericArguments()[0];
		MethodInfo add = objectType.GetMethod("Add");
		var array = JArray.Load(reader);

		for (var i = 0; i < array.Count; i++)
		{
			var item = serializer.Deserialize(array[i].CreateReader(), itemType);
			add.Invoke(value, [item]);
		}

		return value;
	}

	public override bool CanConvert(Type objectType)
	{
		return objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(HashSet<>);
	}

	public override bool CanWrite => false;
}
