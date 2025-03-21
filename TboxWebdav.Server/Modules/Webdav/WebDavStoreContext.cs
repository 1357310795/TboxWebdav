﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TboxWebdav.Server.Modules.Webdav.Internal.Locking;

namespace TboxWebdav.Server.Modules.Webdav
{
    public class WebDavStoreContext : IWebDavStoreContext
    {
        public WebDavStoreContext()
        {
            IsWritable = true;
            LockingManager = new InMemoryLockingManager();
        }

        public bool IsWritable { get; }
        public ILockingManager LockingManager { get; }
    }
}
