﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Store;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Networking.PushNotifications;
using Windows.Storage;

namespace RenrenCoreWrapper.Helper.PushNotificationsHelper
{
    public sealed class ChannelAndWebResponse
    {
        public PushNotificationChannel Channel {get; set;}
        public String WebResponse {get; set;}
    }

    [DataContract]
    internal class UrlData
    {
        [DataMember]
        public String Url;
        [DataMember]
        public bool IsAppId;
        [DataMember]
        public DateTime Renewed;
    }

    public sealed class Notifier
    {
        private const String APP_TILE_ID_KEY = "appTileIds";
        private const String MAIN_APP_TILE_KEY = "mainAppTileKey";
        private const int DAYS_TO_RENEW = 15; // Renew if older than 15 days
        private Dictionary<String, UrlData> urls;

        public Notifier()
        {
            urls = new Dictionary<String, UrlData>();
            List<String> storedUrls = null;
            IPropertySet currentData = ApplicationData.Current.LocalSettings.Values;

            try
            {
                String urlString = (String)currentData[APP_TILE_ID_KEY];
                using (MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(urlString)))
                {
                    DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(List<String>));
                    storedUrls = (List<String>)deserializer.ReadObject(stream);
                }
            }
            catch (Exception) { }

            if (storedUrls != null)
            {
                for (int i = 0; i < storedUrls.Count; i++)
                {
                    String key = storedUrls[i];
                    try
                    {
                        String dataString = (String)currentData[key];
                        using (MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(dataString)))
                        {
                            DataContractJsonSerializer deserializer = new DataContractJsonSerializer(typeof(UrlData));
                            urls[key] = (UrlData)deserializer.ReadObject(stream);
                        }
                    }
                    catch (Exception) { }
                }
            }
        }

        // Update the stored target URL for the default tile for the main app
        private void UpdateUrl(String url)
        {
            UpdateUrl(url, null, true);
        }

        // Update the stored target URL for the secondary tiles of the main app or other apps, or primary tiles of other apps
        private void UpdateUrl(String url, String inputItemId, bool isPrimaryTile)
        {
            String itemId = isPrimaryTile && inputItemId == null ? MAIN_APP_TILE_KEY : inputItemId;

            bool shouldSerialize = !this.urls.ContainsKey(itemId);
            DateTime now = DateTime.Now;
            UrlData storedData = new UrlData() { Url = url, IsAppId = isPrimaryTile, Renewed = now };
            this.urls[itemId] = storedData;

            using (MemoryStream stream = new MemoryStream())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(UrlData));
                serializer.WriteObject(stream, storedData);
                stream.Position = 0;
                using (StreamReader reader = new StreamReader(stream))
                {
                    ApplicationData.Current.LocalSettings.Values[itemId] = reader.ReadToEnd();
                }
            }

            if (shouldSerialize)
            {
                SaveAppTileIds();
            }
        }

        private void SaveAppTileIds()
        {
            List<String> dataToStore = new List<String>(urls.Count);
            foreach (String key in urls.Keys)
            {
                dataToStore.Add(key);
            }

            using (MemoryStream stream = new MemoryStream())
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(List<String>));
                serializer.WriteObject(stream, dataToStore);
                stream.Position = 0;
                using (StreamReader reader = new StreamReader(stream))
                {
                    ApplicationData.Current.LocalSettings.Values[APP_TILE_ID_KEY] = reader.ReadToEnd();
                }
            }
        }

        // This method checks the freshness of each channel, and returns as necessary
        public IAsyncAction RenewAllAsync(bool force)
        {
            DateTime now = DateTime.Now;
            TimeSpan daysToRenew = new TimeSpan(DAYS_TO_RENEW, 0, 0, 0);
            List<Task<ChannelAndWebResponse>> renewalTasks = new List<Task<ChannelAndWebResponse>>(this.urls.Count);
            foreach (var keyValue in this.urls)
            {
                UrlData dataForUpload = keyValue.Value;
                if (force || ((now - dataForUpload.Renewed) > daysToRenew))
                {
                    if (keyValue.Key == MAIN_APP_TILE_KEY)
                    {
                        renewalTasks.Add(OpenChannelAndUploadAsync(dataForUpload.Url).AsTask());
                    }
                    else
                    {
                        renewalTasks.Add(OpenChannelAndUploadAsync(dataForUpload.Url, keyValue.Key, dataForUpload.IsAppId).AsTask());
                    }

                }
            }
            return Task.WhenAll(renewalTasks).AsAsyncAction();
        }
       
        // Instead of using the async and await keywords, actual Tasks will be returned.
        // That way, components consuming these APIs can await the returned tasks
        public IAsyncOperation<ChannelAndWebResponse> OpenChannelAndUploadAsync(String url)
        {
            IAsyncOperation<PushNotificationChannel> channelOperation = PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
            return ExecuteChannelOperation(channelOperation, url, MAIN_APP_TILE_KEY, true);
        }

        public IAsyncOperation<ChannelAndWebResponse> OpenChannelAndUploadAsync(String url, String inputItemId, bool isPrimaryTile)
        {
            IAsyncOperation<PushNotificationChannel> channelOperation;
            if (isPrimaryTile)
            {
                channelOperation = PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync(inputItemId);
            }
            else
            {
                channelOperation = PushNotificationChannelManager.CreatePushNotificationChannelForSecondaryTileAsync(inputItemId);
            }

            return ExecuteChannelOperation(channelOperation, url, inputItemId, isPrimaryTile);
        }

        private IAsyncOperation<ChannelAndWebResponse> ExecuteChannelOperation(IAsyncOperation<PushNotificationChannel> channelOperation, String url, String itemId, bool isPrimaryTile)
        {
            return channelOperation.AsTask().ContinueWith<ChannelAndWebResponse>((Task<PushNotificationChannel> channelTask) =>
            {
                PushNotificationChannel newChannel = channelTask.Result;
                UpdateUrl(url, itemId, isPrimaryTile);

                HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create(url);
                webRequest.Method = "POST";
                webRequest.ContentType = "application/x-www-form-urlencoded";
                byte[] channelUriInBytes = Encoding.UTF8.GetBytes("ChannelUri=" + newChannel + "&ItemId=" + itemId);

                Task<Stream> requestTask = webRequest.GetRequestStreamAsync();
                using (Stream requestStream = requestTask.Result)
                {
                    requestStream.Write(channelUriInBytes, 0, channelUriInBytes.Length);
                }

                Task<WebResponse> responseTask = webRequest.GetResponseAsync();
                String webResponse;
                using (StreamReader requestReader = new StreamReader(responseTask.Result.GetResponseStream()))
                {
                    webResponse = requestReader.ReadToEnd();
                }


                return new ChannelAndWebResponse { Channel = newChannel, WebResponse = webResponse };
            }).AsAsyncOperation();
        }
    }
}
