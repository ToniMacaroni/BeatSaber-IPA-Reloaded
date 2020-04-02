﻿using IPA.Config.Data;
using IPA.Config.Stores.Attributes;
using IPA.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Boolean = IPA.Config.Data.Boolean;

namespace IPA.Config.Stores.Converters
{
    /// <summary>
    /// Provides utility functions for custom converters.
    /// </summary>
    public static class Converter 
    {
        /// <summary>
        /// Gets the integral value of a <see cref="Value"/>, coercing a <see cref="FloatingPoint"/> if necessary,
        /// or <see langword="null"/> if <paramref name="val"/> is not an <see cref="Integer"/> or <see cref="FloatingPoint"/>.
        /// </summary>
        /// <param name="val">the <see cref="Value"/> to get the integral value of</param>
        /// <returns>the integral value of <paramref name="val"/>, or <see langword="null"/></returns>
        public static long? IntValue(Value val)
            => val is Integer inte ? inte.Value :
               val is FloatingPoint fp ? fp.AsInteger()?.Value :
               null;
        /// <summary>
        /// Gets the floaing point value of a <see cref="Value"/>, coercing an <see cref="Integer"/> if necessary,
        /// or <see langword="null"/> if <paramref name="val"/> is not an <see cref="Integer"/> or <see cref="FloatingPoint"/>.
        /// </summary>
        /// <param name="val">the <see cref="Value"/> to get the floaing point value of</param>
        /// <returns>the floaing point value of <paramref name="val"/>, or <see langword="null"/></returns>
        public static decimal? FloatValue(Value val)
            => val is FloatingPoint fp ? fp.Value :
               val is Integer inte ? inte.AsFloat()?.Value :
               null;

        internal interface IValConv<T>
        {
            ValueConverter<T> Get();
        }
        internal class ValConv<T> : IValConv<T> where T : struct
        {
            private static readonly IValConv<T> Impl = ValConvImpls.Impl as IValConv<T> ?? new ValConv<T>();
            public ValueConverter<T> Get() => Impl.Get();
            ValueConverter<T> IValConv<T>.Get()
                => new CustomValueTypeConverter<T>();
        }
        private class ValConvImpls : IValConv<char>,
            IValConv<IntPtr>, IValConv<UIntPtr>,
            IValConv<long>, IValConv<ulong>,
            IValConv<int>, IValConv<uint>,
            IValConv<short>, IValConv<ushort>,
            IValConv<sbyte>, IValConv<byte>,
            IValConv<float>, IValConv<double>,
            IValConv<decimal>, IValConv<bool>
        {
            internal static readonly ValConvImpls Impl = new ValConvImpls();
            ValueConverter<char> IValConv<char>.Get() => new CharConverter();
            ValueConverter<long> IValConv<long>.Get() => new LongConverter();
            ValueConverter<ulong> IValConv<ulong>.Get() => new ULongConverter();
            ValueConverter<IntPtr> IValConv<IntPtr>.Get() => new IntPtrConverter();
            ValueConverter<UIntPtr> IValConv<UIntPtr>.Get() => new UIntPtrConverter();
            ValueConverter<int> IValConv<int>.Get() => new IntConverter();
            ValueConverter<uint> IValConv<uint>.Get() => new UIntConverter();
            ValueConverter<short> IValConv<short>.Get() => new ShortConverter();
            ValueConverter<ushort> IValConv<ushort>.Get() => new UShortConverter();
            ValueConverter<byte> IValConv<byte>.Get() => new ByteConverter();
            ValueConverter<sbyte> IValConv<sbyte>.Get() => new SByteConverter();
            ValueConverter<float> IValConv<float>.Get() => new FloatConverter();
            ValueConverter<double> IValConv<double>.Get() => new DoubleConverter();
            ValueConverter<decimal> IValConv<decimal>.Get() => new DecimalConverter();
            ValueConverter<bool> IValConv<bool>.Get() => new BooleanConverter();
        }
    }

    /// <summary>
    /// Provides generic utilities for converters for certain types.
    /// </summary>
    /// <typeparam name="T">the type of the <see cref="ValueConverter{T}"/> that this works on</typeparam>
    public static class Converter<T>
    {
        private static ValueConverter<T> defaultConverter = null;
        /// <summary>
        /// Gets the default <see cref="ValueConverter{T}"/> for the current type.
        /// </summary>
        public static ValueConverter<T> Default
            => defaultConverter ??= MakeDefault();

