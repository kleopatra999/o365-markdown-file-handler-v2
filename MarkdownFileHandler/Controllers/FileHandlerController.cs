﻿using FileHandlerActions;
using MarkdownFileHandler.Models;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Mvc;

namespace MarkdownFileHandler.Controllers
{

    [Authorize]
    public class FileHandlerController : Controller
    {

        /// <summary>
        /// Generate a read-only preview of the file
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> Preview()
        {
            var input = GetActivationParameters();

            if (!input.CanRead)
            {
                return View(MarkdownFileModel.GetErrorModel(input, "Required parameters are missing. Cannot read the source file."));
            }

            return View(await GetFileHandlerModelAsync(input, FileAccess.Read));
        }

        /// <summary>
        /// Generate a read-write opened version of the file
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> Open()
        {
            var input = GetActivationParameters();

            if (!input.CanWrite)
            {
                return View(MarkdownFileModel.GetErrorModel(input, "Required parameters are missing. Cannot write the source file."));
            }
            //TempData["activationParameters"] = input;

            return View(await GetFileHandlerModelAsync(input, FileAccess.Read));
        }

        public async Task<ActionResult> Edit()
        {
            //FileHandlerActivationParameters input = (FileHandlerActivationParameters)TempData["activationParameters"];
            var input = GetActivationParameters();

            if (input == null || !input.CanWrite)
            {
                return View(MarkdownFileModel.GetErrorModel(input, "Required parameters are missing. Cannot write the source file."));
            }
            
            return View(await GetFileHandlerModelAsync(input, FileAccess.ReadWrite));

        }

        public async Task<ActionResult> Save()
        {
            var input = GetActivationParameters();

            if (input == null || !input.CanWrite)
            {
                return Json(new SaveResults() { Success = false, Error = "Missing activation parameters." });
            }

            try
            {
                return Json(await SaveChangesToFileAsync(input));
            }
            catch (Exception ex)
            {
                return Json(new SaveResults { Success = false, Error = ex.Message });
            }
        }

        /// <summary>
        /// Generate a read-write editor experience for a new file
        /// </summary>
        /// <returns></returns>
        public async Task<ActionResult> NewFile()
        {
            var input = GetActivationParameters();

            if (!input.CanWrite)
            {
                return View(MarkdownFileModel.GetErrorModel(input, "Required parameters are missing. Cannot write the source file."));
            }

            return View("Edit", await GetFileHandlerModelAsync(input, FileAccess.Write));
        }


        public async Task<ActionResult> ConvertToPDF()
        {
            var input = GetActivationParameters();

            var pdfConverter = new FileHandlerActions.PdfConversion();
            FileHandlerActions.AsyncJob job = new FileHandlerActions.AsyncJob(pdfConverter);
            job.Status.OriginalParameters = input.ToDictionary();

            var resourceUrl = AuthHelper.GetResourceFromUrl(input.ItemUrl);
            var accessToken = await AuthHelper.GetUserAccessTokenSilentAsync(resourceUrl);

            HostingEnvironment.QueueBackgroundWorkItem(ct => job.Begin(new string[] { input.ItemUrl }, accessToken));
            return View(new AsyncActionModel { JobIdentifier = job.Id, Status = job.Status });
        }

        public ActionResult GetAsyncJobStatus(string identifier)
        {
            var job = FileHandlerActions.JobTracker.GetJob(identifier);
            return View("AsyncJobStatus", new AsyncActionModel { JobIdentifier = identifier, Status = job });
        }

        public async Task<ActionResult> CompressFiles()
        {
            var input = GetActivationParameters();

            var addToZipFile = new FileHandlerActions.AddToZip.AddToZipAction();
            FileHandlerActions.AsyncJob job = new FileHandlerActions.AsyncJob(addToZipFile);
            job.Status.OriginalParameters = input.ToDictionary();

            var resourceUrl = AuthHelper.GetResourceFromUrl(input.ItemUrls.First());
            var accessToken = await AuthHelper.GetUserAccessTokenSilentAsync(resourceUrl);

            HostingEnvironment.QueueBackgroundWorkItem(ct => job.Begin(input.ItemUrls, accessToken));
            return View(new AsyncActionModel { JobIdentifier = job.Id, Status = job.Status });
        }


