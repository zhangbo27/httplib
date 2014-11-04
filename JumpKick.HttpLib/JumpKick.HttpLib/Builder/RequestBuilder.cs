﻿using JumpKick.HttpLib.Provider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace JumpKick.HttpLib.Builder
{
    public class RequestBuilder
    {
        private string url;
        private HttpVerb method;

        public RequestBuilder(string url, HttpVerb method)
        {
            this.url = url;
            this.method = method;
        }

        #region Headers
        private HeaderProvider headerProvider;

        public RequestBuilder Headers(object header)
        {
            Dictionary<String, String> headers = new Dictionary<String, String>();
            PropertyInfo[] properties;
#if NETFX_CORE
            properties = header.GetType().GetTypeInfo().DeclaredProperties.ToArray();
#else
            properties = header.GetType().GetProperties();
#endif

            foreach (var property in properties)
            {
                headers.Add(property.Name, System.Uri.EscapeDataString(property.GetValue(header, null).ToString()));
            }
            headerProvider = new DictionaryHeaderProvider(headers);
            return this;
        }

        public RequestBuilder Headers(IDictionary<String, String> header)
        {
            this.headerProvider = new DictionaryHeaderProvider(header);
            return this;
        }

        public RequestBuilder Headers(HeaderProvider headerProvider)
        {
            this.headerProvider = headerProvider;
            return this;
        }

        #endregion

        #region Auth
        private AuthenticationProvider authProvider;
        public RequestBuilder Auth(string username, string password)
        {
            authProvider = new BasicAuthenticationProvider(username, password);
            return this;
        }

        public RequestBuilder Auth(string text)
        {
            authProvider = new TextAuthenticationProvider(text);
            return this;
        }

        public RequestBuilder Auth(AuthenticationProvider provider)
        {
            authProvider = provider;
            return this;
        }
        #endregion

        #region Body
        private BodyProvider bodyProvider;

        public RequestBuilder Upload(NamedFileStream[] files, object parameters) 
        {
            MultipartBodyProvider bodyProvider = new MultipartBodyProvider();
            
            foreach (NamedFileStream file in files)
            {
                bodyProvider.AddFile(file);
            }

            bodyProvider.SetParameters(parameters);
            return this.Body(bodyProvider);
        }


        public RequestBuilder Upload(NamedFileStream[] files)
        {
            return this.Upload(files, new { });
        }

        public RequestBuilder Form(object body)
        {
            FormBodyProvider bodyProvider = new FormBodyProvider();
            bodyProvider.AddParameters(body);

            return this.Body(bodyProvider);
        }


        public RequestBuilder Form(IDictionary<String, String> body)
        {
            FormBodyProvider bodyProvider = new FormBodyProvider();
            bodyProvider.AddParameters(body);

            return this.Body(bodyProvider);
        }

        public RequestBuilder Body(Stream stream)
        {
            return this.Body(new StreamBodyProvider(stream));
        }

        public RequestBuilder Body(String contentType, Stream stream)
        {
            return this.Body(new StreamBodyProvider(contentType, stream));
        }


        public RequestBuilder Body(String text)
        {
            return this.Body(new TextBodyProvider(text));
        }

        public RequestBuilder Body(String contentType, String text)
        {
            return this.Body(new TextBodyProvider(contentType, text));
        }



        public RequestBuilder Body(BodyProvider provider)
        {
            if(this.method == HttpVerb.Head || this.method==HttpVerb.Get)
            {
                throw new InvalidOperationException("Cannot set the body of a GET or HEAD request");
            }

            this.bodyProvider = provider;
            return this;
        }

        #endregion

        #region Actions
        ActionProvider actionProvider;
        Action<WebHeaderCollection, Stream> success;
        Action<WebException> fail;
        public RequestBuilder OnSuccess(Action<WebHeaderCollection, String> action)
        {
            this.success = (headers, stream) =>
            {
                StreamReader reader = new StreamReader(stream);
                action(headers, reader.ReadToEnd());
            };


            return this;
        }

        public RequestBuilder OnSuccess(Action<String> action)
        {
            this.success = (headers, stream) =>
                {
                    StreamReader reader = new StreamReader(stream);
                    action(reader.ReadToEnd());
                };
            return this;
        }


        public RequestBuilder OnSuccess(Action<WebHeaderCollection, Stream> action)
        {
            this.success = action;
            return this;
        }

        public RequestBuilder DownloadTo(String filePath)
        {
            this.success = (headers, result) =>
                {
                    FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate);
                    result.CopyTo(fs);
                    fs.Close();
                };
            return this;
        }


        public RequestBuilder AppendTo(String filePath)
        {
            this.success = (headers, result) =>
            {
                FileStream fs = new FileStream(filePath, FileMode.Append);
                result.CopyTo(fs);
                fs.Close();
            };
            return this;
        }


        public RequestBuilder OnFail(Action<WebException> action)
        {
            fail = action;
            return this;
        }


        public RequestBuilder Action(ActionProvider action)
        {
            actionProvider = action;
            return this;
        }

        #endregion
  
        public void Go()
        { 
            /*
             * If an actionprovider has not been set, we create one.
             */
            if(this.actionProvider == null)
            {
                this.actionProvider = new SettableActionProvider(success, fail);
            }

            Request req = new Request
            {
                Url = url,
                Method = method,
                Action = actionProvider,
                Auth = authProvider,
                Headers = headerProvider
            };

            req.Go();
            
        }




    }
}