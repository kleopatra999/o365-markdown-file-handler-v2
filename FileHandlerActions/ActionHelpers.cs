﻿/*
 * Markdown File Handler - Sample Code
 * Copyright (c) Microsoft Corporation
 * All rights reserved. 
 * 
 * MIT License
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of 
 * this software and associated documentation files (the ""Software""), to deal in 
 * the Software without restriction, including without limitation the rights to use, 
 * copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the
 * Software, and to permit persons to whom the Software is furnished to do so, 
 * subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
 * PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION 
 * OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
 * SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

namespace FileHandlerActions
{
    using System;
    using System.Web;

    public static class ActionHelpers
    {
        /// <summary>
        /// Returns an access token from a URL string, if one is available
        /// </summary>
        /// <param name="oneDriveApiSourceUrl"></param>
        /// <returns></returns>
        public static string ParseAccessToken(string oneDriveApiSourceUrl)
        {
            UriBuilder builder = new UriBuilder(oneDriveApiSourceUrl);
            var queryString = builder.Query;
            var values = HttpUtility.ParseQueryString(queryString);
            return values["access_token"];
        }

        /// <summary>
        /// Trims the API URL at /drive to return the base URL we can use to build other API calls
        /// </summary>
        /// <param name="oneDriveApiSourceUrl"></param>
        /// <returns></returns>
        public static string ParseBaseUrl(string oneDriveApiSourceUrl)
        {
            var trimPoint = oneDriveApiSourceUrl.IndexOf("/drive");
            return oneDriveApiSourceUrl.Substring(0, trimPoint);
        }


        public static string BuildApiUrl(string baseUrl, string driveId, string itemId, string extra = "")
        {
            return $"{baseUrl}/drives/{driveId}/items/{itemId}/{extra}";
        }
    }
}
