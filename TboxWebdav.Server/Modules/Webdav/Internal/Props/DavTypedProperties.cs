﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Http;
using TboxWebdav.Server.Modules.Webdav.Internal;
using TboxWebdav.Server.Modules.Webdav.Internal.Stores;

namespace TboxWebdav.Server.Modules.Webdav.Internal.Props
{
    /// <summary>
    /// Abstract base class representing a single DAV property with a specific
    /// CLR type.
    /// </summary>
    /// <remarks>
    /// A dedicated converter should be implemented to convert the property 
    /// value to/from an XML value. This class supports both synchronous and
    /// asynchronous accessor methods. To improve scalability, it is
    /// recommended to use the asynchronous methods for properties that require
    /// some time to get/set.
    /// </remarks>
    /// <typeparam name="TEntry">
    /// Store item or collection to which this DAV property applies.
    /// </typeparam>
    /// <typeparam name="TType">
    /// CLR type of the property.
    /// </typeparam>
    public abstract class DavTypedProperty<TEntry, TType> : DavProperty<TEntry> where TEntry : IStoreItem
    {
        /// <summary>
        /// Converter defining methods to convert property values from/to XML.
        /// </summary>
        public interface IConverter
        {
            /// <summary>
            /// Get the XML representation of the specified value.
            /// </summary>
            /// <param name="httpContext">
            /// Current HTTP context.
            /// </param>
            /// <param name="value">
            /// Value that needs to be converted to XML output.
            /// </param>
            /// <returns>
            /// The XML representation of the <paramref name="value"/>. The
            /// XML output should either be a <see cref="string"/> or
            /// an <see cref="XElement"/>.
            /// </returns>
            /// <remarks>
            /// The current HTTP context can be used to generate XML that is
            /// compatible with the requesting WebDAV client.
            /// </remarks>
            object ToXml(HttpContext httpContext, TType value);

            /// <summary>
            /// Get the typed value of the specified XML representation.
            /// </summary>
            /// <param name="httpContext">
            /// Current HTTP context.
            /// </param>
            /// <param name="value">
            /// The XML value that needs to be converted to the target
            /// type. This value is always a <see cref="string"/>
            /// or an <see cref="XElement"/>.
            /// </param>
            /// <returns>
            /// The typed value of the XML representation.
            /// </returns>
            /// <remarks>
            /// The current HTTP context can be used to generate XML that is
            /// compatible with the requesting WebDAV client.
            /// </remarks>
            TType FromXml(HttpContext httpContext, object value);
        }

        private Func<HttpContext, TEntry, TType> _getter;
        private Func<HttpContext, TEntry, TType, DavStatusCode> _setter;
        private Func<HttpContext, TEntry, Task<TType>> _getterAsync;
        private Func<HttpContext, TEntry, TType, Task<DavStatusCode>> _setterAsync;

        /// <summary>
        /// Converter to convert property values from/to XML for this type.
        /// </summary>
        /// <remarks>
        /// This property should be set from the derived typed property implementation.
        /// </remarks>
        public abstract IConverter Converter { get; }

        /// <summary>
        /// Synchronous getter to obtain the property value.
        /// </summary>
        public Func<HttpContext, TEntry, TType> Getter
        {
            get => _getter;
            set
            {
                _getter = value;
                base.GetterAsync = (c, s) =>
                {
                    var v = _getter(c, s);
                    return Task.FromResult(Converter != null ? Converter.ToXml(c, v) : v);
                };
            }
        }

        /// <summary>
        /// Synchronous setter to set the property value.
        /// </summary>
        public Func<HttpContext, TEntry, TType, DavStatusCode> Setter
        {
            get => _setter;
            set
            {
                _setter = value;
                base.SetterAsync = (c, s, v) =>
                {
                    var tv = Converter != null ? Converter.FromXml(c, v) : (TType)v;
                    return Task.FromResult(_setter(c, s, tv));
                };
            }
        }

        /// <summary>
        /// Asynchronous getter to obtain the property value.
        /// </summary>
        public new Func<HttpContext, TEntry, Task<TType>> GetterAsync
        {
            get => _getterAsync;
            set
            {
                _getterAsync = value;
                base.GetterAsync = async (c, s) =>
                {
                    var v = await _getterAsync(c, s).ConfigureAwait(false);
                    return Converter != null ? Converter.ToXml(c, v) : v;
                };
            }
        }

