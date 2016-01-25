﻿using Devkoes.HttpMessage;
using Devkoes.Restup.WebServer.Http;
using Devkoes.Restup.WebServer.InstanceCreators;
using Devkoes.Restup.WebServer.Models.Contracts;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Devkoes.Restup.WebServer.Rest
{
    internal class RestControllerMethodWithBodyExecutor : IRestMethodExecutor
    {
        private ContentSerializer _bodySerializer;
        private RestResponseFactory _responseFactory;

        public RestControllerMethodWithBodyExecutor()
        {
            _bodySerializer = new ContentSerializer();
            _responseFactory = new RestResponseFactory();
        }

        public async Task<IRestResponse> ExecuteMethodAsync(RestControllerMethodInfo info, HttpServerRequest request)
        {
            var methodInvokeResult = ExecuteAnonymousMethod(info, request);

            if (!info.IsAsync)
                return await Task.Run(() => (IRestResponse)methodInvokeResult);

            return await (dynamic)methodInvokeResult;
        }

        private object ExecuteAnonymousMethod(RestControllerMethodInfo info, HttpServerRequest request)
        {
            var instantiator = InstanceCreatorCache.Default.GetCreator(info.MethodInfo.DeclaringType);

            object bodyObj = null;
            try
            {
                bodyObj = GetContentObject(info, request);
            }
            catch (JsonReaderException)
            {
                return _responseFactory.CreateBadRequest();
            }
            catch (InvalidOperationException)
            {
                return _responseFactory.CreateBadRequest();
            }

            object[] parameters = null;
            try
            {
                parameters = info.GetParametersFromUri(request.Uri).Concat(new[] { bodyObj }).ToArray();
            }
            catch (FormatException)
            {
                return _responseFactory.CreateBadRequest();
            }

            return info.MethodInfo.Invoke(
                    instantiator.Create(info.MethodInfo.DeclaringType, info.ControllerConstructorArgs()),
                    parameters);
        }

        private object GetContentObject(RestControllerMethodInfo info, HttpServerRequest request)
        {
            var contentType = request.ContentType ?? Configuration.Default.ContentType;
            var charset = request.ContentTypeCharset ?? HttpDefaults.Default.GetDefaultCharset(contentType);
            string content = _bodySerializer.GetContentString(charset, request.Content);
            object bodyObj = _bodySerializer.FromContent(content, contentType, info.BodyParameterType);

            return bodyObj;
        }
    }
}
