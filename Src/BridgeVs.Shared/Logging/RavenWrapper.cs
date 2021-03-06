﻿#region License
// Copyright (c) 2013 - 2018 Coding Adventures
//
// Permission is hereby granted, free of charge, to any person
// obtaining a copy of this software and associated documentation
// files (the "Software"), to deal in the Software without
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following
// conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NON INFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
#endregion

using BridgeVs.Shared.Common;
using SharpRaven;
using SharpRaven.Data;
using System;
using System.Diagnostics;
using System.Reflection;

namespace BridgeVs.Shared.Logging
{
    public static class RavenWrapper
    { 
        /// <summary>
        /// The client id associated with the Sentry.io account.
        /// </summary>
        private const string RavenClientId = "https://e187bfd9b1304311be6876ac9036956d:6580e15fc2fd40899e5d5b0f28685efc@sentry.io/1189532";
        private const string LinqBridgeVs = "linqbridgevs";
        private const int Timeout = 2000;

        private static readonly Action<Exception> OnSendError = ex =>
        {
            Trace.WriteLine("Error sending report to Sentry.io");
            Trace.WriteLine(ex.Message);
            Trace.WriteLine(ex.StackTrace);
        };

        [Conditional("DEPLOY")]
        public static void Capture(this Exception exception, string vsVersion, ErrorLevel errorLevel = ErrorLevel.Error, string message = "")
        {
            if (string.IsNullOrEmpty(vsVersion))
            {
                return;
            }

            if (!CommonRegistryConfigurations.IsErrorTrackingEnabled(vsVersion))
            {
                return;
            }

            Func<Requester, Requester> removeUserId = new Func<Requester, Requester>(request =>
            { 
                //GDPR compliant, no personal data sent: no server name, no username stored, no ip address
                request.Packet.ServerName = LinqBridgeVs;
                request.Packet.Contexts.Device.Name = LinqBridgeVs;
                request.Packet.User.Username = CommonRegistryConfigurations.GetUniqueGuid(vsVersion);
                request.Packet.Release = Assembly.GetExecutingAssembly().VersionNumber();
                request.Packet.User.IpAddress = "0.0.0.0";
                return request;
            });

            SentryEvent sentryEvent = new SentryEvent(exception)
            {
                Message = message,
                Level = errorLevel
            };

            sentryEvent.Tags.Add("Visual Studio Version", vsVersion);

            RavenClient ravenClient = new RavenClient(RavenClientId)
            {
                BeforeSend = removeUserId,
                ErrorOnCapture = OnSendError,
                Timeout = TimeSpan.FromMilliseconds(Timeout) //should fail early if it can't send a message
            };

            ravenClient.Capture(sentryEvent);
        }
    }
}