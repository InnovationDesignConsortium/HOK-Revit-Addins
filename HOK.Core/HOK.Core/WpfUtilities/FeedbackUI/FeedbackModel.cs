﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using RestSharp;
using HOK.Core.Utilities;

namespace HOK.Core.WpfUtilities.FeedbackUI
{
    public class Response
    {
        public UploadImageContent content { get; set; }
        public UploadCommit commit { get; set; }
    }

    public class UploadCommit
    {
        public string sha { get; set; }
    }

    public class UploadImageContent
    {
        public string path { get; set; }
        public string sha { get; set; }
        public string name { get; set; }
        public string html_url { get; set; }
    }

    public class DeleteObject
    {
        public string path { get; set; }
        public string message { get; set; }
        public string sha { get; set; }
        public string branch { get; set; }
    }

    public class UploadObject
    {
        public string path { get; set; }
        public string message { get; set; }
        public string content { get; set; }
        public string branch { get; set; }
    }

    public static class ImageExtensions
    {
        public static byte[] ToByteArray(this Image image, ImageFormat format)
        {
            using (var ms = new MemoryStream())
            {
                image.Save(ms, format);
                return ms.ToArray();
            }
        }
    }

    public class FeedbackModel
    {
        private const string baseUrl = "https://api.github.com";

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="att"></param>
        /// <returns></returns>
        public async Task<T> RemoveImage<T>(AttachmentViewModel att) where T : new()
        {
            var client = new RestClient(baseUrl);
            var request = new RestRequest("/repos/HOKGroup/MissionControl_Issues/contents/" + att.UploadImageContent.path, Method.DELETE)
            {
                OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; }
            };
            request.AddHeader("Content-type", "application/json");
            request.AddHeader("Authorization", "Token fc396d894a4f27520b8ce85564c5fc2b2a15b88f");
            request.RequestFormat = DataFormat.Json;

            request.AddBody(new DeleteObject
            {
                path = att.UploadImageContent.path,
                message = "removing an image",
                sha = att.UploadImageContent.sha,
                branch = "master"
            });

            var response = await client.ExecuteTaskAsync<T>(request);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                Log.AppendLog(LogMessageType.EXCEPTION, response.StatusDescription);
                return new T();
            }

            return response.Data;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="att"></param>
        /// <param name="createTemp"></param>
        /// <returns></returns>
        public async Task<T> PostImage<T>(AttachmentViewModel att, bool createTemp) where T: new()
        {
            string tempFile;
            if (createTemp)
            {
                if (!File.Exists(att.FilePath)) return new T();

                try
                {
                    tempFile = Path.Combine(Path.GetTempPath(), DateTime.Now.ToString("yyyyMMddTHHmmss") + "_" + Path.GetFileName(att.FilePath));
                    File.Copy(att.FilePath, tempFile);
                }
                catch (Exception e)
                {
                    Log.AppendLog(LogMessageType.EXCEPTION, e.Message);
                    return new T();
                }
            }
            else
            {
                tempFile = att.FilePath;
            }

            var bytes = File.ReadAllBytes(tempFile);
            var body = new UploadObject
            {
                path = Path.Combine("images", Path.GetFileName(tempFile)),
                message = "uploading an image",
                content = Convert.ToBase64String(bytes),
                branch = "master"
            };

            var client = new RestClient(baseUrl);
            var request = new RestRequest("/repos/HOKGroup/MissionControl_Issues/contents/" + body.path, Method.PUT)
            {
                OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; }
            };
            request.AddHeader("Content-type", "application/json");
            request.AddHeader("Authorization", "Token fc396d894a4f27520b8ce85564c5fc2b2a15b88f");
            request.RequestFormat = DataFormat.Json;
            request.AddBody(body);

            try
            {
                File.Delete(tempFile);
            }
            catch (Exception e)
            {
                Log.AppendLog(LogMessageType.EXCEPTION, e.Message);
            }

            var response = await client.ExecuteTaskAsync<T>(request);
            if (response.StatusCode != HttpStatusCode.Created)
            {
                Log.AppendLog(LogMessageType.EXCEPTION, response.StatusDescription);
                return new T();
            }

            return response.Data;
        }

        /// <summary>
        /// Submits a feedback from user to GitHub account via Issues page.
        /// </summary>
        /// <param name="name">User Name</param>
        /// <param name="email">User Email</param>
        /// <param name="feedback">Feedback/Comments</param>
        /// <param name="toolname">Name and version of tool used to submit.</param>
        /// <returns>Message</returns>
        public string Submit(string name, string email, string feedback, string toolname)
        {
            try
            {
                // (Konrad) This is a token and credentials for the github user we use here
                // username: hokfeedback
                // password: Password123456
                // token: fc396d894a4f27520b8ce85564c5fc2b2a15b88f
                var client = new RestClient(baseUrl);

                var stringBuilder = new StringBuilder();
                stringBuilder.AppendLine("From: " + name);
                stringBuilder.AppendLine("Email: " + email);
                stringBuilder.AppendLine("User: " + Environment.UserName);
                stringBuilder.AppendLine("Machine: " + Environment.MachineName);
                stringBuilder.AppendLine("");
                stringBuilder.AppendLine("Body: ");
                stringBuilder.AppendLine(feedback);

                var body = new Issue
                {
                    title = "hokfeedback - " + toolname,
                    body = stringBuilder.ToString(),
                    assignees = new List<string>(),
                    labels = new List<string>()
                };

                try
                {
                    var request = new RestRequest("/repos/HOKGroup/MissionControl_Issues/issues", Method.POST)
                    {
                        OnBeforeDeserialization = resp => { resp.ContentType = "application/json"; }
                    };
                    request.AddHeader("Content-type", "application/json");
                    request.AddHeader("Authorization", "Token fc396d894a4f27520b8ce85564c5fc2b2a15b88f");
                    request.RequestFormat = DataFormat.Json;
                    request.AddBody(body);

                    var response = client.Execute<Issue>(request);
                    return response.StatusCode == HttpStatusCode.Created ? "Success" : "Failed to create GitHub issue. Try again.";
                }
                catch (Exception ex)
                {
                    Log.AppendLog(LogMessageType.EXCEPTION, ex.Message);
                    return ex.Message;
                }
            }
            catch (Exception e)
            {
                Log.AppendLog(LogMessageType.EXCEPTION, e.Message);
                return e.Message;
            }
        }
    }

    public class Issue
    {
        public string title { get; set; }
        public string body { get; set; }
        public string milestone { get; set; }
        public List<string> assignees { get; set; }
        public List<string> labels { get; set; }
    }
}