        /// <summary>
        /// Asynchronous setter to set the property value.
        /// </summary>
        public new Func<HttpContext, TEntry, TType, Task<DavStatusCode>> SetterAsync
        {
            get => _setterAsync;
            set
            {
                _setterAsync = value;
                base.SetterAsync = (c, s, v) =>
                {
                    var tv = Converter != null ? Converter.FromXml(c, v) : (TType)v;
                    return _setterAsync(c, s, tv);
                };
            }
        }
    }

    /// <summary>
    /// Abstract base class representing a single DAV property using an
    /// RFC1123 date type (mapped to <see cref="DateTime"/>).
    /// </summary>
    /// <typeparam name="TEntry">
    /// Store item or collection to which this DAV property applies.
    /// </typeparam>
    public abstract class DavRfc1123Date<TEntry> : DavTypedProperty<TEntry, DateTime> where TEntry : IStoreItem
    {
        private class Rfc1123DateConverter : IConverter
        {
            public object ToXml(HttpContext httpContext, DateTime value) => value.ToUniversalTime().ToString("R");
            public DateTime FromXml(HttpContext httpContext, object value) => DateTime.Parse((string)value, CultureInfo.InvariantCulture);
        }

        public static IConverter TypeConverter { get; } = new Rfc1123DateConverter();

        /// <summary>
        /// Converter to map RFC1123 dates to/from a <see cref="DateTime"/>.
        /// </summary>
        public override IConverter Converter => TypeConverter;
    }

    /// <summary>
    /// Abstract base class representing a single DAV property using an
    /// ISO 8601 date type (mapped to <see cref="DateTime"/>).
    /// </summary>
    /// <typeparam name="TEntry">
    /// Store item or collection to which this DAV property applies.
    /// </typeparam>
    public abstract class DavIso8601Date<TEntry> : DavTypedProperty<TEntry, DateTime> where TEntry : IStoreItem
    {
        private class Iso8601DateConverter : IConverter
        {
            public object ToXml(HttpContext httpContext, DateTime value)
            {
                // The older built-in Windows WebDAV clients have a problem, so
                // they cannot deal with more than 3 digits for the
                // milliseconds.
                if (HasIso8601FractionBug(httpContext))
                {
                    // We need to recreate the date again, because the Windows 7
                    // WebDAV client cannot 
                    var dt = new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second, value.Millisecond, DateTimeKind.Utc);
                    return XmlConvert.ToString(dt, XmlDateTimeSerializationMode.Utc);
                }

                return XmlConvert.ToString(value, XmlDateTimeSerializationMode.Utc);
            }

            public DateTime FromXml(HttpContext httpContext, object value) => XmlConvert.ToDateTime((string)value, XmlDateTimeSerializationMode.Utc);

            private bool HasIso8601FractionBug(HttpContext httpContext)
            {
                // TODO: Determine which WebDAV clients have this bug
                return true;
            }
        }

        public static IConverter TypeConverter { get; } = new Iso8601DateConverter();

