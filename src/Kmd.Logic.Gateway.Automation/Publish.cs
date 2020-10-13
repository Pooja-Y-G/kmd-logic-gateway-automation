﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kmd.Logic.Gateway.Automation.Gateway;
using Kmd.Logic.Gateway.Automation.Models;
using Kmd.Logic.Identity.Authorization;
using Microsoft.Rest;
using YamlDotNet.Serialization;

namespace Kmd.Logic.Gateway.Automation
{
    public class Publish : IPublish
    {
        private readonly HttpClient httpClient;
        private readonly GatewayOptions options;
        private readonly LogicTokenProviderFactory tokenProviderFactory;
        private IGatewayClient gatewayClient;
        private IList<PublishResult> publishResults;

        /// <summary>
        /// Initializes a new instance of the <see cref="Publish"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client to use. The caller is expected to manage this resource and it will not be disposed.</param>
        /// <param name="tokenProviderFactory">The Logic access token provider factory.</param>
        /// <param name="options">The required configuration options.</param>
        public Publish(HttpClient httpClient, LogicTokenProviderFactory tokenProviderFactory, GatewayOptions options)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.tokenProviderFactory = tokenProviderFactory ?? throw new ArgumentNullException(nameof(tokenProviderFactory));

#pragma warning disable CS0618 // Type or member is obsolete
            if (string.IsNullOrEmpty(this.tokenProviderFactory.DefaultAuthorizationScope))
            {
                this.tokenProviderFactory.DefaultAuthorizationScope = "https://logicidentityprod.onmicrosoft.com/bb159109-0ccd-4b08-8d0d-80370cedda84/.default";
            }

            this.publishResults = new List<PublishResult>();
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Create gateway entities.
        /// </summary>
        /// <param name="folderPath">Folder path provider all gateway entries details.</param>
        /// <returns>Error details on failure, gateway entities name on success.</returns>
        public async Task<IList<PublishResult>> Process(string folderPath)
        {
            if (!this.IsValidInput(folderPath))
            {
                return this.publishResults;
            }

            var yaml = new Deserializer().Deserialize<GatewayDetails>(File.OpenText(Path.Combine(folderPath, @"publish.yml")));
            var client = this.CreateClient();
            await this.CreateProductsAsync(client, this.options.SubscriptionId, this.options.ProviderId, yaml.Products, folderPath).ConfigureAwait(false);// yaml.Products.Select(product => CreateProduct(client, subscriptionId, providerId, product)).ToList();

            return this.publishResults;
        }

        private async Task CreateProductsAsync(IGatewayClient client, Guid subscriptionId, Guid providerId, IList<Product> products, string folderPath)
        {
            foreach (var product in products)
            {

                var response = await client.CreateProductAsync(subscriptionId: subscriptionId,
                                                               name: product.Name,
                                                               description: product.Description,
                                                               providerId: providerId.ToString(),
                                                               apiKeyRequired: product.ApiKeyRequired,
                                                               providerApprovalRequired: product.ProviderApprovalRequired,
                                                               productTerms: product.LegalTerms,
                                                               visibility: product.Visibility,
                                                               logo: this.GetStream(folderPath, product.Logo),
                                                               documentation: null,
                                                               clientCredentialRequired: product.ClientCredentialRequired,
                                                               openidConfigIssuer: product.OpenidConfigIssuer,
                                                               openidConfigCustomUrl: product.OpenidConfigCustomUrl,
                                                               applicationId: product.ApplicationId).ConfigureAwait(false);

                if (response != null)
                {
                    this.publishResults.Add(new PublishResult() { ResultCode = ResultCode.ProductCreated, EntityId = response.Id });
                }
            }
        }

        private Stream GetStream(string folderPath, string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return null;
            }

            //Stream imageStreamSource = new FileStream(Path.Combine(folderPath, fileName), FileMode.Open, FileAccess.Read, FileShare.Read);
            //return imageStreamSource;
            FileStream fs = new FileStream(path: Path.Combine(folderPath, fileName), FileMode.Open);
            return fs;
            //return File.OpenRead(Path.Combine(folderPath, fileName));
            var fileBytes = File.ReadAllBytes(Path.Combine(folderPath, fileName));           
            using (var fileStream = File.OpenRead(Path.Combine(folderPath, fileName)))
            {
                //return fileStream;
                var ms = new MemoryStream(fileBytes);
                
                //fileStream.re(ms);
                return ms;
            }
        }

        private bool IsValidInput(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                this.publishResults.Add(new PublishResult { IsError = true, ResultCode = ResultCode.InvalidInput, Message = "Specified folder doesn’t exist" });
                return false;
            }

            if (!File.Exists(Path.Combine(folderPath, @"publish.yml")))
            {
                this.publishResults.Add(new PublishResult { IsError = true, ResultCode = ResultCode.InvalidInput, Message = "Publish yml not found" });
                return false;
            }

            return true;
        }

        private IGatewayClient CreateClient()
        {
            if (this.gatewayClient != null)
            {
                return this.gatewayClient;
            }

            var tokenProvider = this.tokenProviderFactory.GetProvider(this.httpClient);

            this.gatewayClient = new GatewayClient(new TokenCredentials(tokenProvider))
            {
                BaseUri = this.options.GatewayServiceUri,
            };

            return this.gatewayClient;
        }
    }
}
