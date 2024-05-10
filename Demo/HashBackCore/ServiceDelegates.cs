using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace billpg.HashBackCore
{
    public delegate Exception OnErrorFn(string message);
    public delegate long OnReadClockFn();
    public delegate string OnRetrieveVerifyHashFn(Uri uri);
    public delegate IPAddress OnHostLookupFn(string host);
    public delegate Exception OnRetrieveErrorFn(string message);

}
