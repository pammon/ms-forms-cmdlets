﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Net;
using System.Security;
using System.Threading.Tasks;
using FormsPowerShellModule.Models;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FormsPowerShellModule
{
    public class FormsService : Service
    {
        private readonly UserService _userService;

        public FormsService(string tenantId = "", string clientId = "", string userName = null,
            SecureString password = null) : base(tenantId, clientId,
            new[] {"api://forms.office.com/c9a559d2-7aab-4f13-a6ed-e7e9c52aec87/Forms.Read"}, userName, password)
        {
            _userService = new UserService(tenantId, clientId, userName, password);
        }

        public override void Connect()
        {
            _userService.Connect();
            base.Connect();
            Instance = this;
        }

        public void DownloadDownloadExcelFile(string formId, string path, int minResponseId = 1,
            int maxResponseId = 1000)
        {
            FormsApiAuthenticationInformation authenticationInformation = GetFormsApiAuthenticationInformation();
            string cookieHeader = $"OIDCAuth.forms={authenticationInformation.OIDCAuthToken.Value};";
            string url =
                $"https://forms.office.com/formapi/DownloadExcelFile.ashx?formid={formId}&timezoneOffset=180&minResponseId={minResponseId}&maxResponseId={maxResponseId}";
            var webRequest = System.Net.WebRequest.Create(url);
            webRequest.Method = "GET";
            webRequest.Timeout = 60000;
            webRequest.ContentType = "application/json";
            webRequest.Headers.Add("cookie", cookieHeader);
            webRequest.Headers.Add("x-ms-forms-isdelegatemode", "true");

            using (var response = webRequest.GetResponse())
            {
                using (System.IO.Stream s = response.GetResponseStream())
                {
                    using (FileStream fs = File.Create(path))
                    {
                        s.CopyTo(fs);
                        fs.Close();
                    }
                }
            }
        }

        private FormsApiAuthenticationInformation GetFormsApiAuthenticationInformation()
        {
            using (WebBrowserFactory webBrowser = new WebBrowserFactory())
            {
               return webBrowser.AcquireToken().GetAwaiter().GetResult();
            }

        }


        public Forms[] GetForms(string userId, List<string> fields = null)
        {
            FormsApiAuthenticationInformation authenticationInformation = GetFormsApiAuthenticationInformation();
            string cookieHeader = $"OIDCAuth.forms={authenticationInformation.OIDCAuthToken.Value};";

            string url = $"https://forms.office.com/formapi/api/{authenticationInformation.TenantId}/users/{userId}/light/forms";
            if (fields != null && fields.Count > 0)
            {
                if (!fields.Any(f => f.ToLower().Equals("id")))
                {
                    fields.Add("id");
                }


                if (!fields.Any(f => f.ToLower().Equals("ownerid")))
                {
                    fields.Add("ownerId");
                }

                url = string.Concat(url, "?$select=", string.Join(",", fields.Select(f => f.FirstLetterToLowerCase())));
            }

            var webRequest = System.Net.WebRequest.Create(url);
            webRequest.Method = "GET";
            webRequest.Timeout = 12000;
            webRequest.ContentType = "application/json";
            webRequest.Headers.Add("cookie", cookieHeader);
            webRequest.Headers.Add("x-ms-forms-isdelegatemode", "true");

            try
            {
                using (var response = webRequest.GetResponse())
                {
                    using (System.IO.Stream s = response.GetResponseStream())
                    {
                        using (System.IO.StreamReader sr = new System.IO.StreamReader(s))
                        {
                            string stext = sr.ReadToEnd();
                            return JsonConvert.DeserializeObject<FormsResult>(stext,
                                    new JsonSerializerSettings()
                                    {
                                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                                        NullValueHandling = NullValueHandling.Ignore
                                    })
                                ?.Forms;
                        }
                    }
                }
            }
            catch (WebException ex) when ((ex.Response as HttpWebResponse)?.StatusCode == HttpStatusCode.NotFound)
            {
                // handle 404 exceptions
            }

            return new Forms[] { };
        }

        public Forms[] GetFormsFromDeletedUsers(List<string> fields = null)
        {
            return GetFormsByUserList(_userService.GetDeletedUsers(), fields);
        }

        public Forms[] GetForms(List<string> fields = null)
        {
            return GetFormsByUserList(_userService.GetUsers(), fields);
        }


        public async Task<bool> UpdateFormSettings(string userId, string formId, bool formClosed,
            string formClosedMessage)
        {
            using (WebBrowserFactory webBrowser = new WebBrowserFactory())
            {
                FormsApiAuthenticationInformation authenticationInformation = await webBrowser.AcquireToken();

                string body = "{\"settings\":\"{\\\"FormClosed\\\":" + $"{formClosed}".ToLower() +
                              ",\\\"FormClosedMessage\\\":\\\"" +
                              formClosedMessage + "\\\"}\"}";
                byte[] json = System.Text.Encoding.UTF8.GetBytes(body);

                CookieContainer cc = new CookieContainer();
                cc.Add(authenticationInformation.RequestVerificationToken.GetCookie());
                cc.Add(authenticationInformation.AadAuthForms.GetCookie());
                cc.Add(authenticationInformation.OIDCAuthToken.GetCookie());

                var webRequest = (HttpWebRequest) System.Net.WebRequest.Create(
                    $"https://forms.office.com/formapi/api/{authenticationInformation.TenantId}/users/{userId}/forms('{formId}')");
                webRequest.Timeout = 12000;
                webRequest.ContentType = "application/json";
                webRequest.CookieContainer = cc;
                webRequest.Host = "forms.office.com";
                webRequest.Headers.Add("x-ms-forms-isdelegatemode", "true");
                webRequest.Headers.Add("__requestverificationtoken", authenticationInformation.AntiForgeryToken);
                webRequest.Method = "PATCH";
                webRequest.ContentType = "application/json";
                webRequest.ContentLength = json.Length;

                using (Stream dataStream = webRequest.GetRequestStream())
                {
                    dataStream.Write(json, 0, json.Length);
                    dataStream.Close();
                }

                using (var response = (HttpWebResponse) webRequest.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.NoContent;
                }
            }
        }

        public async Task<Question[]> GetFormQuestions(string userId, string formId)
        {
            using (WebBrowserFactory webBrowser = new WebBrowserFactory())
            {
                FormsApiAuthenticationInformation authenticationInformation = await webBrowser.AcquireToken();

                CookieContainer cc = new CookieContainer();
                cc.Add(authenticationInformation.RequestVerificationToken.GetCookie());
                cc.Add(authenticationInformation.AadAuthForms.GetCookie());
                cc.Add(authenticationInformation.OIDCAuthToken.GetCookie());

                var webRequest =
                    (HttpWebRequest) System.Net.WebRequest.Create(
                        $"https://forms.office.com/formapi/api/{authenticationInformation.TenantId}/users/{userId}/forms('{formId}')/questions");
                webRequest.Timeout = 12000;
                webRequest.ContentType = "application/json";
                webRequest.CookieContainer = cc;
                webRequest.Host = "forms.office.com";
                webRequest.Headers.Add("x-ms-forms-isdelegatemode", "true");
                webRequest.Headers.Add("__requestverificationtoken", authenticationInformation.AntiForgeryToken);
                webRequest.Method = "GET";
                webRequest.ContentType = "application/json";
                using (var response = (HttpWebResponse) webRequest.GetResponse())
                {
                    using (System.IO.Stream s = response.GetResponseStream())
                    {
                        using (System.IO.StreamReader sr = new System.IO.StreamReader(s))
                        {
                            return JsonConvert.DeserializeObject<Questions>(sr.ReadToEnd(),
                                    new JsonSerializerSettings()
                                    {
                                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                                        NullValueHandling = NullValueHandling.Ignore
                                    })
                                ?.Items;
                        }
                    }
                }
            }
        }

        public async Task<Response[]> GetFormResponses(string userId, string formId)
        {
            using (WebBrowserFactory webBrowser = new WebBrowserFactory())
            {
                FormsApiAuthenticationInformation authenticationInformation = await webBrowser.AcquireToken();

                CookieContainer cc = new CookieContainer();
                cc.Add(authenticationInformation.RequestVerificationToken.GetCookie());
                cc.Add(authenticationInformation.AadAuthForms.GetCookie());
                cc.Add(authenticationInformation.OIDCAuthToken.GetCookie());

                var webRequest =
                    (HttpWebRequest) System.Net.WebRequest.Create(
                        $"https://forms.office.com/formapi/api/{authenticationInformation.TenantId}/users/{userId}/forms('{formId}')/responses");
                webRequest.Timeout = 12000;
                webRequest.ContentType = "application/json";
                webRequest.CookieContainer = cc;
                webRequest.Host = "forms.office.com";
                webRequest.Headers.Add("x-ms-forms-isdelegatemode", "true");
                webRequest.Headers.Add("__requestverificationtoken", authenticationInformation.AntiForgeryToken);
                webRequest.Method = "GET";
                webRequest.ContentType = "application/json";
                using (var response = (HttpWebResponse) webRequest.GetResponse())
                {
                    using (System.IO.Stream s = response.GetResponseStream())
                    {
                        using (System.IO.StreamReader sr = new System.IO.StreamReader(s))
                        {
                            return JsonConvert.DeserializeObject<Responses>(sr.ReadToEnd(),
                                    new JsonSerializerSettings()
                                    {
                                        ContractResolver = new CamelCasePropertyNamesContractResolver(),
                                        NullValueHandling = NullValueHandling.Ignore
                                    })
                                ?.Items;
                        }
                    }
                }
            }
        }

        public Task<bool> MoveFormToUser(string userId, string formId, string newOwnerId)
        {
            return MoveForm(userId, formId, newOwnerId, false);
        }

        public Task<bool> MoveFormToGroup(string userId, string formId, string groupId)
        {
            return MoveForm(userId, formId, groupId, true);
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="teantId"></param>
        /// <param name="userId"></param>
        /// <param name="formId"></param>
        /// <param name="newOwnerId">needs to be a globaladmin</param>
        /// <returns></returns>
        private async Task<bool> MoveForm(string userId, string formId, string newOwnerId, bool isNewOwnerGroup)
        {
            using (WebBrowserFactory webBrowser = new WebBrowserFactory())
            {
                FormsApiAuthenticationInformation authenticationInformation = await webBrowser.AcquireToken();

                string body = "{\"newOwnerId\":\"" + newOwnerId + "\",\"isNewOwnerGroup\":" +
                              $"{isNewOwnerGroup}".ToLower() + "}";
                byte[] json = System.Text.Encoding.UTF8.GetBytes(body);

                CookieContainer cc = new CookieContainer();
                cc.Add(authenticationInformation.RequestVerificationToken.GetCookie());
                cc.Add(authenticationInformation.AadAuthForms.GetCookie());
                cc.Add(authenticationInformation.OIDCAuthToken.GetCookie());

                var webRequest = (HttpWebRequest) System.Net.WebRequest.Create(
                    $"https://forms.office.com/formapi/api/{TenantId}/users/{userId}/light/forms('{formId}')/MoveForm");
                webRequest.Timeout = 12000;
                webRequest.ContentType = "application/json";
                webRequest.CookieContainer = cc;
                webRequest.Host = "forms.office.com";
                webRequest.Headers.Add("x-ms-forms-isdelegatemode", "true");
                webRequest.Headers.Add("__requestverificationtoken", authenticationInformation.AntiForgeryToken);
                webRequest.Method = "POST";
                webRequest.ContentType = "application/json";
                webRequest.ContentLength = json.Length;

                using (Stream dataStream = webRequest.GetRequestStream())
                {
                    dataStream.Write(json, 0, json.Length);
                    dataStream.Close();
                }

                using (var response = (HttpWebResponse) webRequest.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.OK;
                }
            }
        }


        private Forms[] GetFormsByUserList(User[] users, List<string> fields = null)
        {
            List<Forms> result = new List<Forms>();

            foreach (User user in users)
            {
                result.AddRange(GetForms(user.Id, fields));
            }

            return result.ToArray();
        }

        public static FormsService Instance;
    }
}