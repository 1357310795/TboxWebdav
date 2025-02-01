﻿using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TboxWebdav.Server.Modules.Tbox.Models.Convertion;
using TboxWebdav.Server.Modules.Tbox.Services;
using TboxWebdav.Server.Modules.Webdav;
using TboxWebdav.Server.Modules.Webdav.Internal;
using TboxWebdav.Server.Modules.Webdav.Internal.Helpers;
using TboxWebdav.Server.Modules.Webdav.Internal.Locking;
using TboxWebdav.Server.Modules.Webdav.Internal.Stores;

namespace TboxWebdav.Server.Modules.Tbox
{
    public class TboxStore : IStore
    {
        private readonly ILogger<TboxStore> _logger;
        private readonly TboxService _tbox;
        private readonly IServiceProvider _serviceProvider;
        private readonly IWebDavStoreContext _context;

        public TboxStore(ILogger<TboxStore> logger, TboxService tbox, IServiceProvider serviceProvider, IWebDavStoreContext context)
        {
            _logger = logger;
            _tbox = tbox;
            _serviceProvider = serviceProvider;
            _context = context;
        }

        public Task<IStoreItem> GetItemAsync(Uri uri, HttpContext httpContext)
        {
            var res = GetItemAsyncInternal(uri).Result;
            _logger.Log(LogLevel.Debug, $"【{(res?.GetType())}】路径 {uri.ToString()}");
            return Task.FromResult(res);
        }

        private Task<IStoreItem> GetItemAsyncInternal(Uri uri)
        {
            // Determine the path from the uri
            var path = UriHelper.GetPathFromUri(uri);
            var topfolder = UriHelper.GetTopFolderFromUri(uri);

            //if (topfolder == "他人的分享链接")
            //{
            //    var specialfolder = JboxSpecialCollection_Shared.getInstance(LockingManager, JboxSpecialCollectionType.Shared);
            //    return specialfolder.GetItemFromPathAsync(path);
            //    //return Task.FromResult<IStoreItem>();
            //}
            //if (topfolder == "交大空间")
            //{
            //    var specialfolder = JboxSpecialCollection_Public.getInstance(LockingManager);
            //    return specialfolder.GetItemFromPathAsync(path);
            //    //return Task.FromResult<IStoreItem>();
            //}

            var res = _tbox.GetItemInfo(path);

            if (!res.Success)
            {
                // The item doesn't exist
                return Task.FromResult<IStoreItem>(null);
            }

            if (res.Result.Type == "file")
            {
                var item = _serviceProvider.GetService<IStoreItem>();
                (item as TboxStoreItem).SetFileInfo(res.Result.ToTboxFileInfoDto());
                // Return the item
                return Task.FromResult<IStoreItem>(item);
            }
            else
            {
                var item = _serviceProvider.GetService<IStoreCollection>();
                (item as TboxStoreCollection).SetFolderInfo(res.Result.ToTboxFolderInfoDto());
                // Return the item
                return Task.FromResult<IStoreItem>(item);
            }
        }

        public Task<IStoreCollection> GetCollectionAsync(Uri uri, HttpContext httpContext)
        {
            var res = GetCollectionInternal(uri).Result;
            _logger.Log(LogLevel.Debug, $"【{(res?.GetType())}】路径 {uri.ToString()}");
            return Task.FromResult(res);
        }

        private Task<IStoreCollection> GetCollectionInternal(Uri uri)
        {
            // Determine the path from the uri
            var path = UriHelper.GetPathFromUri(uri);
            var topfolder = UriHelper.GetTopFolderFromUri(uri);

            //if (topfolder == "他人的分享链接")
            //{
            //    var specialfolder = JboxSpecialCollection_Shared.getInstance(LockingManager, JboxSpecialCollectionType.Shared);
            //    return specialfolder.GetCollectionFromPathAsync(path);
            //    //return Task.FromResult<IStoreItem>();
            //}

            //if (topfolder == "交大空间")
            //{
            //    var specialfolder = JboxSpecialCollection_Public.getInstance(LockingManager);
            //    return specialfolder.GetCollectionFromPathAsync(path);
            //    //return Task.FromResult<IStoreItem>();
            //}

            var res = _tbox.GetFolderInfo(path);

            if (!res.Success || res.Result.Type != "dir")
            {
                // The item doesn't exist
                return Task.FromResult<IStoreCollection>(null);
            }
            var item = _serviceProvider.GetService<IStoreCollection>();
            (item as TboxStoreCollection).SetFolderInfo(res.Result);
            // Return the item
            return Task.FromResult<IStoreCollection>(item);
        }

        public async Task<DavStatusCode> DirectDeleteItemAsync(string deleteItemPath)
        {
            var res = _tbox.DeleteFile(deleteItemPath);
            if (res.Success)
            {
                return DavStatusCode.Ok;
            }
            else if (res.Message.Contains("not found"))
            {
                return DavStatusCode.NotFound;
            }
            else
            {
                _logger.LogError($"Delete failed: {res.Message}");
                return DavStatusCode.InternalServerError;
            }
        }

        public async Task<DavStatusCode> DirectMoveItemAsync(string srcItemPath, string destItemPath)
        {
            var res = _tbox.CopyOrMoveFile(srcItemPath, destItemPath, true);
            if (res.Success)
            {
                return DavStatusCode.Ok;
            }
            else if (res.Message.Contains("SourceFileNotFound"))
            {
                return DavStatusCode.NotFound;
            }
            else
            {
                _logger.LogError($"MoveIten failed: {res.Message}");
                return DavStatusCode.InternalServerError;
            }
        }
    }
}
