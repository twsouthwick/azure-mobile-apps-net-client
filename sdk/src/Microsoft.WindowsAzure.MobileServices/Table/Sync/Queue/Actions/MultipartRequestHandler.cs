using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Table.Sync.Queue.Actions
{
    class MultipartRequestHandler : DelegatingHandler
    {

        public MultipartRequestHandler()
        {
            this.Requests = new List<HttpRequestMessage>();
            this.operationsQueued = new TaskCompletionSource<bool>();
            this.Parts = new List<HttpMessageContent>();
            this.Promises = new List<TaskCompletionSource<HttpResponseMessage>>();
        }

        public List<HttpRequestMessage> Requests { get; set; }

        public List<HttpMessageContent> Parts { get; set; }

        public List<TaskCompletionSource<HttpResponseMessage>> Promises { get; set; }

        public int ExpectedRequests { get; set; }

        private TaskCompletionSource<bool> operationsQueued;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            // we convert the request to a multipart content
            HttpMessageContent content = new HttpMessageContent(request);
            this.Parts.Add(content);

            // create a new promise for this one
            var promise = new TaskCompletionSource<HttpResponseMessage>();
            this.Promises.Add(promise);

            if (this.Parts.Count >= this.ExpectedRequests)
            {
                this.operationsQueued.SetResult(true);
            }

            return promise.Task;
        }

        public async Task<bool> allOperationsQueued()
        {
            return await operationsQueued.Task;
        }
        
        /*
        private void performBatchRequest()
        {
            MultipartContent combinedContent = new MultipartContent("mixed", "batch_" + Guid.NewGuid().ToString());
            foreach (var content in this.Parts)
            {
                combinedContent.Add(content);
            }

            var batchedResponse = await baseClient.InvokeApiAsync(this.client.SyncContext.BatchApiEndpoint, finalContent, HttpMethod.Post, null, null, this.CancellationToken);
            MultipartMemoryStreamProvider responseContents = await batchedResponse.Content.ReadAsMultipartAsync();

        }
         */
    }
}
