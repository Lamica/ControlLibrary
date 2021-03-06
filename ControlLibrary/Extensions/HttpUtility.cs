﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ControlLibrary.Extensions
{
    public static class HttpUtility
    {
        /// <summary>
        /// HTML Encodes a string.
        /// </summary>
        /// <param name="buf"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string HtmlEncode(this string buf)
        {
            return System.Net.WebUtility.HtmlEncode(buf);
        }

        /// <summary>
        /// HTML Decodes a string.
        /// </summary>
        /// <param name="buf"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string HtmlDecode(this string buf)
        {
            return System.Net.WebUtility.HtmlDecode(buf);
        }

        /// <summary>
        /// Encodes text for use in a URL.
        /// </summary>
        /// <param name="urlText"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string UrlEncode(this string urlText)
        {
            return System.Net.WebUtility.UrlEncode(urlText);
        }

        /// <summary>
        /// Decodes text for use in a URL.
        /// </summary>
        /// <param name="urlText"></param>
        /// <returns></returns>
        /// <remarks></remarks>
        public static string UrlDecode(this string urlText)
        {
            return System.Net.WebUtility.UrlDecode(urlText);
        }

        public static byte[] UrlEncodeToBytes(this byte[] urlBytes, int offset = 0, int count = int.MaxValue)
        {
            if (count == int.MaxValue)
                return System.Net.WebUtility.UrlEncodeToBytes(urlBytes, offset, urlBytes.Length);
            else
                return System.Net.WebUtility.UrlEncodeToBytes(urlBytes, offset, count);
        }

        public static byte[] UrlDecodeToBytes(this byte[] urlBytes, int offset = 0, int count = int.MaxValue)
        {
            if (count == int.MaxValue)
                return System.Net.WebUtility.UrlDecodeToBytes(urlBytes, offset, urlBytes.Length);
            else
                return System.Net.WebUtility.UrlDecodeToBytes(urlBytes, offset, count);
        }
    }
}
