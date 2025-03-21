﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using TboxWebdav.Server.Modules.Webdav;
using TboxWebdav.Server.Modules.Webdav.Internal;
using TboxWebdav.Server.Modules.Webdav.Internal.Helpers;
using TboxWebdav.Server.Modules.Webdav.Internal.Locking;
using TboxWebdav.Server.Modules.Webdav.Internal.Stores;

namespace TboxWebdav.Server.Handlers
{
    /// <summary>
    /// Implementation of the LOCK method.
    /// </summary>
    /// <remarks>
    /// The specification of the WebDAV LOCK method can be found in the
    /// <see href="http://www.webdav.org/specs/rfc2518.html#METHOD_LOCK">
    /// WebDAV specification
    /// </see>.
    /// </remarks>
    public class LockHandler : IWebDavHandler
    {
        /// <summary>
        /// Handle a LOCK request.
        /// </summary>
        /// <param name="httpContext">
        /// The HTTP context of the request.
        /// </param>
        /// <param name="store">
        /// Store that is used to access the collections and items.
        /// </param>
        /// <returns>
        /// A task that represents the asynchronous LOCK operation. The task
        /// will always return <see langword="true"/> upon completion.
        /// </returns>
        public async Task<WebDavResult> HandleRequestAsync(HttpContext httpContext, IStore store)
        {
            // Obtain request and response
            var request = httpContext.Request;
            var response = httpContext.Response;

            // Determine the depth and requested timeout(s)
            var depth = request.GetDepth();
            var timeouts = request.GetTimeouts();

            // Obtain the WebDAV item
            var item = await store.GetItemAsync(new Uri(request.GetDisplayUrl()), httpContext).ConfigureAwait(false);
            if (item == null)
            {
                // Set status to not found
                return new WebDavResult(DavStatusCode.PreconditionFailed);
            }

            // Check if we have a lock manager
            var lockingManager = item.LockingManager;
            if (lockingManager == null)
            {
                // Set status to not found
                return new WebDavResult(DavStatusCode.PreconditionFailed);
            }

            LockResult lockResult;

            // Check if an IF header is present (this would refresh the lock)
            var refreshLockToken = request.GetIfLockToken();
            if (refreshLockToken != null)
            {
                // Obtain the token
                lockResult = lockingManager.RefreshLock(item, depth > 0, timeouts, refreshLockToken);
            }
            else
            {
                // Determine lock-scope and owner
                LockScope lockScope;
                LockType lockType;
                XElement owner;

                // Read the property set/remove items from the request
                try
                {
                    // Create an XML document from the stream
                    var xDoc = await request.LoadXmlDocumentAsync().ConfigureAwait(false);
                    if (xDoc == null)
                        throw new Exception("Request-content couldn't be read");

                    // Save the root document
                    var xRoot = xDoc.Root;
                    if (xRoot == null)
                        throw new Exception("No root element (expected 'lockinfo')");

                    // The document should contain a 'lockinfo' element
                    if (xRoot.Name != WebDavNamespaces.DavNs + "lockinfo")
                        throw new Exception("Invalid root element (expected 'lockinfo')");

                    // Check all descendants
                    var xLockScope = xRoot.Elements(WebDavNamespaces.DavNs + "lockscope").Single();
                    var xLockScopeValue = xLockScope.Elements().Single();
                    if (xLockScopeValue.Name == WebDavNamespaces.DavNs + "exclusive")
                        lockScope = LockScope.Exclusive;
                    else if (xLockScopeValue.Name == WebDavNamespaces.DavNs + "shared")
                        lockScope = LockScope.Shared;
                    else
                        throw new Exception("Invalid lockscope (expected 'exclusive' or 'shared')");

                    // Determine the lock-type
                    var xLockType = xRoot.Elements(WebDavNamespaces.DavNs + "locktype").Single();
                    var xLockTypeValue = xLockType.Elements().Single();
                    if (xLockTypeValue.Name == WebDavNamespaces.DavNs + "write")
                        lockType = LockType.Write;
                    else
                        throw new Exception("Invalid locktype (expected 'write')");

                    // Determine the owner
                    var xOwner = xRoot.Elements(WebDavNamespaces.DavNs + "owner").Single();
                    owner = xOwner.Elements().Single();
                }
                catch (Exception)
                {
                    return new WebDavResult(DavStatusCode.BadRequest);
                }

                // Perform the lock
                lockResult = lockingManager.Lock(item, lockType, lockScope, owner, new Uri(request.GetDisplayUrl()), depth > 0, timeouts);
            }

            // Check if result is fine
            if (lockResult.Result != DavStatusCode.Ok)
            {
                // Set status to not found
                return new WebDavResult(lockResult.Result);
            }

            // We should have an active lock result at this point
            Debug.Assert(lockResult.Lock.HasValue, "Lock information should be supplied, when creating or refreshing a lock");

            // Return the information about the lock
            var xDocument = new XDocument(
                new XElement(WebDavNamespaces.DavNs + "prop",
                    new XElement(WebDavNamespaces.DavNs + "lockdiscovery",
                        lockResult.Lock.Value.ToXml())));

            // Add the Lock-Token in the response
            // (only when creating a new lock)
            if (refreshLockToken == null)
                response.SetHeaderValue("Lock-Token", $"<{lockResult.Lock.Value.LockToken.AbsoluteUri}>");

            // Stream the document
            return new WebDavResult(DavStatusCode.Ok, xDocument);
        }
    }
}