        private static ValueConverter<T> MakeDefault()
        {
            var t = typeof(T);
            //Logger.log.Debug($"Converter<{t}>.MakeDefault()");

            if (t.IsValueType)
            { // we have to do this garbo to make it accept the thing that we know is a value type at instantiation time
                if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
                { // this is a Nullable
                    //Logger.log.Debug($"gives NullableConverter<{Nullable.GetUnderlyingType(t)}>");
                    return Activator.CreateInstance(typeof(NullableConverter<>).MakeGenericType(Nullable.GetUnderlyingType(t))) as ValueConverter<T>;
                }

                //Logger.log.Debug($"gives converter for value type {t}");
                var valConv = Activator.CreateInstance(typeof(Converter.ValConv<>).MakeGenericType(t)) as Converter.IValConv<T>;
                return valConv.Get();
            }
            else if (t == typeof(string))
            {
                //Logger.log.Debug($"gives StringConverter");
                return new StringConverter() as ValueConverter<T>;
            }
            else
            {
                //Logger.log.Debug($"gives CustomObjectConverter<{t}>");
                return Activator.CreateInstance(typeof(CustomObjectConverter<>).MakeGenericType(t)) as ValueConverter<T>;
            }
        }
    }

    /// <summary>
    /// A converter for a <see cref="Nullable{T}"/>.
    /// </summary>
    /// <typeparam name="T">the underlying type of the <see cref="Nullable{T}"/></typeparam>
    public class NullableConverter<T> : ValueConverter<T?> where T : struct
    {
        private readonly ValueConverter<T> baseConverter;
        /// <summary>
        /// Creates a converter with the default converter for the base type.
        /// Equivalent to 
        /// <code>
        /// new NullableConverter(Converter&lt;T&gt;.Default)
        /// </code>
        /// </summary>
        /// <seealso cref="NullableConverter{T}.NullableConverter(ValueConverter{T})"/>
        /// <seealso cref="Converter{T}.Default"/>
        public NullableConverter() : this(Converter<T>.Default) { }
        /// <summary>
        /// Creates a converter with the given underlying <see cref="ValueConverter{T}"/>.
        /// </summary>
        /// <param name="underlying">the undlerlying <see cref="ValueConverter{T}"/> to use</param>
        public NullableConverter(ValueConverter<T> underlying)
            => baseConverter = underlying;
        /// <summary>
        /// Converts a <see cref="Value"/> tree to a value.
        /// </summary>
        /// <param name="value">the <see cref="Value"/> tree to convert</param>
        /// <param name="parent">the object which will own the created object</param>
        /// <returns>the object represented by <paramref name="value"/></returns>
        public override T? FromValue(Value value, object parent)
            => value == null ? null : new T?(baseConverter.FromValue(value, parent));
        /// <summary>
        /// Converts a nullable <typeparamref name="T"/> to a <see cref="Value"/> tree.
        /// </summary>
        /// <param name="obj">the value to serialize</param>
        /// <param name="parent">the object which owns <paramref name="obj"/></param>
        /// <returns>a <see cref="Value"/> tree representing <paramref name="obj"/>.</returns>
        public override Value ToValue(T? obj, object parent)
            => obj == null ? null : baseConverter.ToValue(obj.Value, parent);
    }

    /// <summary>
    /// A converter for a <see cref="Nullable{T}"/> that default-constructs a converter of type <typeparamref name="TConverter"/>
    /// to use as the underlying converter. Use this in the <see cref="UseConverterAttribute"/>.
    /// </summary>
    /// <typeparam name="T">the underlying type of the <see cref="Nullable{T}"/></typeparam>
    /// <typeparam name="TConverter">the type to use as an underlying converter</typeparam>
    /// <seealso cref="NullableConverter{T}"/>
    public sealed class NullableConverter<T, TConverter> : NullableConverter<T> 
        where T : struct 
        where TConverter : ValueConverter<T>, new()
    {
        /// <summary>
        /// Creates a converter with a new <typeparamref name="TConverter"/> as the underlying converter.
        /// </summary>
        /// <seealso cref="NullableConverter{T}.NullableConverter(ValueConverter{T})"/>
        public NullableConverter() : base(new TConverter()) { }
    }