        /// <summary>
        /// Converter to map ISO 8601 dates to/from a <see cref="DateTime"/>.
        /// </summary>
        public override IConverter Converter => TypeConverter;
    }

    /// <summary>
    /// Abstract base class representing a single DAV property using a
    /// <see cref="bool"/> type.
    /// </summary>
    /// <typeparam name="TEntry">
    /// Store item or collection to which this DAV property applies.
    /// </typeparam>
    public abstract class DavBoolean<TEntry> : DavTypedProperty<TEntry, bool> where TEntry : IStoreItem
    {
        private class BooleanConverter : IConverter
        {
            public object ToXml(HttpContext httpContext, bool value) => value ? "1" : "0";
            public bool FromXml(HttpContext httpContext, object value) => int.Parse(value.ToString()) != 0;
        }

        public static IConverter TypeConverter { get; } = new BooleanConverter();

        /// <summary>
        /// Converter to map an XML boolean to/from a <see cref="bool"/>.
        /// </summary>
        public override IConverter Converter => TypeConverter;
    }

    /// <summary>
    /// Abstract base class representing a single DAV property using a
    /// <see cref="string"/> type.
    /// </summary>
    /// <typeparam name="TEntry">
    /// Store item or collection to which this DAV property applies.
    /// </typeparam>
    public abstract class DavString<TEntry> : DavTypedProperty<TEntry, string> where TEntry : IStoreItem
    {
        private class StringConverter : IConverter
        {
            public object ToXml(HttpContext httpContext, string value) => value;
            public string FromXml(HttpContext httpContext, object value) => value.ToString();
        }

        public static IConverter TypeConverter { get; } = new StringConverter();

        /// <summary>
        /// Converter to map an XML string to/from a <see cref="string"/>.
        /// </summary>
        public override IConverter Converter => TypeConverter;
    }

    /// <summary>
    /// Abstract base class representing a single DAV property using an
    /// <see cref="int"/> type.
    /// </summary>
    /// <typeparam name="TEntry">
    /// Store item or collection to which this DAV property applies.
    /// </typeparam>
    public abstract class DavInt32<TEntry> : DavTypedProperty<TEntry, int> where TEntry : IStoreItem
    {
        private class Int32Converter : IConverter
        {
            public object ToXml(HttpContext httpContext, int value) => value.ToString(CultureInfo.InvariantCulture);
            public int FromXml(HttpContext httpContext, object value) => int.Parse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        public static IConverter TypeConverter { get; } = new Int32Converter();

        /// <summary>
        /// Converter to map an XML number to/from a <see cref="int"/>.
        /// </summary>
        public override IConverter Converter => TypeConverter;
    }

    /// <summary>
    /// Abstract base class representing a single DAV property using a
    /// <see cref="long"/> type.
    /// </summary>
    /// <typeparam name="TEntry">
    /// Store item or collection to which this DAV property applies.
    /// </typeparam>
    public abstract class DavInt64<TEntry> : DavTypedProperty<TEntry, long> where TEntry : IStoreItem
    {
        private class Int64Converter : IConverter
        {
            public object ToXml(HttpContext httpContext, long value) => value.ToString(CultureInfo.InvariantCulture);
            public long FromXml(HttpContext httpContext, object value) => int.Parse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        public static IConverter TypeConverter { get; } = new Int64Converter();

        /// <summary>
        /// Converter to map an XML number to/from a <see cref="long"/>.
        /// </summary>
        public override IConverter Converter => TypeConverter;
    }

    /// <summary>
    /// Abstract base class representing a single DAV property using an
    /// <see cref="XElement"/> array.
    /// </summary>
    /// <typeparam name="TEntry">
    /// Store item or collection to which this DAV property applies.
    /// </typeparam>
    public abstract class DavXElementArray<TEntry> : DavTypedProperty<TEntry, IEnumerable<XElement>> where TEntry : IStoreItem
    {
        private class XElementArrayConverter : IConverter
        {
            public object ToXml(HttpContext httpContext, IEnumerable<XElement> value) => value;
            public IEnumerable<XElement> FromXml(HttpContext httpContext, object value) => (IEnumerable<XElement>)value;
        }

        public static IConverter TypeConverter { get; } = new XElementArrayConverter();

        /// <summary>
        /// Converter to map an XML number to/from an <see cref="XElement"/> array.
        /// </summary>
        public override IConverter Converter => TypeConverter;
    }

    /// <summary>
    /// Abstract base class representing a single DAV property using an
    /// <see cref="XElement"/> type.
    /// </summary>
    /// <typeparam name="TEntry">
    /// Store item or collection to which this DAV property applies.
    /// </typeparam>
    public abstract class DavXElement<TEntry> : DavTypedProperty<TEntry, XElement> where TEntry : IStoreItem
    {
        private class XElementConverter : IConverter
        {
            public object ToXml(HttpContext httpContext, XElement value) => value;
            public XElement FromXml(HttpContext httpContext, object value) => (XElement)value;
        }

        public static IConverter TypeConverter { get; } = new XElementConverter();

        /// <summary>
        /// Converter to map an XML number to/from a <see cref="XElement"/>.
        /// </summary>
        public override IConverter Converter => TypeConverter;
    }

    /// <summary>
    /// Abstract base class representing a single DAV property using an
    /// <see cref="Uri"/> type.
    /// </summary>
    /// <typeparam name="TEntry">
    /// Store item or collection to which this DAV property applies.
    /// </typeparam>
    public abstract class DavUri<TEntry> : DavTypedProperty<TEntry, Uri> where TEntry : IStoreItem
    {
        private class UriConverter : IConverter
        {
            public object ToXml(HttpContext httpContext, Uri value) => value.ToString();
            public Uri FromXml(HttpContext httpContext, object value) => new Uri((string)value);
        }

        public static IConverter TypeConverter { get; } = new UriConverter();

        /// <summary>
        /// Converter to map an XML string to/from a <see cref="Uri"/>.
        /// </summary>
        public override IConverter Converter => TypeConverter;
    }
}
