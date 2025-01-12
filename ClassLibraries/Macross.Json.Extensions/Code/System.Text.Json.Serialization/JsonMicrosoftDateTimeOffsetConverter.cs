﻿using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text.RegularExpressions;

using Macross.Json.Extensions;

namespace System.Text.Json.Serialization
{
	/// <summary>
	/// <see cref="JsonConverterFactory"/> to convert <see cref="DateTimeOffset"/> to and from strings in the Microsoft "\/Date()\/" format. Supports <see cref="Nullable{DateTimeOffset}"/>.
	/// </summary>
	/// <remarks>Adapted from code posted on: <a href="https://github.com/dotnet/runtime/issues/30776">dotnet/runtime #30776</a>.</remarks>
	public class JsonMicrosoftDateTimeOffsetConverter : JsonConverterFactory
	{
		/// <inheritdoc/>
		public override bool CanConvert(Type typeToConvert)
		{
			// Don't perform a typeToConvert == null check for performance. Trust our callers will be nice.
#pragma warning disable CA1062 // Validate arguments of public methods
			return typeToConvert == typeof(DateTimeOffset)
				|| (typeToConvert.IsGenericType && IsNullableDateTimeOffset(typeToConvert));
#pragma warning restore CA1062 // Validate arguments of public methods
		}

		/// <inheritdoc/>
		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			// Don't perform a typeToConvert == null check for performance. Trust our callers will be nice.
#pragma warning disable CA1062 // Validate arguments of public methods
			return typeToConvert.IsGenericType
				? new JsonNullableDateTimeOffsetConverter()
				: new JsonStandardDateTimeOffsetConverter();
#pragma warning restore CA1062 // Validate arguments of public methods
		}

		private static bool IsNullableDateTimeOffset(Type typeToConvert)
		{
			Type? UnderlyingType = Nullable.GetUnderlyingType(typeToConvert);

			return UnderlyingType != null && UnderlyingType == typeof(DateTimeOffset);
		}

		private class JsonStandardDateTimeOffsetConverter : JsonDateTimeOffsetConverter<DateTimeOffset>
		{
			/// <inheritdoc/>
			public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
				=> ReadDateTimeOffset(ref reader);

			/// <inheritdoc/>
			public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
				=> WriteDateTimeOffset(writer, value);
		}

		private class JsonNullableDateTimeOffsetConverter : JsonDateTimeOffsetConverter<DateTimeOffset?>
		{
			/// <inheritdoc/>
			public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
				=> ReadDateTimeOffset(ref reader);

			/// <inheritdoc/>
			public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
				=> WriteDateTimeOffset(writer, value!.Value);
		}

		private abstract class JsonDateTimeOffsetConverter<T> : JsonConverter<T>
		{
			private static readonly DateTimeOffset s_Epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
			private static readonly Regex s_Regex = new Regex(@"^\\?/Date\((-?\d+)([+-])(\d{2})(\d{2})\)\\?/$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

			public static DateTimeOffset ReadDateTimeOffset(ref Utf8JsonReader reader)
			{
				if (reader.TokenType != JsonTokenType.String)
					throw ThrowHelper.GenerateJsonException_DeserializeUnableToConvertValue(typeof(DateTimeOffset));

				string formatted = reader.GetString()!;
				Match match = s_Regex.Match(formatted);

				if (
						!match.Success
						|| !long.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long unixTime)
						|| !int.TryParse(match.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hours)
						|| !int.TryParse(match.Groups[4].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int minutes))
				{
					throw ThrowHelper.GenerateJsonException_DeserializeUnableToConvertValue(typeof(DateTimeOffset), formatted);
				}

				int sign = match.Groups[2].Value[0] == '+' ? 1 : -1;
				TimeSpan utcOffset = TimeSpan.FromMinutes((sign * hours * 60) + minutes);

				return s_Epoch.AddMilliseconds(unixTime).ToOffset(utcOffset);
			}

			public static void WriteDateTimeOffset(Utf8JsonWriter writer, DateTimeOffset value)
			{
				long unixTime = Convert.ToInt64((value - s_Epoch).TotalMilliseconds);
				TimeSpan utcOffset = value.Offset;

				int stackSize = 64;
				while (true)
				{
					Span<byte> span = stackSize <= 1024 ? stackalloc byte[stackSize] : new byte[stackSize];

					if (!Utf8Formatter.TryFormat(unixTime, span.Slice(7), out int bytesWritten, new StandardFormat('D'))
						|| stackSize < 15 + bytesWritten)
					{
						stackSize *= 2;
						continue;
					}

					JsonMicrosoftDateTimeConverter.Start.CopyTo(span);
					span[7 + bytesWritten] = utcOffset >= TimeSpan.Zero ? (byte)0x2B : (byte)0x2D;

					int hours = Math.Abs(utcOffset.Hours);
					if (hours < 10)
					{
						span[7 + bytesWritten + 1] = 0x30;
						span[7 + bytesWritten + 2] = (byte)(0x30 + hours);
					}
					else
					{
						Utf8Formatter.TryFormat(hours, span.Slice(7 + bytesWritten + 1), out _, new StandardFormat('D'));
					}
					int minutes = Math.Abs(utcOffset.Minutes);
					if (minutes < 10)
					{
						span[7 + bytesWritten + 3] = 0x30;
						span[7 + bytesWritten + 4] = (byte)(0x30 + minutes);
					}
					else
					{
						Utf8Formatter.TryFormat(minutes, span.Slice(7 + bytesWritten + 3), out _, new StandardFormat('D'));
					}
					JsonMicrosoftDateTimeConverter.End.CopyTo(span.Slice(7 + bytesWritten + 5));

					writer.WriteStringValue(
						JsonMicrosoftDateTimeConverter.CreateJsonEncodedTextFunc(span.Slice(0, 15 + bytesWritten).ToArray()));
					break;
				}
			}
		}
	}
}