    /// <summary>
    /// A converter for an enum of type <typeparamref name="T"/>, that converts the enum to its string representation and back.
    /// </summary>
    /// <typeparam name="T">the enum type</typeparam>
    public sealed class EnumConverter<T> : ValueConverter<T>
        where T : Enum
    {
        /// <summary>
        /// Converts a <see cref="Value"/> that is a <see cref="Text"/> node to the corresponding enum value.
        /// </summary>
        /// <param name="value">the <see cref="Value"/> to convert</param>
        /// <param name="parent">the object which will own the created object</param>
        /// <returns>the deserialized enum value</returns>
        /// <exception cref="ArgumentException">if <paramref name="value"/> is not a <see cref="Text"/> node</exception>
        public override T FromValue(Value value, object parent)
            => value is Text t
                ? (T)Enum.Parse(typeof(T), t.Value)
                : throw new ArgumentException("Value not a string", nameof(value));

        /// <summary>
        /// Converts an enum of type <typeparamref name="T"/> to a <see cref="Value"/> node corresponding to its value.
        /// </summary>
        /// <param name="obj">the value to serialize</param>
        /// <param name="parent">the object which owns <paramref name="obj"/></param>
        /// <returns>a <see cref="Text"/> node representing <paramref name="obj"/></returns>
        public override Value ToValue(T obj, object parent)
            => Value.Text(obj.ToString());
    }

    /// <summary>
    /// A converter for an enum of type <typeparamref name="T"/>, that converts the enum to its string representation and back,
    /// ignoring the case of the serialized value for deseiralization.
    /// </summary>
    /// <typeparam name="T">the enum type</typeparam>
    public sealed class CaseInsensitiveEnumConverter<T> : ValueConverter<T>
        where T : Enum
    {
        /// <summary>
        /// Converts a <see cref="Value"/> that is a <see cref="Text"/> node to the corresponding enum value.
        /// </summary>
        /// <param name="value">the <see cref="Value"/> to convert</param>
        /// <param name="parent">the object which will own the created object</param>
        /// <returns>the deserialized enum value</returns>
        /// <exception cref="ArgumentException">if <paramref name="value"/> is not a <see cref="Text"/> node</exception>
        public override T FromValue(Value value, object parent)
            => value is Text t
                ? (T)Enum.Parse(typeof(T), t.Value, true)
                : throw new ArgumentException("Value not a string", nameof(value));

        /// <summary>
        /// Converts an enum of type <typeparamref name="T"/> to a <see cref="Value"/> node corresponding to its value.
        /// </summary>
        /// <param name="obj">the value to serialize</param>
        /// <param name="parent">the object which owns <paramref name="obj"/></param>
        /// <returns>a <see cref="Text"/> node representing <paramref name="obj"/></returns>
        public override Value ToValue(T obj, object parent)
            => Value.Text(obj.ToString());
    }

    /// <summary>
    /// A converter for an enum of type <typeparamref name="T"/>, that converts the enum to its underlying value for serialization.
    /// </summary>
    /// <typeparam name="T">the enum type</typeparam>
    public sealed class NumericEnumConverter<T> : ValueConverter<T>
        where T : Enum
    {
        /// <summary>
        /// Converts a <see cref="Value"/> that is a numeric node to the corresponding enum value.
        /// </summary>
        /// <param name="value">the <see cref="Value"/> to convert</param>
        /// <param name="parent">the object which will own the created object</param>
        /// <returns>the deserialized enum value</returns>
        /// <exception cref="ArgumentException">if <paramref name="value"/> is not a numeric node</exception>
        public override T FromValue(Value value, object parent)
            => (T)Enum.ToObject(typeof(T), Converter.IntValue(value) 
                    ?? throw new ArgumentException("Value not a numeric node", nameof(value)));

        /// <summary>
        /// Converts an enum of type <typeparamref name="T"/> to a <see cref="Value"/> node corresponding to its value.
        /// </summary>
        /// <param name="obj">the value to serialize</param>
        /// <param name="parent">the object which owns <paramref name="obj"/></param>
        /// <returns>an <see cref="Integer"/> node representing <paramref name="obj"/></returns>
        public override Value ToValue(T obj, object parent)
            => Value.Integer(Convert.ToInt64(obj));
    }

    /// <summary>
    /// A converter for instances of <see cref="IDictionary{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TValue">the value type of the dictionary</typeparam>
    public class IDictionaryConverter<TValue> : ValueConverter<IDictionary<string, TValue>>
    {
        /// <summary>
        /// Gets the converter for the dictionary's value type.
        /// </summary>
        protected ValueConverter<TValue> BaseConverter { get; }