        /// <summary>
        /// Parse either the POST data or stored cookie data to retrieve the file information from
        /// the request.
        /// </summary>
        /// <returns></returns>
        private FileHandlerActivationParameters GetActivationParameters()
        {
            FileHandlerActivationParameters activationParameters = null;
            if (Request.Form != null && Request.Form.AllKeys.Count<string>() != 0)
            {
                // Get from current request's form data
                activationParameters = new FileHandlerActivationParameters(Request.Form);
            }
            else
            {
                // If form data does not exist, it must be because of the sign in redirection. 
                // Read the cookie we saved before the redirection in RedirectToIdentityProvider callback in Startup.Auth.cs 
                activationParameters = new FileHandlerActivationParameters(CookieStorage.Load());
                
                // Clear the cookie after using it
                CookieStorage.Clear();
            }
            return activationParameters;
        }



        private async Task<MarkdownFileModel> GetFileHandlerModelAsync(FileHandlerActivationParameters input, FileAccess access)
        {
            if (!string.IsNullOrEmpty(input.ItemUrl))
            {
                return await GetFileHandlerModelV2Async(input);
            }

            // Retrieve an access token so we can make API calls
            string accessToken = null;
            try
            {
                accessToken = await AuthHelper.GetUserAccessTokenSilentAsync(input.ResourceId);
            }
            catch (Exception ex)
            {
                return MarkdownFileModel.GetErrorModel(input, ex);
            }

            // Get file content
            Stream fileContentStream = null;
            try
            {
                fileContentStream = await GetFileContentAsync(input, accessToken);
            }
            catch (Exception ex)
            {
                return MarkdownFileModel.GetErrorModel(input, ex);
            }

            // Convert the stream into text for rendering
            StreamReader reader = new StreamReader(fileContentStream);
            var markdownContent = await reader.ReadToEndAsync();

            return MarkdownFileModel.GetWriteableModel(input, string.Empty, markdownContent);
        }

        private async Task<SaveResults> SaveChangesToFileAsync(FileHandlerActivationParameters input)
        {
            // Retrieve an access token so we can make API calls
            var resourceUrl = AuthHelper.GetResourceFromUrl(input.ItemUrl);
            string accessToken = null;
            try
            {
                accessToken = await AuthHelper.GetUserAccessTokenSilentAsync(resourceUrl);
            }
            catch (Exception ex)
            {
                return new SaveResults { Error = ex.Message };
            }

            // Upload the new file content
            try
            {
                var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(input.FileContent));

                var result = await HttpHelper.Default.UploadFileContentsFromStreamAsync(stream, input.ItemUrl, accessToken);
                return new SaveResults { Success = result };
            }
            catch (Exception ex)
            {
                return new SaveResults { Error = ex.Message };
            }
        }

        private async Task<MarkdownFileModel> GetFileHandlerModelV2Async(FileHandlerActivationParameters input)
        {
            // Retrieve an access token so we can make API calls
            var resourceUrl = AuthHelper.GetResourceFromUrl(input.ItemUrl);
            string accessToken = null;
            try
            {
                accessToken = await AuthHelper.GetUserAccessTokenSilentAsync(resourceUrl);
            }
            catch (Exception ex)
            {
                return MarkdownFileModel.GetErrorModel(input, ex);
            }

            // Get file content
            FileData results = null;
            try
            {
                results = await HttpHelper.Default.GetStreamContentForItemUrlAsync(input.ItemUrl, accessToken);
            }
            catch (Exception ex)
            {
                return MarkdownFileModel.GetErrorModel(input, ex);
            }

            // Convert the stream into text for rendering
            StreamReader reader = new StreamReader(results.ContentStream);
            var markdownSource = await reader.ReadToEndAsync();

            return MarkdownFileModel.GetWriteableModel(input, results.Filename, markdownSource);
        }


        /// <summary>
        /// Download the contents of the file from the server and return as a stream.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="accessToken"></param>
        /// <returns></returns>
        private async Task<Stream> GetFileContentAsync(FileHandlerActivationParameters input, string accessToken)
        {
            // Use the input.FileGet URL to download the contents of the file
            var request = WebRequest.CreateHttp(input.FileGet);
            request.Headers.Add("Authorization", "Bearer " + accessToken);
            request.AllowAutoRedirect = false;

            HttpWebResponse httpResponse = null;

            try
            {
                var response = await request.GetResponseAsync();
                httpResponse = response as HttpWebResponse;
            }
            catch (WebException ex)
            {
                httpResponse = ex.Response as HttpWebResponse;
            }

            if (httpResponse == null)
            {
                throw new WebException("Request was unsuccessful.");
            }

            if (httpResponse.StatusCode == HttpStatusCode.OK)
            {
                MemoryStream responseStream = new MemoryStream();
                await httpResponse.GetResponseStream().CopyToAsync(responseStream);

                // Reset the memory stream
                responseStream.Seek(0, SeekOrigin.Begin);

                return responseStream;
            }
            else
            {
                throw new WebException("Http response had invalid status code: " + httpResponse.StatusCode);
            }
        }

        
    }
}