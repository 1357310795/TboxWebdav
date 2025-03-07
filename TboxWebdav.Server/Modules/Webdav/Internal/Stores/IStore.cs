﻿using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TboxWebdav.Server.Modules.Webdav.Internal;
using TboxWebdav.Server.Modules.Webdav.Internal.Locking;
using TboxWebdav.Server.Modules.Webdav.Internal.Props;

namespace TboxWebdav.Server.Modules.Webdav.Internal.Stores
{
    public struct StoreItemResult
    {
        public DavStatusCode Result { get; }
        public IStoreItem Item { get; }

        public StoreItemResult(DavStatusCode result, IStoreItem item = null)
        {
            Result = result;
            Item = item;
        }

        public static bool operator !=(StoreItemResult left, StoreItemResult right)
        {
            return !(left == right);
        }

        public static bool operator ==(StoreItemResult left, StoreItemResult right)
        {
            return left.Result == right.Result && (left.Item == null && right.Item == null || left.Item != null && left.Item.Equals(right.Item));
        }

        public override bool Equals(object obj)
        {
            if (!(obj is StoreItemResult))
                return false;
            return this == (StoreItemResult)obj;
        }

        public override int GetHashCode() => Result.GetHashCode() ^ (Item?.GetHashCode() ?? 0);
    }

    public struct StoreCollectionResult
    {
        public DavStatusCode Result { get; }
        public IStoreCollection Collection { get; }

        public StoreCollectionResult(DavStatusCode result, IStoreCollection collection = null)
        {
            Result = result;
            Collection = collection;
        }

        public static bool operator !=(StoreCollectionResult left, StoreCollectionResult right)
        {
            return !(left == right);
        }

        public static bool operator ==(StoreCollectionResult left, StoreCollectionResult right)
        {
            return left.Result == right.Result && (left.Collection == null && right.Collection == null || left.Collection != null && left.Collection.Equals(right.Collection));
        }

        public override bool Equals(object obj)
        {
            if (!(obj is StoreCollectionResult))
                return false;
            return this == (StoreCollectionResult)obj;
        }

        public override int GetHashCode() => Result.GetHashCode() ^ (Collection?.GetHashCode() ?? 0);
    }

    public interface IStore
    {
        Task<IStoreItem> GetItemAsync(Uri uri, HttpContext httpContext);
        Task<IStoreCollection> GetCollectionAsync(Uri uri, HttpContext httpContext);
        Task<DavStatusCode> DirectDeleteItemAsync(string deleteItemPath);
        Task<DavStatusCode> DirectMoveItemAsync(string srcItemPath, string destItemPath);
    }

    public interface IStoreItem
    {
        // Item properties
        string Name { get; }
        string UniqueKey { get; }
        string MimeType { get; }
        string FullPath { get; }

        // Read/Write access to the data
        Task<Stream> GetReadableStreamAsync(HttpContext httpContext);
        Task<Stream> GetReadableStreamAsync(HttpContext httpContext, long? start, long? end);

        // Copy support
        Task<DavStatusCode> CopyAsync(IStoreCollection destination, string name, bool overwrite, HttpContext httpContext);

        // Property support
        IPropertyManager PropertyManager { get; }

        // Locking support
        ILockingManager LockingManager { get; }
    }

    public interface IStoreCollection : IStoreItem
    {
        // Get specific item (or all items)
        Task<IStoreItem> GetItemAsync(string name, HttpContext httpContext);

        Task<IEnumerable<IStoreItem>> GetItemsAsync(HttpContext httpContext);
        //Upload File
        Task<DavStatusCode> UploadFromStreamAsync(HttpContext httpContext, string name, Stream source, long length);

        // Create items and collections and add to the collection
        Task<StoreItemResult> CreateItemAsync(string name, bool overwrite, HttpContext httpContext);
        Task<DavStatusCode> CreateCollectionAsync(string name, bool overwrite, HttpContext httpContext);

        // Checks if the collection can be moved directly to the destination
        bool SupportsFastMove(IStoreCollection destination, string destinationName, bool overwrite, HttpContext httpContext);

        // Move items between collections
        Task<StoreItemResult> MoveItemAsync(string sourceName, IStoreCollection destination, string destinationName, bool overwrite, HttpContext httpContext);

        // Delete items from collection
        Task<DavStatusCode> DeleteItemAsync(string name, HttpContext httpContext);

        InfiniteDepthMode InfiniteDepthMode { get; }
    }

    /// <summary>
    /// When the Depth is set to infinite, then this enumeration specifies
    /// how to deal with this.
    /// </summary>
    public enum InfiniteDepthMode
    {
        /// <summary>
        /// Infinite depth is allowed (this is according spec).
        /// </summary>
        Allowed,

        /// <summary>
        /// Infinite depth is not allowed (this results in HTTP 403 Forbidden).
        /// </summary>
        Rejected,

        /// <summary>
        /// Infinite depth is handled as Depth 0.
        /// </summary>
        Assume0,

        /// <summary>
        /// Infinite depth is handled as Depth 1.
        /// </summary>
        Assume1
    }
}