        /// <summary>
        /// Constructs an <see cref="IDictionaryConverter{TValue}"/> using the default converter for the value type.
        /// </summary>
        public IDictionaryConverter() : this(Converter<TValue>.Default) { }
        /// <summary>
        /// Constructs an <see cref="IDictionaryConverter{TValue}"/> using the specified converter for the value.
        /// </summary>
        /// <param name="converter">the converter for the value</param>
        public IDictionaryConverter(ValueConverter<TValue> converter)
            => BaseConverter = converter;

        /// <summary>
        /// Converts a <see cref="Map"/> to an <see cref="IDictionary{TKey, TValue}"/> that is represented by it.
        /// </summary>
        /// <param name="value">the <see cref="Map"/> to convert</param>
        /// <param name="parent">the parent that will own the resulting object</param>
        /// <returns>the deserialized dictionary</returns>
        public override IDictionary<string, TValue> FromValue(Value value, object parent)
            => (value as Map)?.Select(kvp => (kvp.Key, val: BaseConverter.FromValue(kvp.Value, parent)))
                    ?.ToDictionary(p => p.Key, p => p.val)
                ?? throw new ArgumentException("Value not a map", nameof(value));

        /// <summary>
        /// Serializes an <see cref="IDictionary{TKey, TValue}"/> into a <see cref="Map"/> containing its values.
        /// </summary>
        /// <param name="obj">the dictionary to serialize</param>
        /// <param name="parent">the object that owns the dictionary</param>
        /// <returns>the dictionary serialized as a <see cref="Map"/></returns>
        public override Value ToValue(IDictionary<string, TValue> obj, object parent)
            => Value.From(obj.Select(p => new KeyValuePair<string, Value>(p.Key, BaseConverter.ToValue(p.Value, parent))));
    }

    /// <summary>
    /// A converter for instances of <see cref="IDictionary{TKey, TValue}"/>, specifying a value converter as a type parameter.
    /// </summary>
    /// <typeparam name="TValue">the value type of the dictionary</typeparam>
    /// <typeparam name="TConverter">the converter type for values</typeparam>
    public sealed class IDictionaryConverter<TValue, TConverter> : IDictionaryConverter<TValue>
        where TConverter : ValueConverter<TValue>, new()
    {
        /// <summary>
        /// Constructs a new <see cref="IDictionaryConverter{TValue, TConverter}"/> with a new instance of 
        /// <typeparamref name="TConverter"/> as the value converter.
        /// </summary>
        public IDictionaryConverter() : base(new TConverter()) { }
    }


    /// <summary>
    /// A converter for instances of <see cref="Dictionary{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TValue">the value type of the dictionary</typeparam>
    public class DictionaryConverter<TValue> : ValueConverter<Dictionary<string, TValue>>
    {
        /// <summary>
        /// Gets the converter for the dictionary's value type.
        /// </summary>
        protected ValueConverter<TValue> BaseConverter { get; }

        /// <summary>
        /// Constructs an <see cref="IDictionaryConverter{TValue}"/> using the default converter for the value type.
        /// </summary>
        public DictionaryConverter() : this(Converter<TValue>.Default) { }
        /// <summary>
        /// Constructs an <see cref="IDictionaryConverter{TValue}"/> using the specified converter for the value.
        /// </summary>
        /// <param name="converter">the converter for the value</param>
        public DictionaryConverter(ValueConverter<TValue> converter)
            => BaseConverter = converter;

        /// <summary>
        /// Converts a <see cref="Map"/> to a <see cref="Dictionary{TKey, TValue}"/> that is represented by it.
        /// </summary>
        /// <param name="value">the <see cref="Map"/> to convert</param>
        /// <param name="parent">the parent that will own the resulting object</param>
        /// <returns>the deserialized dictionary</returns>
        public override Dictionary<string, TValue> FromValue(Value value, object parent)
            => (value as Map)?.Select(kvp => (kvp.Key, val: BaseConverter.FromValue(kvp.Value, parent)))
                    ?.ToDictionary(p => p.Key, p => p.val)
                ?? throw new ArgumentException("Value not a map", nameof(value));

