﻿// 
// Copyright (c) 2004-2017 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen
// 
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without 
// modification, are permitted provided that the following conditions 
// are met:
// 
// * Redistributions of source code must retain the above copyright notice, 
//   this list of conditions and the following disclaimer. 
// 
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution. 
// 
// * Neither the name of Jaroslaw Kowalski nor the names of its 
//   contributors may be used to endorse or promote products derived from this
//   software without specific prior written permission. 
// 
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE 
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE 
// ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE 
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR 
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS 
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN 
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF 
// THE POSSIBILITY OF SUCH DAMAGE.
// 


namespace NLog
{
#if !SILVERLIGHT
    using System;
    using Internal;
	using System.Linq;

    /// <summary>
    /// Async version of <see cref="NestedDiagnosticsContext" /> - a logical context structure that keeps a stack
    /// Allows for maintaining scope across asynchronous tasks and call contexts.
    /// </summary>
    public static class NestedDiagnosticsLogicalContext
    {
        /// <summary>
        /// Pushes the specified value on current stack
        /// </summary>
        /// <param name="value">The value to be pushed.</param>
        /// <returns>An instance of the object that implements IDisposable that returns the stack to the previous level when IDisposable.Dispose() is called. To be used with C# using() statement.</returns>
        public static IDisposable Push<T>(T value)
        {
            var parent = GetThreadLocal();
            var current = new NestedContext<T>(parent, value);
            SetThreadLocal(current);
            return current;
        }

        /// <summary>
        /// Pops the top message off the NDLC stack.
        /// </summary>
        /// <returns>The top message which is no longer on the stack.</returns>
        public static string Pop()
        {
            return Pop(null);
        }

        /// <summary>
        /// Pops the top message from the NDLC stack.
        /// </summary>
        /// <param name="formatProvider">The <see cref="IFormatProvider"/> to use when converting the value to a string.</param>
        /// <returns>The top message, which is removed from the stack, as a string value.</returns>
        public static string Pop(IFormatProvider formatProvider)
        {
            return FormatHelper.ConvertToString(PopObject(), formatProvider);
        }

        /// <summary>
        /// Pops the top message off the current stack
        /// </summary>
        /// <returns>The top message which is no longer on the stack.</returns>
        public static object PopObject()
        {
            var current = GetThreadLocal();
            if (current != null)
                SetThreadLocal(current.Parent);
            return current != null ? current.Value : string.Empty;
        }

        /// <summary>
        /// Clears current stack.
        /// </summary>
        public static void Clear()
        {
            SetThreadLocal(null);
        }

        /// <summary>
        /// Gets all messages on the stack.
        /// </summary>
        /// <returns>Array of strings on the stack.</returns>
        public static string[] GetAllMessages()
        {
            return GetAllMessages(null);
        }

        /// <summary>
        /// Gets all messages from the stack, without removing them.
        /// </summary>
        /// <param name="formatProvider">The <see cref="IFormatProvider"/> to use when converting a value to a string.</param>
        /// <returns>Array of strings.</returns>
        public static string[] GetAllMessages(IFormatProvider formatProvider)
        {
            return GetAllObjects().Select((o) => FormatHelper.ConvertToString(o, formatProvider)).ToArray();
        }

        /// <summary>
        /// Gets all objects on the stack. The objects are not removed from the stack.
        /// </summary>
        /// <returns>Array of objects on the stack.</returns>
        public static object[] GetAllObjects()
        {
            var currentContext = GetThreadLocal();
            if (currentContext == null)
                return ArrayHelper.Empty<object>();

            int index = 0;
            object[] messages = new object[currentContext.FrameLevel];
            while (currentContext != null)
            {
                messages[index++] = currentContext.Value;
                currentContext = currentContext.Parent;
            }
            return messages;
        }

        interface INestedContext : IDisposable
        {
            INestedContext Parent { get; }
            int FrameLevel { get; }
            object Value { get; }
        }

#if !NETSTANDARD1_5
        [Serializable]
#endif
        class NestedContext<T> : INestedContext
        {
            public INestedContext Parent { get; private set; }
            public T Value { get; private set; }
            object INestedContext.Value { get { return Value; } }
            public int FrameLevel { get; private set; }

            public NestedContext(INestedContext parent, T value)
            {
                Parent = parent;
                Value = value;
                FrameLevel = parent != null ? parent.FrameLevel + 1 : 1; 
            }

            void IDisposable.Dispose()
            {
                PopObject();
            }

            public override string ToString()
            {
                object value = Value;
                return value != null ? value.ToString() : "null";
            }
        }

        private static void SetThreadLocal(INestedContext newValue)
        {
#if NET4_6 || NETSTANDARD
            AsyncNestedDiagnosticsContext.Value = newValue;
#else
            if (newValue == null)
                System.Runtime.Remoting.Messaging.CallContext.FreeNamedDataSlot(NestedDiagnosticsContextKey);
            else
                System.Runtime.Remoting.Messaging.CallContext.LogicalSetData(NestedDiagnosticsContextKey, newValue);
#endif
        }

        private static INestedContext GetThreadLocal()
        {
#if NET4_6 || NETSTANDARD
            return AsyncNestedDiagnosticsContext.Value;
#else
            return System.Runtime.Remoting.Messaging.CallContext.LogicalGetData(NestedDiagnosticsContextKey) as INestedContext;
#endif
        }

#if NET4_6 || NETSTANDARD
        private static readonly System.Threading.AsyncLocal<INestedContext> AsyncNestedDiagnosticsContext = new System.Threading.AsyncLocal<INestedContext>();
#else
        private const string NestedDiagnosticsContextKey = "NLog.AsyncableNestedDiagnosticsContext";
#endif
    }
#endif
}