        /// <summary>
        /// Serializes a <see cref="Dictionary{TKey, TValue}"/> into a <see cref="Map"/> containing its values.
        /// </summary>
        /// <param name="obj">the dictionary to serialize</param>
        /// <param name="parent">the object that owns the dictionary</param>
        /// <returns>the dictionary serialized as a <see cref="Map"/></returns>
        public override Value ToValue(Dictionary<string, TValue> obj, object parent)
            => Value.From(obj.Select(p => new KeyValuePair<string, Value>(p.Key, BaseConverter.ToValue(p.Value, parent))));
    }

    /// <summary>
    /// A converter for instances of <see cref="Dictionary{TKey, TValue}"/>, specifying a value converter as a type parameter.
    /// </summary>
    /// <typeparam name="TValue">the value type of the dictionary</typeparam>
    /// <typeparam name="TConverter">the converter type for values</typeparam>
    public sealed class DictionaryConverter<TValue, TConverter> : DictionaryConverter<TValue>
        where TConverter : ValueConverter<TValue>, new()
    {
        /// <summary>
        /// Constructs a new <see cref="IDictionaryConverter{TValue, TConverter}"/> with a new instance of 
        /// <typeparamref name="TConverter"/> as the value converter.
        /// </summary>
        public DictionaryConverter() : base(new TConverter()) { }
    }

#if NET4

    /// <summary>
    /// A converter for instances of <see cref="IReadOnlyDictionary{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TValue">the value type of the dictionary</typeparam>
    public class IReadOnlyDictionaryConverter<TValue> : ValueConverter<IReadOnlyDictionary<string, TValue>>
    {
        /// <summary>
        /// Gets the converter for the dictionary's value type.
        /// </summary>
        protected ValueConverter<TValue> BaseConverter { get; }

        /// <summary>
        /// Constructs an <see cref="IReadOnlyDictionaryConverter{TValue}"/> using the default converter for the value type.
        /// </summary>
        public IReadOnlyDictionaryConverter() : this(Converter<TValue>.Default) { }
        /// <summary>
        /// Constructs an <see cref="IReadOnlyDictionaryConverter{TValue}"/> using the specified converter for the value.
        /// </summary>
        /// <param name="converter">the converter for the value</param>
        public IReadOnlyDictionaryConverter(ValueConverter<TValue> converter)
            => BaseConverter = converter;

        /// <summary>
        /// Converts a <see cref="Map"/> to an <see cref="IDictionary{TKey, TValue}"/> that is represented by it.
        /// </summary>
        /// <param name="value">the <see cref="Map"/> to convert</param>
        /// <param name="parent">the parent that will own the resulting object</param>
        /// <returns>the deserialized dictionary</returns>
        public override IReadOnlyDictionary<string, TValue> FromValue(Value value, object parent)
            => (value as Map)?.Select(kvp => (kvp.Key, val: BaseConverter.FromValue(kvp.Value, parent)))
                    ?.ToDictionary(p => p.Key, p => p.val)
                ?? throw new ArgumentException("Value not a map", nameof(value));

        /// <summary>
        /// Serializes an <see cref="IDictionary{TKey, TValue}"/> into a <see cref="Map"/> containing its values.
        /// </summary>
        /// <param name="obj">the dictionary to serialize</param>
        /// <param name="parent">the object that owns the dictionary</param>
        /// <returns>the dictionary serialized as a <see cref="Map"/></returns>
        public override Value ToValue(IReadOnlyDictionary<string, TValue> obj, object parent)
            => Value.From(obj.Select(p => new KeyValuePair<string, Value>(p.Key, BaseConverter.ToValue(p.Value, parent))));
    }

    /// <summary>
    /// A converter for instances of <see cref="IReadOnlyDictionary{TKey, TValue}"/>, specifying a value converter as a type parameter.
    /// </summary>
    /// <typeparam name="TValue">the value type of the dictionary</typeparam>
    /// <typeparam name="TConverter">the converter type for values</typeparam>
    public sealed class IReadOnlyDictionaryConverter<TValue, TConverter> : IReadOnlyDictionaryConverter<TValue>
        where TConverter : ValueConverter<TValue>, new()
    {
        /// <summary>
        /// Constructs a new <see cref="IReadOnlyDictionaryConverter{TValue, TConverter}"/> with a new instance of 
        /// <typeparamref name="TConverter"/> as the value converter.
        /// </summary>
        public IReadOnlyDictionaryConverter() : base(new TConverter()) { }
    }
#endif

    internal class StringConverter : ValueConverter<string>
    {
        public override string FromValue(Value value, object parent)
            => (value as Text)?.Value;

        public override Value ToValue(string obj, object parent)
            => Value.From(obj);
    }

    internal class CharConverter : ValueConverter<char>
    {
        public override char FromValue(Value value, object parent)
            => (value as Text)?.Value[0] 
                ?? throw new ArgumentException("Value not a text node", nameof(value)); // can throw nullptr

        public override Value ToValue(char obj, object parent)
            => Value.From(char.ToString(obj));
    }

    internal class LongConverter : ValueConverter<long>
    {
        public override long FromValue(Value value, object parent)
            => Converter.IntValue(value) 
                ?? throw new ArgumentException("Value not a numeric value", nameof(value));

        public override Value ToValue(long obj, object parent)
            => Value.From(obj);
    }

    internal class ULongConverter : ValueConverter<ulong>
    {
        public override ulong FromValue(Value value, object parent)
            => (ulong)(Converter.FloatValue(value) 
                ?? throw new ArgumentException("Value not a numeric value", nameof(value)));

        public override Value ToValue(ulong obj, object parent)
            => Value.From(obj);
    }

    internal class IntPtrConverter : ValueConverter<IntPtr>
    {
        public override IntPtr FromValue(Value value, object parent)
            => (IntPtr)Converter<long>.Default.FromValue(value, parent);

        public override Value ToValue(IntPtr obj, object parent)
            => Value.From((long)obj);
    }

    internal class UIntPtrConverter : ValueConverter<UIntPtr>
    {
        public override UIntPtr FromValue(Value value, object parent)
            => (UIntPtr)Converter<ulong>.Default.FromValue(value, parent);

        public override Value ToValue(UIntPtr obj, object parent)
            => Value.From((decimal)obj);
    }

    internal class IntConverter : ValueConverter<int>
    {
        public override int FromValue(Value value, object parent)
            => (int)Converter<long>.Default.FromValue(value, parent);
        public override Value ToValue(int obj, object parent)
            => Value.From(obj);
    }

    internal class UIntConverter : ValueConverter<uint>
    {
        public override uint FromValue(Value value, object parent)
            => (uint)Converter<long>.Default.FromValue(value, parent);
        public override Value ToValue(uint obj, object parent)
            => Value.From(obj);
    }

    internal class ShortConverter : ValueConverter<short>
    {
        public override short FromValue(Value value, object parent)
            => (short)Converter<long>.Default.FromValue(value, parent);
        public override Value ToValue(short obj, object parent)
            => Value.From(obj);
    }

    internal class UShortConverter : ValueConverter<ushort>
    {
        public override ushort FromValue(Value value, object parent)
            => (ushort)Converter<long>.Default.FromValue(value, parent);
        public override Value ToValue(ushort obj, object parent)
            => Value.From(obj);
    }

    internal class ByteConverter : ValueConverter<byte>
    {
        public override byte FromValue(Value value, object parent)
            => (byte)Converter<long>.Default.FromValue(value, parent);
        public override Value ToValue(byte obj, object parent)
            => Value.From(obj);
    }

    internal class SByteConverter : ValueConverter<sbyte>
    {
        public override sbyte FromValue(Value value, object parent)
            => (sbyte)Converter<long>.Default.FromValue(value, parent);
        public override Value ToValue(sbyte obj, object parent)
            => Value.From(obj);
    }

    internal class DecimalConverter : ValueConverter<decimal>
    {
        public override decimal FromValue(Value value, object parent)
            => Converter.FloatValue(value) ?? throw new ArgumentException("Value not a numeric value", nameof(value));
        public override Value ToValue(decimal obj, object parent)
            => Value.From(obj);
    }

    internal class FloatConverter : ValueConverter<float>
    {
        public override float FromValue(Value value, object parent)
            => (float)Converter<decimal>.Default.FromValue(value, parent);
        public override Value ToValue(float obj, object parent)
            => Value.From((decimal)obj);
    }

    internal class DoubleConverter : ValueConverter<double>
    {
        public override double FromValue(Value value, object parent)
            => (double)Converter<decimal>.Default.FromValue(value, parent);
        public override Value ToValue(double obj, object parent)
            => Value.From((decimal)obj);
    }

    internal class BooleanConverter : ValueConverter<bool>
    {
        public override bool FromValue(Value value, object parent)
            => (value as Boolean)?.Value ?? throw new ArgumentException("Value not a Boolean", nameof(value));
        public override Value ToValue(bool obj, object parent)
            => Value.From(obj);
    }
}
